using Noesis;
using SHCDESE.API;
using SHCDESE.Logging;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SHCDESE.ManagedHooks;

public partial class ManagedHookManager
{
    //
    // NoesisTextureProvider
    //

    // Reflection field to access the private dictionary
    private FieldInfo _texturesField;

    // Cache to prevent disk thrashing. 
    private HashSet<string> _attemptedTextureUris = new HashSet<string>();

    internal ManagedDetour<noesisTextureProvider_GetTextureInfoDelegate> noesisTextureProvider_GetTextureInfo_hook;
    internal delegate TextureProvider.TextureInfo noesisTextureProvider_GetTextureInfoDelegate(NoesisTextureProvider instance, Uri uri);

    /// <summary>
    /// A hook that intercepts the <see cref="NoesisTextureProvider.GetTextureInfo(Uri)"/> method.
    /// Acts as a lazy-loading mechanism for custom mod textures.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Noesis calls this method natively when it encounters an Image source in XAML.
    /// This hook inspects the internal <c>_textures</c> dictionary of the <see cref="NoesisTextureProvider"/>.
    /// </para>
    /// <para>
    /// If the requested <paramref name="uri"/> is missing from the dictionary:
    /// <list type="bullet">
    /// <item>It queries <see cref="GameAssetManagerAPI.TryLoadTexture(string, out Texture2D)"/> to find the file in mod folders.</item>
    /// <item>If found, it immediately calls NoesisTextureProvider.Register(string, Texture) to inject the texture.</item>
    /// </list>
    /// Finally, it executes the original trampoline, which will now successfully find the newly registered texture.
    /// </para>
    /// </remarks>
    /// <param name="instance">The singleton instance of <see cref="NoesisTextureProvider"/>.</param>
    /// <param name="uri">The URI of the texture requested by the Noesis engine.</param>
    /// <returns>
    /// A <see cref="TextureProvider.TextureInfo"/> struct containing the dimensions of the texture.
    /// </returns>
    internal TextureProvider.TextureInfo NoesisTextureProvider_GetTextureInfo(NoesisTextureProvider instance, Uri uri)
    {
        string cleanUri = uri.OriginalString;
        if (cleanUri.StartsWith("/"))
        {
            cleanUri = cleanUri.Substring(1);
        }

        // Initialize reflection access
        if (_texturesField == null)
        {
            _texturesField = typeof(NoesisTextureProvider).GetField("_textures", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        if (_attemptedTextureUris.Add(cleanUri))
        {
            // TryLoadTexture handles finding the file, loading it, and caching the Texture2D object.
            if (GameAssetManagerAPI.Instance.TryLoadTexture(cleanUri, out Texture2D? modTexture))
            {
                LogHelper.Information($"Registering Mod Texture: {cleanUri}");
                instance.Register(cleanUri, modTexture);
            }
        }


        LogHelper.Debug($"Retrieving Texture Info: {cleanUri}");
        return noesisTextureProvider_GetTextureInfo_hook.Trampoline(instance, uri);
    }
}
