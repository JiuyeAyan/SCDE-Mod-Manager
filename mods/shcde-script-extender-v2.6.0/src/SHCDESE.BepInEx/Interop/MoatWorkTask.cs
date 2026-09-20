using SHCDESE.Interop.Enums;
using SHCDESE.Lua.CodeGen;
using System;
using System.Runtime.InteropServices;

namespace SHCDESE.Interop;

/// <summary>
/// A native moat-work task slot. 
/// Slot zero is reserved as the no-task sentinel.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 0x10)]
public struct MoatWorkTask
{
    [LuaExposed] public Int32 r_TileId;
    [LuaExposed] public Int16 r_TileX;
    [LuaExposed] public Int16 r_TileY;
    public UInt16 N000_MoatWork_0008;
    public UInt16 N000_MoatWork_000A;
    [LuaExposed] public byte r_OwnerPlayerId;
    [LuaExposed] public byte r_Progress;
    [LuaExposed] public MoatWorkType r_WorkType;
    [LuaExposed] public byte r_ReservationPenalty;
}
