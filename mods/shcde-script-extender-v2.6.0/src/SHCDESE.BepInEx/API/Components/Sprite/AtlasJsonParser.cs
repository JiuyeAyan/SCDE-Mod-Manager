using SHCDESE.Extensions;
using SHCDESE.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using UnityEngine;

namespace SHCDESE.API.Components.Sprite;

internal static class AtlasJsonParser
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Parses an atlas JSON file into a list of AtlasFrameData.
    /// Supports two schemas:
    ///   1. Game's own raw sprite JSON (single frame per file: "m_Name", "m_Rect", "m_Pivot", "m_PixelsToUnits")
    ///   2. Multi-frame mod JSON: { "frames": [ { "name", "rect": {x,y,w,h}, "pivot": {x,y}, "pixelsPerUnit" } ] }
    /// </summary>
    public static List<AtlasFrameData> Parse(string jsonPath, out SpriteMaterialMode? materialMode, out bool invalidMaterialMode)
    {
        materialMode = null;
        invalidMaterialMode = false;
        string json;
        try
        {
            json = File.ReadAllText(jsonPath);
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Failed to read atlas JSON: [{jsonPath}]");
            return null;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            JsonElement root = doc.RootElement;

            if (root.TryGetPropertyIgnoreCase("material", out JsonElement materialElement))
            {
                if (materialElement.ValueKind != JsonValueKind.String || !SpriteMaterialModeParser.TryParse(materialElement.GetString(), out SpriteMaterialMode parsedMode))
                {
                    invalidMaterialMode = true;
                    LogHelper.Warning($"Invalid atlas material mode in [{jsonPath}]. Expected Auto, Plain, TeamColour, or Foliage.");
                }
                else
                {
                    materialMode = parsedMode;
                }
            }

            // Detect schema by probing for a root-level discriminator property
            if (root.TryGetProperty("m_Name", out _))
                return ParseGameRawFormat(root);

            if (root.TryGetProperty("frames", out _))
                return ParseModFormat(root);

            LogHelper.Warning($"Unrecognised atlas JSON schema in: [{jsonPath}]");
            return null;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Failed to parse atlas JSON: [{jsonPath}]");
            return null;
        }
    }

    // -----------------------------------------------------------------------
    // Schema 1: Game's own extracted single-sprite JSON
    // -----------------------------------------------------------------------

    private static List<AtlasFrameData> ParseGameRawFormat(JsonElement root)
    {
        string name = root.GetProperty("m_Name").GetString() ?? string.Empty;
        float ppu = root.TryGetProperty("m_PixelsToUnits", out JsonElement ppuEl) ? ppuEl.GetSingle() : 64f;

        JsonElement rect = root.GetProperty("m_Rect");
        JsonElement pivot = root.GetProperty("m_Pivot");

        return
        [
            new AtlasFrameData
            {
                Name = name,
                TextureRect = new Rect(
                    rect.GetProperty("m_X").GetSingle(),
                    rect.GetProperty("m_Y").GetSingle(),
                    rect.GetProperty("m_Width").GetSingle(),
                    rect.GetProperty("m_Height").GetSingle()
                ),
                Pivot = new Vector2(
                    pivot.GetProperty("m_X").GetSingle(),
                    pivot.GetProperty("m_Y").GetSingle()
                ),
                PixelsPerUnit = ppu
            }
        ];
    }

    // -----------------------------------------------------------------------
    // Schema 2: Simplified multi-frame mod JSON
    // -----------------------------------------------------------------------

    private static List<AtlasFrameData> ParseModFormat(JsonElement root)
    {
        // Optional global default PPU at the root level
        float defaultPpu = root.TryGetProperty("pixelsPerUnit", out JsonElement globalPpu) ? globalPpu.GetSingle() : 64f;

        JsonElement framesArray = root.GetProperty("frames");
        var result = new List<AtlasFrameData>(framesArray.GetArrayLength());

        foreach (JsonElement frame in framesArray.EnumerateArray())
        {
            string name = frame.GetProperty("name").GetString() ?? string.Empty;

            JsonElement r = frame.GetProperty("rect");
            float rx = r.GetProperty("x").GetSingle();
            float ry = r.GetProperty("y").GetSingle();
            float rw = r.GetProperty("w").GetSingle();
            float rh = r.GetProperty("h").GetSingle();

            float pivotX = 0.5f;
            float pivotY = 0.5f;
            if (frame.TryGetProperty("pivot", out JsonElement p))
            {
                if (p.TryGetProperty("x", out JsonElement px)) pivotX = px.GetSingle();
                if (p.TryGetProperty("y", out JsonElement py)) pivotY = py.GetSingle();
            }

            float ppu = frame.TryGetProperty("pixelsPerUnit", out JsonElement framePpu) ? framePpu.GetSingle() : defaultPpu;

            result.Add(new AtlasFrameData
            {
                Name = name,
                TextureRect = new Rect(rx, ry, rw, rh),
                Pivot = new Vector2(pivotX, pivotY),
                PixelsPerUnit = ppu
            });
        }

        return result;
    }
}
