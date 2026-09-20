using System;
using System.Runtime.InteropServices;

namespace SHCDESE.Interop;

/// <summary>
/// Used to describe newly allocated/modified memory regions by the engine.
/// Specifically:
/// - c_game_memset_wrapper
/// - c_game_memmove_wrapper
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct EngineMemoryDescriptor
{
    public UInt64 Unknown;
    public IntPtr LastDestination;
    public Int32 LastByteCount;
    public UInt32 LastFillPattern;
}
