using System;
using System.Runtime.InteropServices;

namespace SHCDESE.Interop;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct GameTribeManager
{
    public UInt32 CurrentSelectedTribeId; //0x0000
    public UInt32 ActiveAIGroups; //0x0004
    public UInt32 TotalAllocated; //0x0008
    public UInt32 LastCreatedTileX; //0x000C
    public UInt32 LastCreatedTileY; //0x0010
    public UInt32 N00000387; //0x0014
    public UInt32 N00000273; //0x0018
    public UInt32 N00000829; //0x001C
    public UInt32 N00000274; //0x0020
    public UInt32 N0002227C; //0x0024
    public UInt16 N00000AA3;//0x0028
    public GameTribe GameTribeArray; // Capacity: 4500
}; //Size: 0xF5572