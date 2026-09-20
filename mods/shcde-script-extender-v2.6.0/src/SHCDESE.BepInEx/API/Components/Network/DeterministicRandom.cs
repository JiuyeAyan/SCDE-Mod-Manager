using MessagePack;
using R3;
using SHCDESE.API.Components.SaveData;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Network;
using SHCDESE.Logging;
using SHCDESE.Lua.DocsGen;
using System;

namespace SHCDESE.API.Components.Network;

/// <summary>
/// Provides a deterministic, synchronized random number generator for the game.
/// </summary>
/// <remarks>
/// This system is essential for any gameplay logic that requires randomness without causing desyncs in multiplayer.
/// It must be initialized once at the start of a match with a seed that is identical for all players.
/// The RNG state (seed) is automatically saved and restored with save files.
/// </remarks>
public static class DeterministicRandom
{
    private static Random? _rng;
    private static int? _currentSeed;
    private static bool _init = false;

    private static R3PacketEventHook<SeedSyncNetworkPacket>? _seedSyncEventHook;

    /// <summary>
    /// Internal data structure for serializing the random engine state.
    /// </summary>
    [MessagePackObject(true)]
    public class RandomEngineState
    {
        public int Seed { get; set; }
        public bool WasInitialized { get; set; }
    }

    internal static void InitializeSubscribers()
    {
        if (_init)
            return;

        LogHelper.Information($"Setting up subscribers");

        MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(OnStartMap);
        MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(OnUnloadMap);

        _seedSyncEventHook = GameNetworkAPI.Instance.GetPacketEventFor<SeedSyncNetworkPacket>(CustomNetworkPacketType.SeedSync);
        _seedSyncEventHook.GetBaseHook().Observable.Subscribe(OnReceiveSeedSyncPacket);

        // Register save/load handler for the random engine state
        ModSaveDataAPI.Instance.RegisterModDataHandler(
            modIdentifier: "se-deterministicrandom-state",
            saveCallback: SaveRandomState,
            loadCallback: LoadRandomState,
            onUnloadCallback: null
        );

        _init = true;
    }

    private static byte[]? SaveRandomState(SaveContext context)
    {
        // Only save for actual save files, not map editor saves
        if (!context.IsSaveFile)
        {
            LogHelper.Debug("Skipping save - not a save file");
            return null;
        }

        // If the RNG was never initialized, don't save anything
        if (!_currentSeed.HasValue)
        {
            LogHelper.Debug("No seed to save");
            return null;
        }

        var state = new RandomEngineState
        {
            Seed = _currentSeed.Value,
            WasInitialized = _rng != null
        };

        LogHelper.Information($"Saving state - Seed={state.Seed}, Initialized={state.WasInitialized}");
        return MessagePackSerializer.Serialize(state);
    }

    private static void LoadRandomState(byte[] bytes, LoadContext context)
    {
        try
        {
            RandomEngineState state = MessagePackSerializer.Deserialize<RandomEngineState>(bytes);

            LogHelper.Information($"Loading state - Seed={state.Seed}, WasInitialized={state.WasInitialized}");

            if (state.WasInitialized)
            {
                // Restore the RNG with the saved seed
                _currentSeed = state.Seed;
                _rng = new Random(state.Seed);

                LogHelper.Information($"RNG restored with seed {state.Seed}");
            }
            else
            {
                LogHelper.Information("RNG was not initialized in the save, skipping restoration");
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Failed to deserialize random engine state");
        }
    }

    private static void OnReceiveSeedSyncPacket(ReceiveCustomPacketEventArgs<SeedSyncNetworkPacket> e)
    {
        LogHelper.Information($"Received seed-sync packet: {e.Packet.Seed}, from player: {e.Packet.FromPlayerId}");
        Initialize(e.Packet.Seed);
    }

    private static void OnStartMap(MapStartEventArgs e)
    {
        if (e.Phase == EventHookPhase.Pre)
            return;

        LogHelper.Information("Initializing shared state.");

        // Only initialize a new seed if we're the host AND we don't already have a seed
        // (a loaded save will have restored the seed already)
        if (GameNetworkAPI.IsLocalHost() && !_currentSeed.HasValue)
        {
            InitializeSeedAsHost();
        }
        else if (_currentSeed.HasValue)
        {
            LogHelper.Information($"Using restored seed {_currentSeed.Value} from save file");

            // If we're the host and we loaded from a save, we need to sync this seed to other players
            if (GameNetworkAPI.IsLocalHost() && GameNetworkAPI.IsNetworkedEnvironment())
            {
                LogHelper.Information($"Syncing restored seed to all players");
                GameNetworkAPI.SendPacketToAll(new SeedSyncNetworkPacket()
                {
                    FromPlayerId = GameNetworkAPI.GetLocalPlayerId(),
                    Seed = _currentSeed.Value
                }, (int)CustomNetworkPacketType.SeedSync);
            }
        }
    }

    private static void OnUnloadMap(MapUnloadEventArgs e)
    {
        LogHelper.Information("uninitializing shared state.");

        Uninitialize();
    }

    internal static void InitializeSeedAsHost()
    {
        int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        LogHelper.Information($"Initializing random seed [{seed}] as host.");

        Initialize(seed);
        GameNetworkAPI.SendPacketToAll(new SeedSyncNetworkPacket()
        {
            FromPlayerId = GameNetworkAPI.GetLocalPlayerId(),
            Seed = seed
        }, (int)CustomNetworkPacketType.SeedSync);
    }

    /// <summary>
    /// Initializes the deterministic RNG with a shared seed. This must be called only once at the start of a match.
    /// WARNING: Do not ever call this from a map. This API call is purely for debugging purposes in offline mode.
    /// This will get called by the script extender backend automatically, there is no need for a normal map to use this function.
    /// </summary>
    /// <param name="seed">An integer seed that is identical for all players in the multiplayer session.</param>
    [LuaApiExport("Random_ForceInitialize")]
    public static void Initialize(int seed)
    {
        _currentSeed = seed;
        _rng = new Random(seed);
        LogHelper.Debug($"Initialized with seed {seed}");
    }

    /// <summary>
    /// Uninitializes the deterministic RNG.
    /// Note: The seed is preserved during uninitialization so it can be saved if needed.
    /// </summary>
    public static void Uninitialize()
    {
        _rng = null;
        _currentSeed = null;
    }

    /// <summary>
    /// Gets the current seed used by the random engine.
    /// Returns null if the engine has not been initialized.
    /// </summary>
    /// <returns>The current seed, or null if uninitialized.</returns>
    [LuaApiExport("Random_GetCurrentSeed")]
    public static int? GetCurrentSeed()
    {
        return _currentSeed;
    }

    /// <summary>
    /// Checks if the random engine is currently initialized.
    /// </summary>
    /// <returns>True if the engine is ready to generate random numbers, false otherwise.</returns>
    [LuaApiExport("Random_IsInitialized")]
    public static bool IsInitialized()
    {
        return _rng != null;
    }

    /// <summary>
    /// Returns a non-negative random integer.
    /// </summary>
    /// <returns>A 32-bit signed integer that is greater than or equal to 0; On error defaults to 0</returns>
    [LuaApiExport("Random_Next")]
    public static int Next()
    {
        if (_rng == null)
        {
            LogHelper.Error("Random Engine is uninitialized!");
            return 0;
        }

        return _rng.Next();
    }

    /// <summary>
    /// Returns a non-negative random integer that is less than the specified maximum.
    /// </summary>
    /// <param name="maxValue">The exclusive upper bound of the random number to be generated.</param>
    /// <returns>A 32-bit signed integer greater than or equal to 0, and less than maxValue; On error defaults to 0</returns>
    [LuaApiExport("Random_NextMax")]
    public static int Next(int maxValue)
    {
        if (_rng == null)
        {
            LogHelper.Error("Random Engine is uninitialized!");
            return 0;
        }

        return _rng.Next(maxValue);
    }

    /// <summary>
    /// Returns a random integer that is within a specified range.
    /// </summary>
    /// <param name="minValue">The inclusive lower bound of the random number returned.</param>
    /// <param name="maxValue">The exclusive upper bound of the random number returned.</param>
    /// <returns>A 32-bit signed integer greater than or equal to minValue and less than maxValue; On error defaults to 0</returns>
    [LuaApiExport("Random_NextMinMax")]
    public static int Next(int minValue, int maxValue)
    {
        if (_rng == null)
        {
            LogHelper.Error("Random Engine is uninitialized!");
            return 0;
        }

        return _rng.Next(minValue, maxValue);
    }
}