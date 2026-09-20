using SHCDESE.Logging;
using UnityEngine;

namespace SHCDESE.API.Components.Sprite;

/// <summary>
/// Resolves override materials from the material state established by the vanilla sprite loader. 
/// Reusing loaded shader references avoids relying on Shader.Find for shaders that Unity may have stripped from lookup tables.
/// </summary>
internal static class SpriteMaterialUtility
{
    internal const string PlainShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
    internal const string TeamColourShaderName = "Unlit/TeamColour";
    internal const string FoliageShaderName = "Unlit/Foliage";

    internal static bool TryDetectVanillaMode(spriteLoader loader, int fileID, out SpriteMaterialMode mode, out Shader? shader)
    {
        mode = SpriteMaterialMode.Auto;
        shader = null;

        if (fileID < 0 || fileID >= loader.gmMaterials.Length)
            return false;

        Material[]? materials = loader.gmMaterials[fileID];
        if (materials == null || materials.Length == 0)
            return false;

        if (ReferenceEquals(materials, loader.plainMaterials))
        {
            mode = SpriteMaterialMode.Plain;
            shader = FirstShader(materials);
            return true;
        }

        shader = FirstShader(materials);
        string shaderName = shader?.name ?? string.Empty;
        if (shaderName == FoliageShaderName)
        {
            mode = SpriteMaterialMode.Foliage;
            return true;
        }
        if (shaderName == TeamColourShaderName)
        {
            mode = SpriteMaterialMode.TeamColour;
            return true;
        }
        if (shaderName == PlainShaderName)
        {
            mode = SpriteMaterialMode.Plain;
            return true;
        }

        mode = SpriteMaterialMode.Auto;
        return false;
    }

    internal static bool TryResolveShader(spriteLoader loader, int fileID, SpriteMaterialMode mode, string context, out Shader? shader)
    {
        shader = null;
        if (mode == SpriteMaterialMode.Plain)
        {
            shader = FirstShader(loader.plainMaterials);
            if (shader == null)
                shader = Shader.Find(PlainShaderName);
        }
        else
        {
            if (TryDetectVanillaMode(loader, fileID, out SpriteMaterialMode vanillaMode, out Shader? vanillaShader) &&
                vanillaMode == mode)
            {
                shader = vanillaShader;
            }

            if (shader == null)
                shader = Shader.Find(GetShaderName(mode));
        }

        if (shader == null)
        {
            LogHelper.Warning($"Cannot apply [{context}]: shader [{GetShaderName(mode)}] was not found. The vanilla entry will be preserved.");
            return false;
        }

        string expectedShaderName = GetShaderName(mode);
        if (shader.name != expectedShaderName)
        {
            LogHelper.Warning($"Cannot apply [{context}]: material mode [{mode}] resolved unexpected shader [{shader.name}] instead of [{expectedShaderName}]. The vanilla entry will be preserved.");
            shader = null;
            return false;
        }

        if (!shader.isSupported)
        {
            LogHelper.Warning($"Cannot apply [{context}]: shader [{shader.name}] is unsupported. The vanilla entry will be preserved.");
            shader = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Builds the seven material slots expected by the vanilla loader. 
    /// Plain reuses its existing array, Foliage repeats one shared material, and TeamColour creates the seven vanilla cutoff variants.
    /// </summary>
    internal static bool TryBuildMaterials(spriteLoader loader, int fileID, SpriteMaterialMode mode, Texture2D? mask, string context, out Material[]? materials, out string shaderName)
    {
        materials = null;
        shaderName = GetShaderName(mode);
        if (!TryResolveShader(loader, fileID, mode, context, out Shader? shader))
            return false;

        shaderName = shader!.name;
        if (mode == SpriteMaterialMode.Plain)
        {
            if (loader.plainMaterials == null || loader.plainMaterials.Length < 7)
            {
                LogHelper.Warning($"Cannot apply [{context}]: the vanilla plain material array is unavailable. The vanilla entry will be preserved.");
                return false;
            }

            materials = loader.plainMaterials;
            return true;
        }

        if (mask == null)
        {
            LogHelper.Warning($"Cannot apply [{context}] with material [{mode}] without a mask. The vanilla entry will be preserved.");
            return false;
        }

        Material[] result = new Material[7];
        if (mode == SpriteMaterialMode.Foliage)
        {
            Material foliageMaterial = new Material(shader);
            if (!foliageMaterial.HasProperty("_TeamMask"))
            {
                LogHelper.Warning($"Cannot apply [{context}]: shader [{shader.name}] has no _TeamMask property. The vanilla entry will be preserved.");
                UnityEngine.Object.Destroy(foliageMaterial);
                return false;
            }

            foliageMaterial.SetTexture("_TeamMask", mask);
            for (int i = 0; i < result.Length; i++)
                result[i] = foliageMaterial;
        }
        else
        {
            for (int i = 0; i < result.Length; i++)
            {
                Material teamMaterial = new Material(shader);
                if (!teamMaterial.HasProperty("_TeamMask") || !teamMaterial.HasProperty("_SpriteCutoff"))
                {
                    LogHelper.Warning($"Cannot apply [{context}]: shader [{shader.name}] does not expose the expected _TeamMask and _SpriteCutoff properties. The vanilla entry will be preserved.");
                    DestroyUniqueMaterials(result);
                    UnityEngine.Object.Destroy(teamMaterial);
                    return false;
                }

                teamMaterial.SetTexture("_TeamMask", mask);
                teamMaterial.SetFloat("_SpriteCutoff", i == 0 ? 0f : (i + 4f) / 20f);
                result[i] = teamMaterial;
            }
        }

        materials = result;
        return true;
    }

    internal static string GetShaderName(SpriteMaterialMode mode) => mode switch
    {
        SpriteMaterialMode.Plain => PlainShaderName,
        SpriteMaterialMode.TeamColour => TeamColourShaderName,
        SpriteMaterialMode.Foliage => FoliageShaderName,
        _ => "Auto",
    };

    private static Shader? FirstShader(Material[]? materials)
    {
        if (materials == null)
            return null;

        foreach (Material material in materials)
        {
            if (material != null && material.shader != null)
                return material.shader;
        }

        return null;
    }

    private static void DestroyUniqueMaterials(Material[] materials)
    {
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null)
                continue;

            bool seen = false;
            for (int j = 0; j < i; j++)
            {
                if (ReferenceEquals(materials[j], material))
                {
                    seen = true;
                    break;
                }
            }

            if (!seen)
                UnityEngine.Object.Destroy(material);
        }
    }
}
