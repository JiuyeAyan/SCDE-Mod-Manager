using SHCDESE.Lua.CodeGen;
using System;
using System.Runtime.InteropServices;

namespace SHCDESE.Interop;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct GameUnitManager
{
    /// <summary>Native unit IDs are stored in the range 0..9999; zero is reserved.</summary>
    public const int NativeUnitSlotCount = 10_000;
    public const int FirstLiveUnitId = 1;
    public const int LastLiveUnitId = NativeUnitSlotCount - 1;

    /// <summary>Size of one native <see cref="GameUnit"/> record.</summary>
    public const int UnitRecordByteSize = 0x490;

    /// <summary>
    /// Offset of the separate packed path-plan block from the start of GameUnitManager.
    /// This block is intentionally accessed through offsets instead of being embedded in this struct.
    /// </summary>
    public const int PackedPathPlanBlockOffset = 0xB4FE78;
    public const int PackedPathPlanBytesPerUnit = 1_000;
    public const int PackedPathPlanTransitionsPerUnit = PackedPathPlanBytesPerUnit * 2;
    public const int PackedPathPlanBlockByteSize = NativeUnitSlotCount * PackedPathPlanBytesPerUnit;

    public UInt32 r_NextUnitId; //0x0000
    public UInt32 r_TotalUnits; //0x0004
    public UInt32 N000003DC; //0x0008
    public UInt32 N00002C1F; //0x000C
    public UInt32 N000003DD; //0x0010
    public UInt32 N00002C21; //0x0014
    public UInt32 N000003DE; //0x0018
    public UInt32 N00002C23; //0x001C
    public UInt32 r_HoveredChimpsCount; //0x0020
    public UInt32 N00002C25; //0x0024
    public UInt32 r_SelectedChimpsCount; //0x0028
    public UInt32 N00002C27; //0x002C
    public UInt32 N000003E1; //0x0030
    public UInt32 N00002C29; //0x0034
    public UInt32 N000003E2; //0x0038
    public UInt32 N00002C2B; //0x003C
    public UInt32 N000003E3; //0x0040
    public UInt32 N00002C2D; //0x0044
    public UInt32 N000003E4; //0x0048
    public UInt32 N00002C2F; //0x004C
    public UInt32 N000003E5; //0x0050
    public UInt32 N00002C31; //0x0054
    public UInt32 N000003E6; //0x0058
    public UInt32 N00002C33; //0x005C
    public UInt32 N000003E7; //0x0060
    public UInt32 N00002C35; //0x0064
    public UInt32 N000003E8; //0x0068
    public UInt32 N00002C37; //0x006C
    public UInt32 N000003E9; //0x0070
    public UInt32 N00002C39; //0x0074
    public UInt32 N000003EA; //0x0078
    public UInt32 N00002C3B; //0x007C
    public UInt32 N000003EB; //0x0080
    public UInt32 N000091DA; //0x0084
    public UInt32 N000003EC; //0x0088
    public UInt32 N000091DD; //0x008C
    public UInt32 N000003ED; //0x0090
    public UInt32 N000091E0; //0x0094
    public fixed byte pad_0098[1224]; //0x0098
    public UInt32 r_CurrentSelectedUnitId; //0x0560
    public UInt32 r_SelectedArchersAmount; //0x0564
    public UInt32 r_SelectedSpearmanAmount; //0x0568
    public UInt32 r_SelectedMacemanAmount; //0x056C
    public UInt32 r_SelectedCrossbowAmount; //0x0570
    public UInt32 r_SelectedPikemanAmount; //0x0574
    public UInt32 r_SelectedSwordsmanAmount; //0x0578
    public UInt32 r_SelectedKnightAmount; //0x057C
    public UInt32 r_SelectedEngineersAmount; //0x0580
    public UInt32 r_SelectedLaddermanAmount; //0x0584
    public UInt32 r_SelectedTunnelerAmount; //0x0588
    public UInt32 r_SelectedMonkAmount; //0x058C
    public UInt32 r_SelectedCatapultAmount; //0x0590
    public UInt32 r_SelectedTrebuchetAmount; //0x0594
    public UInt32 r_SelectedBatteringRamAmount; //0x0598
    public UInt32 r_SelectedSiegeTowerAmount; //0x059C
    public UInt32 r_SelectedPortableShieldsAmount; //0x05A0
    public UInt32 r_SelectedStationaryCatapultAmount; //0x05A4
    public UInt32 r_SelectedStationaryBallistaAmount; //0x05A8
    public UInt32 r_SelectedUnknownAmount; //0x05AC
    public UInt32 r_SelectedArabBowAmount; //0x05B0
    public UInt32 r_SelectedArabSlaveAmount; //0x05B4
    public UInt32 r_SelectedArabSlingerAmount; //0x05B8
    public UInt32 r_SelectedAssasinAmount; //0x05BC
    public UInt32 r_SelectedArabHorseBowAmount; //0x05C0
    public UInt32 r_SelectedArabSwordsmanAmount; //0x05C4
    public UInt32 r_SelectedArabFirethrowerAmount; //0x05C8
    public UInt32 r_SelectedArabFireBallistaAmount; //0x05CC
    public UInt32 r_SelectedBedouinCamelLancerAmount; //0x05D0
    public UInt32 r_SelectedBedouinHealerAmount; //0x05D4
    public UInt32 r_SelectedBedouinEunuchAmount; //0x05D8
    public UInt32 r_SelectedBedouinAmbusherAmount; //0x05DC
    public UInt32 r_SelectedBedouinSkirmisherAmount; //0x05E0
    public UInt32 r_SelectedBedouinHeavyCamelAmount; //0x05E4
    public UInt32 r_SelectedBedouinSapperAmount; //0x05E8
    public UInt32 r_SelectedBedouinDemolisherAmount; //0x05EC
    public UInt32 N00000499; //0x05F0
    public UInt32 N00009283; //0x05F4
    public UInt32 N0000049A; //0x05F8
    public UInt32 N00009285; //0x05FC
    public UInt32 N0000049B; //0x0600
    public UInt32 N00009287; //0x0604
    public UInt32 N0000049C; //0x0608
    public UInt32 N00009289; //0x060C
    public UInt32 N0000049D; //0x0610
    public UInt32 N0000928B; //0x0614
    public UInt32 N0000049E; //0x0618
    public UInt32 N0000928D; //0x061C
    public UInt32 N0000049F; //0x0620
    public UInt32 N0000928F; //0x0624
    public UInt32 N000004A0; //0x0628
    public UInt32 N00009291; //0x062C
    public UInt32 N000004A1; //0x0630
    public UInt32 N00009293; //0x0634
    public UInt32 N000004A2; //0x0638
    public UInt32 N00009295; //0x063C
    public UInt32 N000004A3; //0x0640
    public UInt32 N00009297; //0x0644
    public UInt32 N000004A4; //0x0648
    public UInt32 N00009299; //0x064C

    public Int32 r_RecruitmentResultFailureReason; //0x0650
    public Int32 r_RecruitmentResultMissingGoodId; //0x0654
    public UInt32 EmptyUnitFillValue; //0x0658

    public GameUnit GameUnitArray; // [0] = LastOrderedUnit, Capacity: 10000
    // UNKNOWN SECTION: 44999 * 4
    // PackedPathPlans: 10000 * 1000
}