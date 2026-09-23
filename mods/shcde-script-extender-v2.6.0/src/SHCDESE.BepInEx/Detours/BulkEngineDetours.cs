using RedBird.Core.Memory;
using RedBird.Core.Memory.Scanners;
using RedBird.X64.Hooks.Transaction;
using RedBird.X64.Memory.Scanners;
using SHCDESE.API.LowLevel;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.Logging;
using System;
using System.Runtime.InteropServices;
using System.Security;

namespace SHCDESE.Detours;

[SuppressUnmanagedCodeSecurity]
public unsafe class BulkEngineDetours
{
    public BulkEngineDetours(ReadOnlySpan<byte> memory, ScanRegion region, HookTransaction tx, DataScanner scanner)
    {
        LogHelper.Information($"Applying");

        UInt64 currentImageBase = (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle;

        DataScanner c_game_memset_wrapper_scan = scanner.Scan(CompiledPattern.Parse("4C 63 D2 41 0F B6 C0"));
        if (c_game_memset_wrapper_scan.Found)
        {
            c_game_memset_wrapper = Marshal.GetDelegateForFunctionPointer<c_game_memset_wrapper_delegate>((IntPtr)c_game_memset_wrapper_scan.CurrentAddress);
        }
        else LogHelper.Warning($"Failed to find c_game_memset_wrapper");

        DataScanner c_game_memmove_wrapper_scan = scanner.Scan(CompiledPattern.Parse("49 8B C0 49 8B C9"));
        if (c_game_memmove_wrapper_scan.Found)
        {
            c_game_memmove_wrapper = Marshal.GetDelegateForFunctionPointer<c_game_memmove_wrapper_delegate>((IntPtr)c_game_memmove_wrapper_scan.CurrentAddress);
        }
        else LogHelper.Warning($"Failed to find c_game_memmove_wrapper");
    }

    // void *__fastcall c_game_memset_wrapper(__int64 pMemoryDescriptor, int size, unsigned __int8 val, void *ptr)
    // 4C 63 D2 41 0F B6 C0
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate byte* c_game_memset_wrapper_delegate(EngineMemoryDescriptor* pMemoryDescriptor, int size, byte value, byte* pDest);
    public static c_game_memset_wrapper_delegate c_game_memset_wrapper;

    // void *__fastcall c_game_memmove_wrapper(__int64 pMemoryDescriptor, int Size, const void *pSrc, void *pDst)
    // 49 8B C0 49 8B C9
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate byte* c_game_memmove_wrapper_delegate(EngineMemoryDescriptor* pMemoryDescriptor, int size, byte* pSrc, byte* pDest);
    public static c_game_memmove_wrapper_delegate c_game_memmove_wrapper;

}