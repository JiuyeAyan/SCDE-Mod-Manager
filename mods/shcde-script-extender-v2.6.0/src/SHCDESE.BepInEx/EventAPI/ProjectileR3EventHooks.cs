using SHCDESE.EventAPI.Projectiles;
using SHCDESE.Lua.DocsGen;

namespace SHCDESE.EventAPI;

/// <summary>
/// Provides static R3EventHook fields for subscribing to projectile-specific events.
/// </summary>
public static class ProjectileR3EventHooks
{
    /// <summary>
    /// Fired when a projectile is spawned.
    /// </summary>
    [LuaApiExport("OnProjectileSpawn")]
    public static readonly R3EventHook<ProjectileSpawnEventArgs> OnProjectileSpawn = new();

    /// <summary>
    /// Fired when a projectile is deleted.
    /// </summary>
    [LuaApiExport("OnProjectileDelete")]
    public static readonly R3EventHook<ProjectileDeleteEventArgs> OnProjectileDelete = new();

    /// <summary>
    /// Fired when fire is spawned through any means. This is what instantiates the fire projectiles in the first place.
    /// </summary>
    [LuaApiExport("OnSpawnFire")]
    public static readonly R3EventHook<SpawnFireEventArgs> OnSpawnFire = new();
}
