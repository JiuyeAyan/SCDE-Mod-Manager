using Iced.Intel;
using Microsoft.Extensions.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Core.Memory;
using RedBird.Core.Memory.Scanners;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using RedBird.X64.Memory.Scanners;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using SHCDESE.Logging;
using System;
using System.Security;
using static Iced.Intel.AssemblerRegisters;
namespace SHCDESE.Detours;

/// <summary>
/// Bypass for mono stack size limit.
/// Made for Unity Engine v2021.3.45.8976527
/// Affects:
/// .text:00000001800CD826                 cmp     eax, 10000Fh
/// File Offset: #CCC26
/// Function: mono_class_layout_fields+1226
/// </summary>
internal class BulkMonoDetours
{
    private const int NEW_VALUE_TYPE_SIZE_CONSTRAINT = 0x40003C;
    public BulkMonoDetours(AobCacheOptions cache)
    {
        ILogger logger = BepInEx.Bootstrap.Plugin.Instance.LoggerFactory.CreateLogger("MonoDetours");

        IntPtr handle = MinWinAPI.LoadLibraryA("mono-2.0-bdwgc.dll");
        ScanRegion region = ScanRegion.FromModule(handle, "mono-2.0-bdwgc.dll");

        LogHelper.Information($"Applying, region={region.BaseAddress.ToString("X16")}, size={region.Span.Length.ToString("X16")}");

        DataScanner scanner = DataScanner.Create(region, logger, cache);

        //  Goodbye 1Mb restriction on value types on mono (jk you wont be missed)
        DataScanner valueTypeSizeConstraintScan = scanner.Scan("3D ? ? ? ? 76 ? 49 8B 4D ? 48 8D 15"); // cmp     eax, 10000Fh
        if (valueTypeSizeConstraintScan.Found)
        {
            LogHelper.Warning($"NUKING 1MB VALUE TYPE SIZE LIMIT FROM ORBIT");

            X64AssemblyPatch patch = new(valueTypeSizeConstraintScan.CurrentAddress, maxByteCount: 14, logger);
            patch.Generate(static (Assembler asm, UInt64 addressVA) =>
            {
                asm.cmp(eax, NEW_VALUE_TYPE_SIZE_CONSTRAINT);
            });
            patch.Enable();
        }
        else LogHelper.Fatal("1MB VALUE TYPE SIZE LIMIT NOT FOUND");

    }

}
