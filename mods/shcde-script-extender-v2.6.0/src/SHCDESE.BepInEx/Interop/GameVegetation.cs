using SHCDESE.API.Components.Spatial;
using SHCDESE.Interop.Enums;
using SHCDESE.Lua.CodeGen;
using System;
using System.Runtime.InteropServices;

namespace SHCDESE.Interop;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct GameVegetation : IPositionable, IEquatable<GameVegetation>
{
    public UInt32 N0000486B; //0x0000
    public UInt32 N00004896; //0x0004
    public UInt32 N0000486C; //0x0008
    public UInt32 N00004898; //0x000C
    [LuaExposed] public GM r_GameMaterialIndex; //0x0010
    public UInt16 N0000542C;
    [LuaExposed] public UInt32 r_SpriteHueLevel; //0x0014
    public UInt32 N0000486E; //0x0018
    [LuaExposed] public UInt32 r_AnimationPlaybackSpeed; //0x001C
    public UInt16 N0000486F; //0x0020
    public UInt16 N00004951; //0x0022
    public UInt32 N0000489E; //0x0024
    public UInt32 N00004870; //0x0028
    public UInt32 N000048A0; //0x002C
    public UInt32 N00004871; //0x0030
    public UInt32 N000048A2; //0x0034
    public UInt32 N00004872; //0x0038
    public UInt32 N000048A4; //0x003C
    public UInt32 N00004873; //0x0040
    public UInt32 N000048A6; //0x0044
    public UInt32 N00004874; //0x0048
    public UInt32 N000048A8; //0x004C
    [LuaExposed] public AliveState r_AliveState; //0x0050
    [LuaExposed] public VegetationType r_VegetationType; //0x0052
	public UInt32 N000048AA; //0x0054
    [LuaExposed] public UInt32 r_GlobalId; //0x0058
    public UInt32 N000048AC; //0x005C
    public UInt32 N0000487B; //0x0060
    public UInt32 N000048AE; //0x0064
    [LuaExposed] public UInt16 r_WorldPositionX; //0x0068
    [LuaExposed] public UInt16 r_WorldPositionY; //0x006A
    public UInt16 N000048B0; //0x006C
    [LuaExposed] public UInt16 r_TilePositionX; //0x006E
    [LuaExposed] public UInt16 r_TilePositionY; //0x0070
    public UInt16 N000048B2_0; //0x0074
    [LuaExposed] public UInt32 r_TileId; //0x0074
    public UInt32 N0000488F; //0x0078
    public UInt16 N000048B4; //0x007C
    [LuaExposed] public UInt16 r_Health; //0x007E
    public UInt32 N00004890; //0x0080
    [LuaExposed] public Int16 r_ResourceState; //0x0084
    public UInt16 N00004958; //0x0086
    public UInt32 N00004892; //0x0088
    [LuaExposed] public UInt32 r_GrowthStage; //0x008C
    [LuaExposed] public UInt32 r_GrowthProgress; //0x0090
    public UInt32 N000048BA; //0x0094
    public UInt32 N00004894; //0x0098

    /// <summary>
    /// IPositionable: CurrentTilePosition
    /// </summary>
    /// <returns>Current tile position as UnmanagedVector2*</returns>
    public UnmanagedVector2<UInt16>* CurrentTilePosition()
    {
        fixed (UInt16* ptr = &r_TilePositionX)
        {
            return (UnmanagedVector2<UInt16>*)ptr;
        }
    }

    /// <summary>
    /// Determines whether the specified <see cref="GameVegetation"/> instance is equal to the current instance.
    /// </summary>
    /// <param name="other">The <see cref="GameVegetation"/> instance to compare with the current instance.</param>
    /// <returns>true if the specified instance has the same global identifier as the current instance; otherwise, false.</returns>
    public bool Equals(GameVegetation other)
    {
        return this.r_GlobalId == other.r_GlobalId;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct GameVegetationManager
{
    public UInt32 N000047CF; //0x0000
    public UInt32 N00004810; //0x0004
    public UInt32 TotalActive; //0x0008
    public UInt32 N00004812; //0x000C
    public UInt32 TotalAllocated; //0x0010
    public UInt32 N00004814; //0x0014
    public UInt32 N000047D2; //0x0018
    public UInt32 N00004816; //0x001C
    public UInt32 N000047D3; //0x0020
    public UInt32 N00004818; //0x0024
    public UInt32 N000047D4; //0x0028
    public UInt32 N0000481A; //0x002C
    public UInt32 N000047D5; //0x0030
    public UInt32 N0000481C; //0x0034
    public UInt32 N000047D6; //0x0038
    public UInt32 N0000481E; //0x003C
    public UInt32 N000047D7; //0x0040
    public UInt32 N00004820; //0x0044
    public UInt32 N000047D8; //0x0048
    public UInt32 N00004822; //0x004C
    public UInt32 N000047D9; //0x0050
    public UInt32 N00004824; //0x0054
    public UInt32 N000047DA; //0x0058
    public UInt32 N00004826; //0x005C
    public UInt32 N000047DB; //0x0060
    public UInt32 N00004828; //0x0064
    public UInt32 N000047DC; //0x0068
    public UInt32 N0000482A; //0x006C
    public UInt32 N000047DD; //0x0070
    public UInt32 N0000482C; //0x0074
    public UInt32 N000047DE; //0x0078
    public UInt32 N0000482E; //0x007C
    public UInt32 N000047DF; //0x0080
    public UInt32 N00004830; //0x0084
    public UInt32 N000047E0; //0x0088
    public UInt32 N00004832; //0x008C
    public UInt32 N000047E1; //0x0090
    public UInt32 N00004834; //0x0094
    public UInt32 N000047E2; //0x0098
    public UInt32 N00004836; //0x009C
    public UInt32 N000048E8; //0x00A0
    public UInt32 N000048D0; //0x00A4
    public UInt32 N000048EA; //0x00A8
    public UInt32 N000048D1; //0x00AC
    public UInt32 N000048EC; //0x00B0
    public GameVegetation VegetationArray;
}; //Size: 0x5EB4
