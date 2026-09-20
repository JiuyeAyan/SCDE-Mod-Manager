using SHCDESE.Interop.Enums;
using SHCDESE.Lua.CodeGen;
using System;
using System.Runtime.InteropServices;

namespace SHCDESE.Interop;

/// <summary>
/// A native macro-path connection record. 
/// Record zero is reserved; live records use IDs 1 through 199.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 0x204)]
public unsafe struct PathConnectionRecord
{
    [LuaExposed] public Int32 r_IsActive;
    [LuaExposed] public PathConnectionClass r_ConnectionClass;
    [LuaExposed] public UInt32 r_RecordGlobalId;
    [LuaExposed] public Int32 r_BuildingId;
    [LuaExposed] public Int32 r_UnitId;
    [LuaExposed] public UInt32 r_SubjectGlobalId;
    [LuaExposed] public Int32 r_IsEnabledOrOpen;
    [LuaExposed] public Int32 r_EntryTilePositionX;
    [LuaExposed] public Int32 r_EntryTilePositionY;
    [LuaExposed] public Int32 r_EntryTileId;
    [LuaExposed] public Int32 r_ExitTilePositionX;
    [LuaExposed] public Int32 r_ExitTilePositionY;
    [LuaExposed] public Int32 r_ExitTileId;
    [LuaExposed] public Int32 r_PathComponentA;
    [LuaExposed] public Int32 r_PathComponentB;
    [LuaExposed] public Int32 r_RegisteredUnitCount;
    public fixed Int32 r_RegisteredUnitIds[50];
    public fixed UInt32 r_RegisteredUnitGlobalIds[50];
    [LuaExposed] public Int32 r_SubtypeOrOrientation;
    public fixed Int32 N000_PathConnection_01D4[4];
    [LuaExposed] public Int32 r_OwnerOrAccessPlayerId;
    [LuaExposed] public Int32 r_PathComponentC;
    public fixed Int32 N000_PathConnection_01EC[6];
}
