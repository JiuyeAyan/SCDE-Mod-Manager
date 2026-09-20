using System;

namespace SHCDESE.API.Components.ModManager;

public enum ModManifest : byte
{
    /// <summary>
    /// This is a asset map mod / scripted map (.lua) only
    /// </summary>
    Asset = 0,

    /// <summary>
    /// This is a BepInEx-style map mod and can feature anything
    /// </summary>
    BepInEx = 1
}
