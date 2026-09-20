using SHCDESE.API.Components.Spatial;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.Lua.CodeGen;
using System;
using System.Runtime.InteropServices;

namespace SHCDESE.Interop;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct GameProjectile : IPositionable, IEquatable<GameProjectile>
{
	public UInt32 N000053F4; //0x0000
    public UInt32 N00005425; //0x0004
    public UInt32 N000053F5; //0x0008
    public UInt32 N00005427; //0x000C
    public UInt32 N000053F6; //0x0010
    public UInt32 r_p_AnimRelated; //0x0014
    public UInt16 r_Sprite; //0x0018
    [LuaExposed] public GM r_GameMaterial; //0x001A
    [LuaExposed] public UInt32 r_UnitPlayerSourceId; //0x001C
    public UInt16 Unknown9; //0x0020
    [LuaExposed] public sbyte r_DistanceToGroundModifier; //0x0022
    public byte r_Unknown10; //0x0023
    public UInt16 r_Unknown5; //0x0024
    public UInt16 r_Unknown3; //0x0026
    public UInt16 r_Unknown4; //0x0028
    public UInt16 N000054EF; //0x002A
    public UInt32 N0000542F; //0x002C
    public UInt32 N000053FA; //0x0030
    public UInt32 N00005431; //0x0034
    public UInt32 N000053FB; //0x0038
    [LuaExposed] public AliveState r_AliveState; //0x003C
    [LuaExposed] public ProjectileType r_ProjectileType; //0x003E
    [LuaExposed] public UInt32 r_PlayerSourceId; //0x0040
    [LuaExposed] public UInt32 r_GlobalId; //0x0044 // Potentially r_GlobalId
    public UInt32 r_UnknownDword; //0x0048 // Potentially r_GlobalId
    [LuaExposed] public UInt16 r_SourceWorldTileX; //0x004C
    [LuaExposed] public UInt16 r_SourceWorldTileY; //0x004E
    [LuaExposed] public UInt16 r_SourceElevation; //0x0050
    [LuaExposed] public UInt16 r_TargetWorldTileX; //0x0052
    [LuaExposed] public UInt16 r_TargetWorldTileY; //0x0054
    [LuaExposed] public UInt16 r_TargetElevation; //0x0056
    [LuaExposed] public UInt16 r_CurrentTileX; //0x0058
    [LuaExposed] public UInt16 r_CurrentTileY; //0x005A
    public UInt32 N00005400; //0x005C
    [LuaExposed] public UInt32 r_CurrentTileId; //0x0060
    public UInt16 r_UnkWorldTileX; //0x0064
    public UInt16 r_UnkWorldTileY; //0x0066
    public UInt16 r_SourceUnknown2; //0x0068
    public UInt16 r_SourceUnknown3; //0x006A
    public UInt16 r_SourceUnknown4; //0x006C
    public UInt16 r_typeDependant4; //0x006E
    public UInt16 N00005441; //0x0070
    public UInt16 N0000A80F; //0x0072
    [LuaExposed] public Int16 r_BackwardsVelocity; //0x0074
    public UInt16 N0000A815; //0x0076
    public Int16 N00005443; //0x0078
    [LuaExposed] public Int16 r_VelocityModifier; //0x007A
    public UInt32 N00005404; //0x007C
    public UInt32 N00005445; //0x0080
    public UInt16 N00005405; //0x0084
    public Int16 N0000A821; //0x0086
    [LuaExposed] public UInt32 r_ArrowState; //0x0088
    [LuaExposed] public float r_VelocityGained; //0x008C
    [LuaExposed] public float r_VelocityX; //0x0090
    [LuaExposed] public float r_VelocityY; //0x0094
    public UInt32 r_FlyingRelated; //0x0098
    [LuaExposed] public UInt32 r_SpriteRotation; //0x009C
    public UInt32 N0000544D; //0x00A0
    public UInt32 r_SrcTargetInvolvedUnknown; //0x00A4
    public UInt16 N0000544F; //0x00A8
    [LuaExposed] public UInt16 r_InitialVelocityMagnitude; //0x00AA
    [LuaExposed] public float r_Velocity; //0x00AC
    [LuaExposed] public UInt16 r_FiringAngle; //0x00B0
    public Int16 N0000A845; //0x00B2
    [LuaExposed] public UInt16 r_TargetUnidId; //0x00B4
    [LuaExposed] public UInt16 r_SourceUnitId; //0x00B6
    public UInt16 Unknown8; //0x00B8
    [LuaExposed] public UInt32 r_AfterimageSprite; //0x00BA
    public UInt16 r_Unknown6; //0x00BE
    public UInt16 N0000A839; //0x00C0
    public UInt32 r_Unknown7; //0x00C2
    public UInt32 N0000540E; //0x00C6
    public UInt32 N00005459; //0x00CA
    public UInt32 N0000540F; //0x00CE
    public UInt32 N0000545B; //0x00D2
    public UInt32 N00005410; //0x00D6
    public UInt32 N0000545D; //0x00DA
    public UInt32 N00005411; //0x00DE
    public UInt16 N0000A849; //0x00E2
    public UInt32 N0000545F; //0x00E4

    /// <summary>
    /// IPositionable: CurrentTilePosition
    /// </summary>
    /// <returns>Current tile position as UnmanagedVector2*</returns>
    public UnmanagedVector2<UInt16>* CurrentTilePosition()
    {
        fixed (UInt16* ptr = &r_CurrentTileX)
        {
            return (UnmanagedVector2<UInt16>*)ptr;
        }
    }

    /// <summary>
    /// Determines whether the specified <see cref="GameProjectile"/> instance is equal to the current instance.
    /// </summary>
    /// <param name="other">The <see cref="GameProjectile"/> instance to compare with the current instance.</param>
    /// <returns>true if the specified instance has the same global identifier as the current instance; otherwise, false.</returns>
    public bool Equals(GameProjectile other)
    {
        return this.r_GlobalId == other.r_GlobalId;
    }
}; //Size: 0x00E8