using SHCDESE.EventAPI.Vegetation;
using SHCDESE.Lua.DocsGen;

namespace SHCDESE.EventAPI;

/// <summary>
/// Provides static R3EventHook fields for subscribing to events related to vegetation like trees and shrubs.
/// </summary>
public static class VegetationR3EventHooks
{
    /// <summary>
    /// Fired when a new vegetation object (e.g., a tree or shrub) is spawned in the world.
    /// </summary>
    [LuaApiExport("OnVegetationCreate")]
    public static readonly R3EventHook<VegetationCreateEventArgs> OnVegetationCreate = new();

    /// <summary>
    /// Fired when a vegetation object, typically a tree, advances its growth stage.
    /// </summary>
    [LuaApiExport("OnVegetationGrowth")]
    public static readonly R3EventHook<VegetationGrowthEventArgs> OnVegetationGrowth = new();

    /// <summary>
    /// Fired when a vegetation object is deleted from the game using the low-level deletion function.
    /// </summary>
    [LuaApiExport("OnVegetationDelete")]
    public static readonly R3EventHook<VegetationDeleteEventArgs> OnVegetationDelete = new();

    /// <summary>
    /// Fired when a tree takes damage, for example from a woodcutter.
    /// </summary>
    [LuaApiExport("OnVegetationTreeDamaged")]
    public static readonly R3EventHook<VegetationTreeDamagedEventArgs> OnVegetationTreeDamaged = new();

    /// <summary>
    /// Fired at the moment a tree's health is depleted and it falls over.
    /// </summary>
    [LuaApiExport("OnVegetationTreeFell")]
    public static readonly R3EventHook<VegetationTreeFellEventArgs> OnVegetationTreeFell = new();

    /// <summary>
    /// Fired when a resource (e.g., wood) is subtracted from a vegetation object, typically during harvesting.
    /// </summary>
    [LuaApiExport("OnVegetationSubtractResource")]
    public static readonly R3EventHook<VegetationSubtractResourceEventArgs> OnVegetationSubtractResource = new();
}