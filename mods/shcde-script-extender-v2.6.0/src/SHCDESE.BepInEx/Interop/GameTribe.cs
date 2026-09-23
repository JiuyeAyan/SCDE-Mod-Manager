using SHCDESE.Interop.Enums;
using SHCDESE.Lua.CodeGen;
using System;
using System.Runtime.InteropServices;

namespace SHCDESE.Interop;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct GameTribe : IEquatable<GameTribe>
{
	public UInt16 N000004BC; //0x0000
    [LuaExposed] public UInt16 r_PlayerIdOwner;
    public UInt32 N0000081C; //0x0004
    public UInt16 N000004BD; //0x0008
    [LuaExposed] public UInt32 r_GlobalId; //0x000C
    public UInt16 N00008645;
    public UInt32 N000004BE; //0x0010
    public UInt16 N00000819; //0x0014
    [LuaExposed] public AliveState r_AliveState; //0x0016
	public UInt16 N000004BF; //0x0018
    public UInt16 N00000591; //0x001A
    public UInt16 N00000594; //0x001C
    public UInt16 N00000592; //0x001E
    public UInt32 N000004C0; //0x0020
    public UInt16 N00000813; //0x0024
    public UInt16 N00000813_2; //0x0024
    public UInt32 N000004C1; //0x0028
    [LuaExposed] public Int16 r_TimeSinceLastOrder; //0x002C
    public UInt16 N00016150; // 0x002E
    [LuaExposed] public UInt16 r_LeaderUnitId; //0x0030
    [LuaExposed] public UInt16 r_UnitsInGroup; //0x0032
    public UInt16 r_UnitsInGroup2; //0x0034
    [LuaExposed] public UInt16 r_UnitIdsInGroupBitfield; //0x0036
    public UInt16 N000004C3; //0x0038
    public UInt16 N0000059F; //0x003A
    public UInt16 N000005A2; //0x003C
    public UInt16 N000005A0; //0x003E
    public UInt32 N000004C4; //0x0040
    public UInt32 N000005BE; //0x0044
    public UInt32 N000004C5; //0x0048
    public UInt32 N000005C0; //0x004C
    public UInt32 N000004C6; //0x0050
    public UInt32 N000005C2; //0x0054
    public UInt32 N000004C7; //0x0058
    public UInt32 N000005C4; //0x005C
    public UInt32 N000004C8; //0x0060
    public UInt32 N000005C6; //0x0064
    public UInt32 N000004C9; //0x0068
    public UInt32 N000005C8; //0x006C
    public UInt32 N000004CA; //0x0070
    public UInt32 N000005CA; //0x0074
    public UInt32 N000004CB; //0x0078
    public UInt32 N000005CC; //0x007C
    public UInt32 N000004CC; //0x0080
    public UInt32 N000005CE; //0x0084
    public UInt32 N000004CD; //0x0088
    public UInt32 N000005D0; //0x008C
    public UInt32 N000004CE; //0x0090
    public UInt32 N000005D2; //0x0094
    public UInt32 N000004CF; //0x0098
    public UInt32 N000005D4; //0x009C
    public UInt32 N000004D0; //0x00A0
    public UInt32 N000005D6; //0x00A4
    public UInt32 N000004D1; //0x00A8
    public UInt32 N000005D8; //0x00AC
    public UInt32 N000004D2; //0x00B0
    public UInt32 N000005DA; //0x00B4
    public UInt32 N000004D3; //0x00B8
    public UInt32 N000005DC; //0x00BC
    public UInt32 N000004D4; //0x00C0
    public UInt32 N000005DE; //0x00C4
    public UInt32 N000004D5; //0x00C8
    public UInt32 N000005E0; //0x00CC
    public UInt32 N000004D6; //0x00D0
    public UInt32 N000005E2; //0x00D4
    public UInt32 N000004D7; //0x00D8
    public UInt32 N000005E4; //0x00DC
    public UInt32 N000004D8; //0x00E0
    public UInt32 N000005E6; //0x00E4
    public UInt32 N000004D9; //0x00E8
    public UInt32 N000005E8; //0x00EC
    public UInt32 N000004DA; //0x00F0
    public UInt32 N000005EA; //0x00F4
    public UInt32 N000004DB; //0x00F8
    public UInt32 N000005EC; //0x00FC
    public UInt32 N000004DC; //0x0100
    public UInt32 N000005EE; //0x0104
    public UInt32 N000004DD; //0x0108
    public UInt32 N000005F0; //0x010C
    public UInt32 N000004DE; //0x0110
    public UInt32 N000005F2; //0x0114
    public UInt32 N000004DF; //0x0118
    public UInt32 N000005F4; //0x011C
    public UInt32 N000004E0; //0x0120
    public UInt32 N000005F6; //0x0124
    public UInt32 N000004E1; //0x0128
    public UInt32 N000005F8; //0x012C
    public UInt32 N000004E2; //0x0130
    public UInt32 N000005FA; //0x0134
    public UInt32 N000004E3; //0x0138
    public UInt32 N000005FC; //0x013C
    public UInt32 N000004E4; //0x0140
    public UInt32 N000005FE; //0x0144
    public UInt32 N000004E5; //0x0148
    public UInt32 N00000600; //0x014C
    public UInt32 N000004E6; //0x0150
    public UInt32 N00000602; //0x0154
    public UInt32 N000004E7; //0x0158
    public UInt32 N00000604; //0x015C
    public UInt32 N000004E8; //0x0160
    public UInt32 N00000606; //0x0164
    public UInt32 N000004E9; //0x0168
    public UInt32 N00000608; //0x016C
    public UInt32 N000004EA; //0x0170
    public UInt32 N0000060A; //0x0174
    public UInt32 N000004EB; //0x0178
    public UInt32 N0000060C; //0x017C
    public UInt32 N000004EC; //0x0180
    public UInt32 N0000060E; //0x0184
    public UInt32 N000004ED; //0x0188
    public UInt32 N00000610; //0x018C
    public UInt32 N000004EE; //0x0190
    public UInt32 N00000612; //0x0194
    public UInt32 N000004EF; //0x0198
    public UInt32 N00000614; //0x019C
    public UInt32 N000004F0; //0x01A0
    public UInt32 N00000616; //0x01A4
    public UInt32 N000004F1; //0x01A8
    public UInt32 N00000618; //0x01AC
    public UInt32 N000004F2; //0x01B0
    public UInt32 N0000061A; //0x01B4
    public UInt32 N000004F3; //0x01B8
    public UInt32 N0000061C; //0x01BC
    public UInt32 N000004F4; //0x01C0
    public UInt32 N0000061E; //0x01C4
    public UInt32 N000004F5; //0x01C8
    public UInt32 N00000620; //0x01CC
    public UInt32 N000004F6; //0x01D0
    public UInt16 N00000622; //0x01D4
    public UInt16 N00000622_2; //0x01D6
    public UInt32 N000004F7; //0x01D8
    public UInt32 N00000624; //0x01DC
    public UInt32 N000004F8; //0x01E0
    public UInt32 N00000626; //0x01E4
    public UInt32 N000004F9; //0x01E8
    public UInt32 N00000628; //0x01EC
    public UInt32 N000004FA; //0x01F0
    public UInt32 N0000062A; //0x01F4
    public UInt32 N000004FB; //0x01F8
    public UInt32 N0000062C; //0x01FC
    public UInt32 N000004FC; //0x0200
    public UInt32 N0000062E; //0x0204
    public UInt32 N000004FD; //0x0208
    public UInt32 N00000630; //0x020C
    public UInt32 N000004FE; //0x0210
    public UInt32 N00000632; //0x0214
    public UInt32 N000004FF; //0x0218
    public UInt32 N00000634; //0x021C
    public UInt32 N00000500; //0x0220
    public UInt32 N00000636; //0x0224
    public UInt32 N00000501; //0x0228
    public UInt32 N00000638; //0x022C
    public UInt32 N00000502; //0x0230
    public UInt32 N0000063A; //0x0234
    public UInt32 N00000503; //0x0238
    public UInt32 N0000063C; //0x023C
    public UInt32 N00000504; //0x0240
    public UInt32 N0000063E; //0x0244
    public UInt32 N00000505; //0x0248
    public UInt32 N00000640; //0x024C
    public UInt32 N00000506; //0x0250
    public UInt32 N00000642; //0x0254
    public UInt32 N00000507; //0x0258
    public UInt32 N00000644; //0x025C
    public UInt32 N00000508; //0x0260
    public UInt32 N00000646; //0x0264
    public UInt32 N00000509; //0x0268
    public UInt32 N00000648; //0x026C
    public UInt32 N0000050A; //0x0270
    public UInt32 N0000064A; //0x0274
    public UInt32 N0000050B; //0x0278
    public UInt32 N0000064C; //0x027C
    public UInt32 N0000050C; //0x0280
    public UInt32 N0000064E; //0x0284
    public UInt32 N0000050D; //0x0288
    public UInt32 N00000650; //0x028C
    public UInt32 N0000050E; //0x0290
    public UInt32 N00000652; //0x0294
    public UInt32 N0000050F; //0x0298
    public UInt32 N00000654; //0x029C
    public UInt32 N00000510; //0x02A0
    public UInt32 N00000656; //0x02A4
    public UInt32 N00000511; //0x02A8
    public UInt32 N00000658; //0x02AC
    public UInt32 N00000512; //0x02B0
    public UInt32 N0000065A; //0x02B4
    public UInt32 N00000513; //0x02B8
    public UInt32 N0000065C; //0x02BC
    public UInt32 N00000514; //0x02C0
    public UInt32 N0000065E; //0x02C4
    public UInt32 N00000515; //0x02C8
    public UInt32 N00000660; //0x02CC
    public UInt32 N00000516; //0x02D0
    public UInt32 N00000662; //0x02D4
    public UInt32 N00000517; //0x02D8
    public UInt32 N00000664; //0x02DC
    public UInt32 N00000518; //0x02E0
    public UInt32 N00000666; //0x02E4
    public UInt32 N00000519; //0x02E8
    public UInt32 N00000668; //0x02EC
    public UInt32 N0000051A; //0x02F0
    public UInt32 N0000066A; //0x02F4
    public UInt32 N0000051B; //0x02F8
    public UInt32 N0000066C; //0x02FC
    public UInt32 N0000051C; //0x0300
    public UInt32 N0000066E; //0x0304
    public UInt32 N0000051D; //0x0308
    public UInt32 N00000670; //0x030C
    public UInt32 N0000051E; //0x0310
    public UInt32 N00000672; //0x0314
    public UInt32 N0000051F; //0x0318
    public UInt32 N00000674; //0x031C
    public UInt32 N00000520; //0x0320
    public UInt32 N00000676; //0x0324
    public UInt32 N00000521; //0x0328
    public UInt32 N00000678; //0x032C
    public UInt32 N00000522; //0x0330
    public UInt32 N0000067A; //0x0334
    public UInt32 N00000523; //0x0338
    public UInt32 N0000067C; //0x033C
    public UInt32 N00000524; //0x0340
    public UInt32 N0000067E; //0x0344
    public UInt32 N00000525; //0x0348
    public UInt32 N00000680; //0x034C
    public UInt32 N00000526; //0x0350
    public UInt32 N00000682; //0x0354
    public UInt32 N00000527; //0x0358
    public UInt32 N00000684; //0x035C
    public UInt32 N00000528; //0x0360
    public UInt32 N00000686; //0x0364
    public UInt32 N00000529; //0x0368
    public UInt32 N00000688; //0x036C
    public UInt32 N0000052A; //0x0370
    public UInt32 N0000068A; //0x0374
    public UInt32 N0000052B; //0x0378
    public UInt32 N0000068C; //0x037C
    public UInt32 N0000052C; //0x0380
    public UInt32 N0000068E; //0x0384
    public UInt32 N0000052D; //0x0388
    public UInt32 N00000690; //0x038C
    public UInt32 N0000052E; //0x0390
    public UInt32 N00000692; //0x0394
    public UInt32 N0000052F; //0x0398
    public UInt32 N00000694; //0x039C
    public UInt32 N00000530; //0x03A0
    public UInt32 N00000696; //0x03A4
    public UInt32 N00000531; //0x03A8
    public UInt32 N00000698; //0x03AC
    public UInt32 N00000532; //0x03B0
    public UInt32 N0000069A; //0x03B4
    public UInt32 N00000533; //0x03B8
    public UInt32 N0000069C; //0x03BC
    public UInt32 N00000534; //0x03C0
    public UInt32 N0000069E; //0x03C4
    public UInt32 N00000535; //0x03C8
    public UInt32 N000006A0; //0x03CC
    public UInt32 N00000536; //0x03D0
    public UInt32 N000006A2; //0x03D4
    public UInt32 N00000537; //0x03D8
    public UInt32 N000006A4; //0x03DC
    public UInt32 N00000538; //0x03E0
    public UInt32 N000006A6; //0x03E4
    public UInt32 N00000539; //0x03E8
    public UInt32 N000006A8; //0x03EC
    public UInt32 N0000053A; //0x03F0
    public UInt32 N000006AA; //0x03F4
    public UInt32 N0000053B; //0x03F8
    public UInt32 N000006AC; //0x03FC
    public UInt32 N0000053C; //0x0400
    public UInt32 N000006AE; //0x0404
    public UInt32 N0000053D; //0x0408
    public UInt32 N000006B0; //0x040C
    public UInt32 N0000053E; //0x0410
    public UInt32 N000006B2; //0x0414
    public UInt32 N0000053F; //0x0418
    public UInt32 N000006B4; //0x041C
    public UInt32 N00000540; //0x0420
    public UInt32 N000006B6; //0x0424
    public UInt32 N00000541; //0x0428
    public UInt32 N000006B8; //0x042C
    public UInt32 N00000542; //0x0430
    public UInt32 N000006BA; //0x0434
    public UInt32 N00000543; //0x0438
    public UInt32 N000006BC; //0x043C
    public UInt32 N00000544; //0x0440
    public UInt32 N000006BE; //0x0444
    public UInt32 N00000545; //0x0448
    public UInt32 N000006C0; //0x044C
    public UInt32 N00000546; //0x0450
    public UInt32 N000006C2; //0x0454
    public UInt32 N00000547; //0x0458
    public UInt32 N000006C4; //0x045C
    public UInt32 N00000548; //0x0460
    public UInt32 N000006C6; //0x0464
    public UInt32 N00000549; //0x0468
    public UInt32 N000006C8; //0x046C
    public UInt32 N0000054A; //0x0470
    public UInt32 N000006CA; //0x0474
    public UInt32 N0000054B; //0x0478
    public UInt32 N000006CC; //0x047C
    public UInt32 N0000054C; //0x0480
    public UInt32 N000006CE; //0x0484
    public UInt32 N0000054D; //0x0488
    public UInt32 N000006D0; //0x048C
    public UInt32 N0000054E; //0x0490
    public UInt32 N000006D2; //0x0494
    public UInt32 N0000054F; //0x0498
    public UInt32 N000006D4; //0x049C
    public UInt32 N00000550; //0x04A0
    public UInt32 N000006D6; //0x04A4
    public UInt32 N00000551; //0x04A8
    public UInt32 N000006D8; //0x04AC
    public UInt32 N00000552; //0x04B0
    public UInt32 N000006DA; //0x04B4
    public UInt32 N00000553; //0x04B8
    public UInt32 N000006DC; //0x04BC
    public UInt32 N00000554; //0x04C0
    public UInt32 N000006DE; //0x04C4
    public UInt32 N00000555; //0x04C8
    public UInt32 N000006E0; //0x04CC
    public UInt32 N00000556; //0x04D0
    public UInt32 N000006E2; //0x04D4
    public UInt32 N00000557; //0x04D8
    public UInt32 N000006E4; //0x04DC
    public UInt32 N00000558; //0x04E0
    public UInt32 N000006E6; //0x04E4
    public UInt32 N00000559; //0x04E8
    public UInt32 N000006E8; //0x04EC
    public UInt32 HeightElevation; //0x04F0
    public UInt32 N000006EA; //0x04F4
    public UInt32 N0000055B; //0x04F8
    public UInt32 N000006EC; //0x04FC
    public UInt32 N0000055C; //0x0500
    public UInt32 N000006EE; //0x0504
    public UInt32 N0000055D; //0x0508
    public UInt32 N000006F0; //0x050C
    public UInt32 N0000055E; //0x0510
    public UInt32 N000006F2; //0x0514
    public UInt32 N0000055F; //0x0518
    public UInt32 N000006F4; //0x051C
    public UInt32 N00000560; //0x0520
    public UInt32 N000006F6; //0x0524
    public UInt32 N00000561; //0x0528
    public UInt32 N000006F8; //0x052C
    public UInt32 N00000562; //0x0530
    public UInt32 N000006FA; //0x0534
    public UInt32 N00000563; //0x0538
    public UInt32 N000006FC; //0x053C
    public UInt32 N00000564; //0x0540
    public UInt32 N000006FE; //0x0544
    public UInt32 N00000565; //0x0548
    public UInt32 N00000700; //0x054C
    public UInt32 N00000566; //0x0550
    public UInt32 N00000702; //0x0554
    [LuaExposed] public TribePatrolMode r_PatrolMode; //0x0558
    public UInt16 N00011844; //0x055A
    public UInt32 N00000704; //0x055C
    public UInt32 N00000568; //0x0560
    public UInt32 N00000706; //0x0564
    public UInt32 N00000569; //0x0568
    public UInt32 N00000708; //0x056C
    public UInt32 N0000056A; //0x0570
    public UInt32 N0000070A; //0x0574
    public UInt32 N0000056B; //0x0578
    [LuaExposed] public UInt32 r_AttackStanceTicker; //0x057C
    public UInt32 N0000056C; //0x0580
    public UInt32 N000007B3; //0x0584
    public UInt16 N0000056D; //0x0588
    [LuaExposed] public UInt16 r_PatrolPoint1TileX; //0x058A
    [LuaExposed] public UInt16 r_PatrolPoint1TileY; //0x058C
    [LuaExposed] public UInt16 r_PatrolPoint2TileX; //0x058E
    [LuaExposed] public UInt16 r_PatrolPoint2TileY; //0x0590
    [LuaExposed] public UInt16 r_PatrolPoint3TileX; //0x0592
    [LuaExposed] public UInt16 r_PatrolPoint3TileY; //0x0594
    [LuaExposed] public UInt16 r_PatrolPoint4TileX; //0x0596
    [LuaExposed] public UInt16 r_PatrolPoint4TileY; //0x0598
    [LuaExposed] public UInt16 r_PatrolPoint5TileX; //0x059A
    [LuaExposed] public UInt16 r_PatrolPoint5TileY; //0x059C
    [LuaExposed] public UInt16 r_PatrolPoint6TileX; //0x059E
    [LuaExposed] public UInt16 r_PatrolPoint6TileY; //0x05A0
    [LuaExposed] public UInt16 r_PatrolPoint7TileX; //0x05A2
    [LuaExposed] public UInt16 r_PatrolPoint7TileY; //0x05A4
    [LuaExposed] public UInt16 r_PatrolPoint8TileX; //0x05A6
    [LuaExposed] public UInt16 r_PatrolPoint8TileY; //0x05A8
    [LuaExposed] public UInt16 r_PatrolPoint9TileX; //0x05AA
    [LuaExposed] public UInt16 r_PatrolPoint9TileY; //0x05AC
    [LuaExposed] public UInt16 r_PatrolPoint10TileX; //0x05AE
    [LuaExposed] public UInt16 r_PatrolPoint10TileY; //0x05B0
    [LuaExposed] public UInt16 r_PatrolCurrentTargetIndex; //0x05B2
    [LuaExposed] public UInt32 r_CurrentPatrolPoints; //0x05B4
    public UInt32 N00000573; //0x05B8
    public UInt32 N000007C1; //0x05BC
    public UInt32 N00000574; //0x05C0
    public UInt32 N000007C3; //0x05C4
    public UInt32 N00000575; //0x05C8
    public UInt32 N000007C5; //0x05CC
    public UInt32 N00000576; //0x05D0
    public UInt16 N000005A9; //0x05D4
    public UInt16 N000005AB; //0x05D6
    public UInt32 N00000577; //0x05D8
    public UInt16 N000007D1; //0x05DC
    [LuaExposed] public UInt16 r_bAttackNearestUnit; //0x05DE
    [LuaExposed] public UInt16 r_TicksFrom100WhenRangeAttacked; //0x05E0
    [LuaExposed] public UInt16 r_TrackedRangedAttackersCount; //0x05E0
    public UInt32 N000007D3; //0x05E4
    public UInt16 N00000579; //0x05E8
    public UInt16 is100whenwalking; //0x05EA
    public UInt32 N000007D5; //0x05EC
    public UInt32 N0000057A; //0x05F0
    public UInt32 N000007D7; //0x05F4
    public UInt32 N0000057B; //0x05F8
    public UInt32 N000007D9; //0x05FC
    public UInt32 N0000057C; //0x0600
    public UInt32 N000007DB; //0x0604
    public UInt16 N0000057D; //0x0608
    [LuaExposed] public TribeStance r_TribeStance; //0x060A
    public UInt32 N000007DD; //0x060C
    public UInt16 p_AttackTargetDistanceRelated; //0x0610
    [LuaExposed] public UInt16 r_AttackTargetUnitId; //0x0612
    public UInt16 N000007DF; //0x0614
    [LuaExposed] public UInt32 r_UnkRefGlobalId; //0x0616
    public UInt16 N00005DC0; //0x061A
    [LuaExposed] public UInt32 r_AttackTargetOwnerPlayerId; //0x061C
    public UInt32 N00000580; //0x0620
    public UInt32 N000007E3; //0x0624
    public UInt16 unk10; //0x0628
    [LuaExposed] public UInt16 r_LastRangedAttackerGlobalId10; //0x062A
    public UInt16 unk1; //0x062C
    [LuaExposed] public UInt16 r_LastRangedAttackerGlobalId1; //0x062E
    public UInt16 unk2; //0x0630
    [LuaExposed] public UInt16 r_LastRangedAttackerGlobalId2; //0x0632
    public UInt16 unk3; //0x0634
    [LuaExposed] public UInt16 r_LastRangedAttackerGlobalId3; //0x0636
    public UInt16 unk4; //0x0638
    [LuaExposed] public UInt16 r_LastRangedAttackerGlobalId4; //0x063A
    public UInt16 unk5; //0x063C
    [LuaExposed] public UInt16 r_LastRangedAttackerGlobalId5; //0x063E
    public UInt16 unk6; //0x0640
    [LuaExposed] public UInt16 r_LastRangedAttackerGlobalId6; //0x0642
    public UInt16 unk7; //0x0644
    [LuaExposed] public UInt16 r_LastRangedAttackerGlobalId7; //0x0646
    public UInt16 unk8; //0x0648
    [LuaExposed] public UInt16 r_LastRangedAttackerGlobalId8; //0x064A
    public UInt16 unk9; //0x064C
    [LuaExposed] public UInt16 r_LastRangedAttackerGlobalId9; //0x064E
    public UInt32 N00000586; //0x0650
    public UInt32 N000007EF; //0x0654
    public UInt32 N00000587; //0x0658
    public UInt32 N000007F1; //0x065C
    public UInt32 N00000588; //0x0660
    public UInt32 N000007F3; //0x0664
    public UInt32 N00000589; //0x0668
    public UInt32 N000007F5; //0x066C
    public UInt32 N0000058A; //0x0670
    public UInt32 N000007F7; //0x0674
    public UInt32 N0000058B; //0x0678
    public UInt32 N000007F9; //0x067C
    public UInt32 N0000058C; //0x0680
    public UInt32 N00000820; //0x0684

    public bool Equals(GameTribe other)
    {
        return this.r_GlobalId == other.r_GlobalId;
    }
}; //Size: 0x0688
