using System;

namespace SHCDESE.API.Components.ModManager;

/// <summary>
/// Specifies the network mode of a mod, indicating whether it is client-side only or networked.
/// </summary>
public enum ModNetworkMode
{
    Clientside = 0,
    Networked = 1
}
