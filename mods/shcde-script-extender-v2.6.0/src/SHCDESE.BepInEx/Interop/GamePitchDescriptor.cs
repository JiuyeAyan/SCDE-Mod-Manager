using SHCDESE.API.Components.Spatial;
using SHCDESE.Interop.Enums;
using SHCDESE.Lua.CodeGen;
using System;
using System.Runtime.InteropServices;

namespace SHCDESE.Interop;

/// <summary>
/// Represents a single pitch (tar/fire-trap) entry as it exists in native memory.
/// The struct is exactly 20 bytes and maps directly onto the game's pitch array,
/// which begins at <c>TileManager + 0x2038E48</c> with a stride of 20 bytes per slot.
/// </summary>
/// <remarks>
/// Field layout reverse-engineered from <c>c_game_player_build_pitch_ditch</c>:
/// <code>
/// +0x00  uint32   GlobalId      - gGlobalIdsUsed assigned at creation
/// +0x04  int16    OwnerId       - player who placed the pitch
/// +0x06  uint16   TileX         - tile X coordinate
/// +0x08  uint16   TileY         - tile Y coordinate
/// +0x0A  uint16   RandomSeed    - visual noise seed (gRandomShort+1)
/// +0x0C  uint32   State         - flags/state, 0 = normal
/// +0x10  uint32   _padding      - reserved
/// </code>
/// A slot is considered free when the sentinel word at
/// <c>PitchArrayBase + 0x18 + stride*(id-1)</c> is zero (game's own scan logic).
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct GamePitchDescriptor : IPositionable, IEquatable<GamePitchDescriptor>
{
    /// <summary>Global unique object ID assigned at creation time.</summary>
    [LuaExposed] public UInt32 GlobalId;

    /// <summary>The ID of the player who placed this pitch tile.</summary>
    [LuaExposed] public Int16 OwnerId;

    /// <summary>Tile X coordinate.</summary>
    [LuaExposed] public UInt16 TileX;

    /// <summary>Tile Y coordinate.</summary>
    [LuaExposed] public UInt16 TileY;

    /// <summary>
    /// Random visual noise seed written from <c>gRandomShort+1</c> at placement time.
    /// </summary>
    [LuaExposed] public UInt16 RandomSeed;

    /// <summary>
    /// The current state this pitch trap is in.
    /// </summary>
    [LuaExposed] public PitchState State;

    /// <summary>
    /// The tick timer that keeps track for how much time this trap was
    /// active for already (post-ignite)
    /// </summary>
    [LuaExposed] public Int16 FireTimer;

    /// <summary>
    /// Probably padding, unknown
    /// </summary>
    [LuaExposed] public UInt32 Reserved;

    /// <summary>
    /// Returns <c>true</c> if this slot is occupied (GlobalId != 0).
    /// Mirrors the game's own free-slot sentinel check.
    /// </summary>
    public readonly bool IsAlive => GlobalId != 0;

    /// <summary>
    /// IPositionable: CurrentTilePosition
    /// </summary>
    /// <returns>Current tile position as UnmanagedVector2*</returns>
    public UnmanagedVector2<UInt16>* CurrentTilePosition()
    {
        fixed (UInt16* ptr = &TileX)
        {
            return (UnmanagedVector2<UInt16>*)ptr;
        }
    }

    /// <summary>
    /// Determines whether the specified <see cref="GamePitchDescriptor"/> instance is equal to the current instance.
    /// </summary>
    /// <param name="other">The <see cref="GamePitchDescriptor"/> instance to compare with the current instance.</param>
    /// <returns>true if the specified instance has the same global identifier as the current instance; otherwise, false.</returns>
    public bool Equals(GamePitchDescriptor other)
    {
        return this.GlobalId == other.GlobalId;
    }
}
