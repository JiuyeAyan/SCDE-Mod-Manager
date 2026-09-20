using System;

namespace SHCDESE.Interop.Enums;

/// <summary>
/// Used 
/// </summary>
[Flags]
public enum CompactPlayerBitMask : byte
{
    None = 0,
    Player0 = 1 << 0,   // 1
    Player1 = 1 << 1,   // 2
    Player2 = 1 << 2,   // 4
    Player3 = 1 << 3,   // 8
    Player4 = 1 << 4,   // 16
    Player5 = 1 << 5,   // 32
    Player6 = 1 << 6,   // 64
    Player7 = 1 << 7,   // 128
    Neutral = byte.MaxValue,    // 255 / 0xFF / -1
}