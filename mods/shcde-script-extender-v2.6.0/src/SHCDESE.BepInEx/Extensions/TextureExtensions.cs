using Pfim;
using Pfim.dds;
using SHCDESE.Logging;
using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace SHCDESE.Extensions;

/// <summary>
/// Provides extension methods for the <see cref="UnityEngine.Texture2D"/> class
/// </summary>
public static class TextureExtensions
{
    /// <summary>
    /// Loads a raw image file (PNG, JPG, TGA, or block-compressed DDS) from the specified disk path into a new <see cref="Texture2D"/>.
    /// </summary>
    /// <remarks>
    /// </remarks>
    /// <param name="filePath">The absolute path to the image file on disk.</param>
    /// <param name="format">The format of the texture file</param>
    /// <param name="mipChain">Whether to use mipchains</param>
    /// <param name="premultiplyAlpha">Whether to premultiply the alpha of the output texture</param>
    /// <returns>
    /// A valid <see cref="Texture2D"/> containing the image data if successful; otherwise, <c>null</c>.
    /// </returns>
    public static Texture2D? LoadTexture(string filePath, TextureFormat format = TextureFormat.RGBA32, bool mipChain = false, bool premultiplyAlpha = true)
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
    public static Texture2D? LoadTexture(byte[] fileData, TextureFormat format = TextureFormat.RGBA32, bool mipChain = false, bool premultiplyAlpha = true)
    {
        if (fileData == null || fileData.Length == 0)
            return null;

        // DDS keeps its GPU-compressed layout; format/mipChain are taken from the file.
        if (IsDds(fileData))
            return LoadDds(fileData);

        // size (2,2) will be replaced by LoadImage automatically.
        Texture2D tex = new Texture2D(2, 2, format, mipChain);
        if (!tex.LoadImage(fileData))
            return null;

        if (premultiplyAlpha)
        {
            Texture2D paTex = PremultiplyAlpha(tex);
            UnityEngine.Object.Destroy(tex);
            return paTex;
        }
        return tex;
    }

    private static readonly PfimConfig DdsConfig = new(decompress: false);

    /// <summary>Returns <c>true</c> if the data starts with the DDS magic number.</summary>
    public static bool IsDds(byte[]? data) => 
        data != null && data.Length >= 4 && 
        data[0] == (byte)'D' && 
        data[1] == (byte)'D' && 
        data[2] == (byte)'S' && 
        data[3] == (byte)' ';

    /// <summary>
    /// Loads a BC1 (DXT1), BC3 (DXT5) or BC7 DDS without decompressing it. 
    /// Pfim parses the header (including the DX10 header), and the compressed blocks are uploaded to the GPU as-is, then the CPU copy is released (the texture is not readable).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Rows are uploaded unchanged, so the DDS must be stored bottom-up (vertically flipped), which is Unity's layout.</item>
    /// <item>Alpha is not modified; premultiply it when authoring the file if the consumer expects premultiplied alpha.</item>
    /// <item>The file's mip chain is used if it is complete; otherwise only the base level is used.</item>
    /// <item>Other DDS formats (BC2, BC4-BC6H, uncompressed) are rejected.</item>
    /// </list>
    /// </remarks>
    /// <returns>The texture, or <c>null</c> if the file is unsupported or invalid.</returns>
    public static Texture2D? LoadDds(byte[] fileData)
    {
        Texture2D? tex = null;
        try
        {
            using Dds dds = Dds.Create(fileData, DdsConfig);

            TextureFormat format;
            int blockBytes;
            if (dds is Bc7Dds) 
            { 
                format = TextureFormat.BC7; 
                blockBytes = 16; 
            }
            else if (dds is Dxt5Dds) 
            { 
                format = TextureFormat.DXT5; 
                blockBytes = 16; 
            }
            else if (dds is Dxt1Dds) 
            { 
                format = TextureFormat.DXT1; 
                blockBytes = 8; 
            }
            else
            {
                LogHelper.Warning($"Unsupported DDS format [{dds.GetType().Name}]. Supported: BC1 (DXT1), BC3 (DXT5), BC7.");
                return null;
            }

            int width = dds.Width;
            int height = dds.Height;

            int fullChain = 1;
            for (int size = Math.Max(width, height); size > 1; size >>= 1)
                fullChain++;

            bool useMips = Math.Max(1, (int)dds.Header.MipMapCount) == fullChain;
            int uploadBytes = useMips ? dds.DataLen : ((width + 3) / 4) * ((height + 3) / 4) * blockBytes;
            if (!useMips && dds.Header.MipMapCount > 1)
                LogHelper.Debug($"DDS has an incomplete mip chain ({dds.Header.MipMapCount}/{fullChain}); using the base level only.");

            if (dds.Data == null || dds.Data.Length < uploadBytes)
            {
                LogHelper.Warning("DDS data is truncated.");
                return null;
            }

            tex = new Texture2D(width, height, format, useMips, false);
            GCHandle handle = GCHandle.Alloc(dds.Data, GCHandleType.Pinned);
            try
            {
                tex.LoadRawTextureData(handle.AddrOfPinnedObject(), uploadBytes);
            }
            finally
            {
                handle.Free();
            }

            tex.Apply(false, true); // upload, then drop the CPU copy
            return tex;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Failed to load DDS texture");
            if (tex != null)
                UnityEngine.Object.Destroy(tex);
            return null;
        }
    }

    /// <summary>
    /// Creates a new texture where the RGB color values are multiplied by their alpha component (Premultiplied Alpha).
    /// This does not destroy the source texture.
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
