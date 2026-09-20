using SHCDESE.Logging;
using System;
using System.IO;
using UnityEngine;

namespace SHCDESE.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="UnityEngine.Texture2D"/> class
/// </summary>
public static class TextureExtensions
{
    /// <summary>
    /// Loads a raw image file (PNG, JPG, TGA) from the specified disk path into a new <see cref="Texture2D"/>.
    /// </summary>
    /// <remarks>
    /// </remarks>
    /// <param name="filePath">The absolute path to the image file on disk.</param>
    /// <returns>
    /// A valid <see cref="Texture2D"/> containing the image data if successful; otherwise, <c>null</c>.
    /// </returns>
    public static Texture2D? LoadTexture(string filePath, TextureFormat format = TextureFormat.RGBA32, bool mipChain = false)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            Texture2D? tex = LoadTexture(File.ReadAllBytes(filePath), format, mipChain);
            if (tex != null)
                tex.name = Path.GetFileNameWithoutExtension(filePath);
            return tex;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Failed to load texture from [{filePath}]");
        }
        return null;
    }

    /// <summary>Loads an image from memory, allowing packed mod resources to remain unextracted.</summary>
    public static Texture2D? LoadTexture(byte[] fileData, TextureFormat format = TextureFormat.RGBA32, bool mipChain = false)
    {
        if (fileData == null || fileData.Length == 0)
            return null;

        // size (2,2) will be replaced by LoadImage automatically.
        Texture2D tex = new Texture2D(2, 2, format, mipChain);
        if (!tex.LoadImage(fileData))
            return null;

        return PremultiplyAlpha(tex);
    }

    /// <summary>
    /// Creates a new texture where the RGB color values are multiplied by their alpha component (Premultiplied Alpha).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unity's Texture2D.LoadImage(byte[]) loads textures with Straight Alpha.
    /// However, UI frameworks like Noesis often require Premultiplied Alpha for correct transparency blending.
    /// </para>
    /// <para>
    /// This method creates a deep copy of the texture data. Be mindful of memory usage with large textures.
    /// </para>
    /// </remarks>
    /// <param name="src">The source texture containing Straight Alpha colors.</param>
    /// <returns>
    /// A new <see cref="Texture2D"/> instance with premultiplied pixel data.
    /// </returns>
    public static Texture2D PremultiplyAlpha(Texture2D src)
    {
        Texture2D tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
        Color[] px = src.GetPixels();

        for (int i = 0; i < px.Length; i++)
        {
            float a = px[i].a;
            px[i].r *= a;
            px[i].g *= a;
            px[i].b *= a;
        }

        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}
