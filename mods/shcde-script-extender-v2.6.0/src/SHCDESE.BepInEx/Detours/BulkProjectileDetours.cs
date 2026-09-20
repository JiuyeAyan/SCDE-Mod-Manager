using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Projectiles;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.Logging;
using System;
using System.Runtime.InteropServices;
using System.Security;

namespace SHCDESE.Detours;

[SuppressUnmanagedCodeSecurity]
internal class BulkProjectileDetours
{
    private HookTransaction? tx;
    public BulkProjectileDetours(ReadOnlySpan<byte> memory, ScanRegion region)
    {
        LogHelper.Information($"Applying");

        UInt64 currentImageBase = (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle;
        tx ??= new HookTransaction(region, Plugin.Instance.LoggerFactory);

        tx.AddDetour(c_game_projectile_spawn_hook,
             "48 89 5C 24 ? 44 89 4C 24 ? 44 89 44 24 ? 89 54 24 ? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ? 8B BC 24",
             c_game_projectile_spawn_hook_impl);

        tx.AddDetour(c_game_projectile_delete_hook,
             "48 89 5C 24 ?? 57 48 83 EC ?? 48 63 DA 48 8B F9 8B D3 E8",
             c_game_projectile_delete_hook_impl);

        tx.AddDetour(c_game_spawn_fire_internal_hook,
             "44 89 4C 24 ? 53 56 57",
             c_game_spawn_fire_internal_hook_impl);

        tx.Commit();

    }

    //
    // __int64 __fastcall c_game_spawn_fire_internal(int playerSourceId, int worldTileX, int worldTileY, int heightElevation, int spreadRadius, int a6)
    // 44 89 4C 24 ? 53 56 57
    //
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int c_game_spawn_fire_internal_delegate(int sourcePlayerId, int worldTileX, int worldTileY, int heightElevation, int spreadRadius, int a6);
    internal static DetourHandle<c_game_spawn_fire_internal_delegate> c_game_spawn_fire_internal_hook = new();
    public static int c_game_spawn_fire_internal_hook_impl(int sourcePlayerId, int worldTileX, int worldTileY, int heightElevation, int spreadRadius, int a6)
    {
        LogHelper.Debug($"sourcePlayerId={sourcePlayerId}, worldTileX={worldTileX}, worldTileY={worldTileY}, heightElevation={heightElevation}, spreadRadius={spreadRadius}, a6={a6}");

        SpawnFireEventArgs eventArgs = new(EventHookPhase.Pre, sourcePlayerId, worldTileX, worldTileY, heightElevation, spreadRadius, a6);
        ProjectileR3EventHooks.OnSpawnFire.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            int originalResult = c_game_spawn_fire_internal_hook.Original(
                eventArgs.SourcePlayerId,
                eventArgs.WorldTileX,
                eventArgs.WorldTileY,
                eventArgs.HeightElevation,
                eventArgs.SpreadRadius,
                eventArgs.A6
            );
            eventArgs.ReturnValue = originalResult;
            SpawnFireEventArgs postEventArgs = new(EventHookPhase.Post, sourcePlayerId, worldTileX, worldTileY, heightElevation, spreadRadius, a6)
            {
                ReturnValue = originalResult
            };
            ProjectileR3EventHooks.OnSpawnFire.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return (int)eventArgs.ReturnValue;
    }

    //__int64 __fastcall c_game_projectile_delete(__int64 pProjectileManager, unsigned int projectileId)
    // 48 89 5C 24 ?? 57 48 83 EC ?? 48 63 DA 48 8B F9 8B D3 E8
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void c_game_projectile_delete_delegate(NativePointer<GameProjectileManager> pProjectileManager, int projectileId);
    internal static DetourHandle<c_game_projectile_delete_delegate> c_game_projectile_delete_hook = new();
    public static void c_game_projectile_delete_hook_impl(NativePointer<GameProjectileManager> pProjectileManager, int projectileId)
    {
        //LogHelper.Debug($"pProjectileManager={new IntPtr(pProjectileManager).ToString("X16")}, sourceUnitId={sourceUnitId}, playerSourceId={playerSourceId}, unitPlayerSourceId={unitPlayerSourceId}, sourceWorldTileX={sourceWorldTileX}, sourceWorldTileY={sourceWorldTileY}, sourceUnknown={sourceUnknown}, targetWorldTileX={targetWorldTileX}, targetWorldTileY={targetWorldTileY}, targetUnknown={targetUnknown}, projectileType_arg={projectileType_arg}, attackedUnitId={attackedUnitId}");
        ProjectileDeleteEventArgs eventArgs = new(EventHookPhase.Pre, projectileId);
        ProjectileR3EventHooks.OnProjectileDelete.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            c_game_projectile_delete_hook.Original(
                pProjectileManager,
                eventArgs.ProjectileId
            );

            ProjectileDeleteEventArgs postEventArgs = new(EventHookPhase.Post, projectileId);
            ProjectileR3EventHooks.OnProjectileDelete.Raise(postEventArgs);
        }
    }

    // __int64 __fastcall c_game_projectile_spawn(__int64 pProjectileManager, int sourceUnitId, __int16 playerSourceId, int unitPlayerSourceId, int sourceWorldTileX, int sourceWorldTileY, int sourceUnknown, int targetWorldTileX, int targetWorldTileY, int targetUnknown, ProjectileType projectileType_arg, int attackedUnitId)
    // 48 89 5C 24 ? 44 89 4C 24 ? 44 89 44 24 ? 89 54 24 ? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ? 8B BC 24
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_projectile_spawn_delegate(NativePointer<GameProjectileManager> pProjectileManager, int sourceUnitId, Int16 playerSourceId, int unitPlayerSourceId, int sourceWorldTileX, int sourceWorldTileY, int sourceUnknown, int targetWorldTileX, int targetWorldTileY, int targetUnknown, ProjectileType projectileType_arg, int attackedUnitId);
    internal static DetourHandle<c_game_projectile_spawn_delegate> c_game_projectile_spawn_hook = new();
    public static Int64 c_game_projectile_spawn_hook_impl(NativePointer<GameProjectileManager> pProjectileManager, int sourceUnitId, Int16 playerSourceId, int unitPlayerSourceId, int sourceWorldTileX, int sourceWorldTileY, int sourceElevation, int targetWorldTileX, int targetWorldTileY, int targetElevation, ProjectileType projectileType_arg, int attackedUnitId)
    {
        //LogHelper.Debug($"pProjectileManager={pProjectileManager}, sourceUnitId={sourceUnitId}, playerSourceId={playerSourceId}, unitPlayerSourceId={unitPlayerSourceId}, sourceWorldTileX={sourceWorldTileX}, sourceWorldTileY={sourceWorldTileY}, sourceElevation={sourceElevation}, targetWorldTileX={targetWorldTileX}, targetWorldTileY={targetWorldTileY}, targetElevation={targetElevation}, projectileType_arg={projectileType_arg}, attackedUnitId={attackedUnitId}");
        ProjectileSpawnEventArgs eventArgs = new(EventHookPhase.Pre, sourceUnitId, playerSourceId, unitPlayerSourceId,sourceWorldTileX, sourceWorldTileY, sourceElevation, targetWorldTileX, targetWorldTileY, targetElevation, projectileType_arg, attackedUnitId);
        ProjectileR3EventHooks.OnProjectileSpawn.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_projectile_spawn_hook.Original(
                pProjectileManager,
                eventArgs.SourceUnitId,
                eventArgs.PlayerSourceId,
                eventArgs.UnitPlayerSourceId,
                eventArgs.SourceWorldTileX,
                eventArgs.SourceWorldTileY,
                eventArgs.SourceElevation,
                eventArgs.TargetWorldTileX,
                eventArgs.TargetWorldTileY,
                eventArgs.TargetElevation,
                eventArgs.ProjectileType,
                eventArgs.AttackedUnitId
            );
            eventArgs.ReturnValue = originalResult;
            ProjectileSpawnEventArgs postEventArgs = new(EventHookPhase.Post, sourceUnitId, playerSourceId, unitPlayerSourceId, sourceWorldTileX, sourceWorldTileY, sourceElevation, targetWorldTileX, targetWorldTileY, targetElevation, projectileType_arg, attackedUnitId)
            {
                ReturnValue = originalResult
            };
            ProjectileR3EventHooks.OnProjectileSpawn.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }
}
