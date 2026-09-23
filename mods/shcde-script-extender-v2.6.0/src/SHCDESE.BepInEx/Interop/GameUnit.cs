using SHCDESE.API.Components.Spatial;
using SHCDESE.Interop.Enums;
using SHCDESE.Lua.CodeGen;
using System;
using System.Runtime.InteropServices;

namespace SHCDESE.Interop;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct GameUnit : IPositionable, IEquatable<GameUnit>
{
    [LuaExposed] public UInt32 r_AnimationFrame; //0x0000
    [LuaExposed] public UInt32 r_SpriteAnimationGroup; //0x0004
    [LuaExposed] public GM r_GameMaterialIndex; //0x0008
	public UInt16 N0000542F; //0x000A
    [LuaExposed] public UInt32 r_SpritePlayerColorId; //0x000C
    public UInt16 N00000055; //0x0010
    public UInt16 N000002FB; //0x0012
    public UInt32 N000000F8; //0x0014
    public UInt16 N00000056; //0x0018
    public UInt16 N000002F7; //0x001A
    public UInt16 N000000FA; //0x001C
    public UInt16 N00000301; //0x001E
    public UInt32 N00000057; //0x0020
    public UInt32 N000000FC; //0x0024
    public UInt32 N00000058; //0x0028
    public UInt32 N000000FE; //0x002C
    [LuaExposed] public UInt32 r_UnitSelected; //0x0030
    [LuaExposed] public UInt32 r_HealthBarBlocks; //0x0034 (Usually r_CurrentHealthPercentage / 10)
    [LuaExposed] public UInt32 r_TicksAlive1; //0x0038
    public UInt32 r_TicksAlive2; //0x003C
    public UInt32 N0000005B; //0x0040
    [LuaExposed] public UInt32 r_CurrentSpriteAnimationFrame; //0x0044
    public UInt32 N0000005C; //0x0048
    public UInt32 N00000106; //0x004C
    [LuaExposed] public Dircs r_Direction; //0x0050
    public UInt16 Unknown;
	public UInt32 N00000108; //0x0054
    public UInt32 N0000005E; //0x0058
    public UInt32 N0000010A; //0x005C
    public UInt32 N0000005F; //0x0060
    [LuaExposed] public UInt32 r_IsInvisible; //0x0064
    public UInt32 N00000060; //0x0068
    public UInt32 N0000010E; //0x006C
    public UInt32 N00000061; //0x0070
    public UInt32 N00000110; //0x0074
    public UInt32 N00000062; //0x0078
    [LuaExposed] public UInt32 r_SpawnedForPlayerIndex; //0x007C
    public UInt32 N00000063; //0x0080
    public UInt32 N00000114; //0x0084
    [LuaExposed] public AliveState r_AliveState; //0x0088
    [LuaExposed] public eChimps r_UnitChimp; //0x008A
	public UInt32 N00000116; //0x008C
    public byte N00000573; //0x0090
    public byte N00000568; //0x0091
    [LuaExposed] public byte r_ControllableForPlayerId; //0x0092
    public byte N00000569; //0x0093
    [LuaExposed] public UInt32 r_GlobalId; //0x0094
    public UInt32 N00000066; //0x0098
    public UInt32 N0000011A; //0x009C
    public UInt32 N00000067; //0x00A0
    [LuaExposed] public UInt32 r_WorkerTargetContextEntityGlobalId; //0x00A4
    public UInt32 N00000068; //0x00A8
    public UInt16 N0000011E; //0x00AC
    public UInt16 r_UnitSelected2; //0x00AE
    public UInt16 N00000069; //0x00B0
    [LuaExposed] public UInt16 r_CurrentWorldPositionX; //0x00B2
    [LuaExposed] public UInt16 r_CurrentWorldPositionY; //0x00B4
    [LuaExposed] public UInt16 r_HeightElevation; //0x00B6
    public Int16 N0000006A;
    [LuaExposed] public UInt16 r_LookAtWorldPositionX;
    [LuaExposed] public UInt16 r_LookAtWorldPositionY; //0x00BC
    [LuaExposed] public UInt16 r_LookAtHeight;
    [LuaExposed] public UInt16 r_CurrentTilePositionX; //0x00C0
    [LuaExposed] public UInt16 r_CurrentTilePositionY; //0x00C2
    [LuaExposed] public UInt16 r_TargetTilePositionX; //0x00C4
    [LuaExposed] public UInt16 r_TargetTilePositionY; //0x00C6
    [LuaExposed] public UInt16 r_PreviousTilePositionX; //0x00C8
    [LuaExposed] public UInt16 r_PreviousTilePositionY; //0x00CA
    public UInt32 N00000126; //0x00CC
    [LuaExposed] public UInt32 r_CurrentPositionTileId; //0x00D0
    [LuaExposed] public UInt32 r_TargetPositionTileId; //0x00D4
    [LuaExposed] public UInt32 r_PreviousPositionTileId; //0x00D8
    [LuaExposed] public UInt16 r_NextTilePositionX2; //0x00DC
    [LuaExposed] public UInt16 r_NextTilePositionY2; //0x00DE
    [LuaExposed] public UInt32 r_NextPositionTileId2; //0x00E0
    public UInt32 N0000012C; //0x00E4
    [LuaExposed] public UInt16 r_TargetTilePositionX2; //0x00E8
    [LuaExposed] public UInt16 r_TargetTilePositionY2; //0x00EA
    public UInt32 N0000012E; //0x00EC
    public UInt16 r_PathPlanRelated1; //0x00F0
    [LuaExposed] public UInt16 r_PathPlanStateBitFlags; // Seems to be "2" while moving, perhaps something like PathState? Seems to be BitFlags (see: c_game_unit_issueorder_movehere)
    [LuaExposed] public UInt16 r_MovementSubstep; //0x00F4
    [LuaExposed] public UInt16 r_CurrentPathPlanIndex; //0x00F6
    [LuaExposed] public UInt16 r_PathPlanLength; //0x00F8
    public UInt16 N00009255;
    public UInt32 N00000132; //0x00FC
    public UInt32 N00000073; //0x0100
    public UInt32 N00000134; //0x0104
    public UInt32 N00000074; //0x0108
    public UInt32 N00000136; //0x010C
    public UInt32 N00000075; //0x0110
    public UInt32 N00000138; //0x0114
    public UInt32 N00000076; //0x0118
    public UInt32 N0000013A; //0x011C
    public UInt32 N00000077; //0x0120
    public UInt32 N0000013C; //0x0124
    public UInt32 N00000078; //0x0128
    public UInt32 N0000013E; //0x012C
    public UInt32 N00000079; //0x0130
    public UInt32 N00000140; //0x0134
    public UInt32 N0000007A; //0x0138
    public UInt32 N00000142; //0x013C
    public UInt32 N0000007B; //0x0140
    public UInt32 N00000144; //0x0144
    public UInt32 N0000007C; //0x0148
    public UInt32 N00000146; //0x014C
    public UInt32 N0000007D; //0x0150
    public UInt32 N00000148; //0x0154
    public UInt32 N0000007E; //0x0158
    public UInt32 N0000014A; //0x015C
    public UInt32 N0000007F; //0x0160
    public UInt32 N0000014C; //0x0164
    public UInt32 N00000080; //0x0168
    public UInt32 N0000014E; //0x016C
    public UInt32 N00000081; //0x0170
    public UInt32 N00000150; //0x0174
    public UInt32 N00000082; //0x0178
    public UInt32 N00000152; //0x017C
    public UInt32 N00000083; //0x0180
    public UInt32 N00000154; //0x0184
    public UInt32 N00000084; //0x0188
    public UInt32 N00000156; //0x018C
    public UInt32 N00000085; //0x0190
    public UInt32 N00000158; //0x0194
    public UInt32 N00000086; //0x0198
    public UInt32 N0000015A; //0x019C
    public UInt32 N00000087; //0x01A0
    public UInt32 N0000015C; //0x01A4
    public UInt32 N00000088; //0x01A8
    public UInt32 N0000015E; //0x01AC
    public UInt32 N00000089; //0x01B0
    public UInt32 N00000160; //0x01B4
    public UInt32 N0000008A; //0x01B8
    public UInt32 N00000162; //0x01BC
    public UInt32 N0000008B; //0x01C0
    public UInt32 N00000164; //0x01C4
    public UInt32 N0000008C; //0x01C8
    public UInt32 N00000166; //0x01CC
    public UInt32 N0000008D; //0x01D0
    public UInt32 N00000168; //0x01D4
    public UInt32 N0000008E; //0x01D8
    public UInt32 N0000016A; //0x01DC
    public UInt32 N0000008F; //0x01E0
    public UInt32 N0000016C; //0x01E4
    public UInt32 N00000090; //0x01E8
    public UInt32 N0000016E; //0x01EC
    public UInt32 N00000091; //0x01F0
    public UInt32 N00000170; //0x01F4
    public UInt32 N00000092; //0x01F8
    public UInt32 N00000172; //0x01FC
    public UInt32 N00000093; //0x0200
    public UInt32 N00000174; //0x0204
    public UInt32 N00000094; //0x0208
    public UInt32 N00000176; //0x020C
    public UInt32 N00000095; //0x0210
    public UInt32 N00000178; //0x0214
    public UInt32 N00000096; //0x0218
    public UInt32 N0000017A; //0x021C
    public UInt32 N00000097; //0x0220
    public UInt32 N0000017C; //0x0224
    public UInt32 N00000098; //0x0228
    public UInt32 N0000017E; //0x022C
    public UInt32 N00000099; //0x0230
    public UInt32 N00000180; //0x0234
    public UInt32 N0000009A; //0x0238
    public UInt32 N00000182; //0x023C
    public UInt32 N0000009B; //0x0240
    public UInt32 N00000184; //0x0244
    public UInt32 N0000009C; //0x0248
    public UInt32 N00000186; //0x024C
    public UInt32 N0000009D; //0x0250
    public UInt32 N00000188; //0x0254
    public UInt32 N0000009E; //0x0258
    public UInt32 N0000018A; //0x025C
    public UInt32 N0000009F; //0x0260
    public UInt32 N0000018C; //0x0264
    public UInt32 N000000A0; //0x0268
    public UInt32 N0000018E; //0x026C
    public UInt32 N000000A1; //0x0270
    public UInt32 N00000190; //0x0274
    public UInt32 N000000A2; //0x0278
    public UInt32 N00000192; //0x027C
    public UInt32 N000000A3; //0x0280
    public UInt32 N00000194; //0x0284
    public UInt32 N000000A4; //0x0288
    public UInt32 N00000196; //0x028C
    [LuaExposed] public UInt16 r_SelectedConnectionRecordId; //0x0290
    public UInt16 N000000A5_2; //0x0290
    [LuaExposed] public UInt32 r_SelectedConnectionRecordGlobalId; //0x0294
    public UInt16 N000000A6; //0x0298
    public UInt16 N000000A6_2; //0x029A
    public UInt32 N0000019A; //0x029C
    public UInt16 N000000A7; //0x02A0
    public UInt16 N00000305; //0x02A2
    [LuaExposed] public UInt32 r_InitialNextOrTargetPCLId; //0x02A4
    public UInt32 N000000A8; //0x02A8
    [LuaExposed] public UInt32 r_AnimationTimer; //0x02AC
    [LuaExposed] public Dircs r_MovementDirection; //0x02B0
    public UInt16 N000001A0_1;
    public UInt32 N000001A0; //0x02B4
    public UInt16 N000000AA; //0x02B8
    [LuaExposed] public UInt16 r_SpeedBonus; //0x02B8
    [LuaExposed] public UInt16 r_AIState; //0x02BC
    public UInt16 N0000924D;
    public UInt32 N000000AB; //0x02C0
    [LuaExposed] public UInt16 r_TimeSinceDeathTicker;
    [LuaExposed] public eChimps r_TransformIntoUnitOfType; //0x02C4
    public UInt32 N000000AC; //0x02C8
    public UInt32 N000001A6; //0x02CC
    [LuaExposed] public UInt16 r_CurrentHealthPercentage; //0x02D0
    [LuaExposed] public UInt16 r_TribeLeaderUnitId; //0x02D2
    [LuaExposed] public UInt16 r_TribeId; //0x02D4
    public UInt16 UnknownAIFlag; //0x02D6
    [LuaExposed] public UInt16 r_AttackMoveToTargetTileX; //0x02D8
    [LuaExposed] public UInt16 r_AttackMoveToTargetTileY; //0x02DA
    public UInt32 N000001AA; //0x02DC
    public UInt32 N000000AF; //0x02E0
    public UInt32 N000001AC; //0x02E4
    public UInt32 N000000B0; //0x02E8
    public UInt32 N000001AE; //0x02EC
    public UInt32 N000000B1; //0x02F0
    public UInt32 N000001B0; //0x02F4
    public UInt32 N000000B2; //0x02F8
    public UInt32 N000001B2; //0x02FC
    [LuaExposed] public UInt16 r_CarryOverGoodsAmount; //0x0300
    [LuaExposed] public UInt16 r_CarryBonusYieldAmount; //0x0300
    public UInt32 N000001B4; //0x0304
    public UInt32 N000000B4; //0x0308
    public UInt32 N000001B6; //0x030C
    [LuaExposed] public UInt16 r_AssignedEngineer1; //0x0310
    [LuaExposed] public UInt16 r_AssignedEngineer2; //0x0310
    [LuaExposed] public UInt16 r_AssignedEngineer3; //0x0314
    [LuaExposed] public UInt16 r_AssignedEngineer4; //0x0314
    [LuaExposed] public UInt32 r_AssignedEngineer1GlobalId; //0x0318
    [LuaExposed] public UInt32 r_AssignedEngineer2GlobalId; //0x031C
    [LuaExposed] public UInt32 r_AssignedEngineer3GlobalId; //0x0320
    [LuaExposed] public UInt32 r_AssignedEngineer4GlobalId; //0x0324
    public UInt16 N000000B8; //0x0328
    public UInt16 UnknownRelevant2; //0x032A
    public UInt32 N000001BE; //0x032C
    public UInt32 N000000B9; //0x0330
    [LuaExposed] public UInt16 r_LinkedProductionBuildingId; //0x0334
    [LuaExposed] public UInt16 r_LinkedProductionBuildingTileEndX;
    [LuaExposed] public UInt16 r_LinkedProductionBuildingTileEndY; //0x0338
    [LuaExposed] public UInt16 r_AttackingUnitId; //0x033A
    public UInt32 N000001C2; //0x033C
    [LuaExposed] public UInt32 r_RangedAttackTargetUnitId; //0x0340
    public UInt16 N000001C4;
    public UInt16 r_CurrentSpeed2;
    [LuaExposed] public UInt16 r_CurrentSpeed;
    public UInt16 N00008635;
    [LuaExposed] public UInt32 r_AliveTicks1; //0x034C
    public UInt32 r_AliveTicks2; //0x0350
    public UInt16 r_SelectionRelevant3; //0x0354
    public UInt16 N0000924A; //0x0354
    public UInt32 N000000BE; //0x0358
    [LuaExposed] public UInt16 r_PathConnectionMode; //0x035C
    [LuaExposed] public byte r_StoneAmmoLeft;
    [LuaExposed] public byte r_StoneAmmoStacksLeft;
    public UInt32 N000000BF; //0x0360
    [LuaExposed] public UInt32 r_LinkedProductionBuildingGlobalId; //0x0364
    public UInt32 N000000C0; //0x0368
    public byte N000001CE; //0x036C
    public byte UnknownRelevant1; //0x036D
    public UInt16 N0000026E; //0x036E
    public UInt32 Unknown2; //0x0370
    public UInt32 N000001D0; //0x0374
    public UInt32 N000000C2; //0x0378
    public UInt32 TimeUntilResting4thBit; //0x037C
    public UInt16 TimeUntilResting; //0x0380
    public UInt16 N0000026B; //0x0382
    public UInt32 N000001D4; //0x0384
    public UInt32 N000000C4; //0x0388
    public UInt32 N000001D6; //0x038C
    public UInt32 N000000C5; //0x0390
    public UInt32 N000001D8; //0x0394
    [LuaExposed] public UInt16 r_AI_LastIssuedTribeCommand; //0x0398
    [LuaExposed] public UInt16 r_AI_ContextTargetUnitId; //0x039C
    [LuaExposed] public UInt32 r_AI_ContextTargetUnitGlobalId; //0x03A0
    public UInt32 N000000C7; //0x03A0
    [LuaExposed] public UInt32 r_AI_ContextTargetBuildingTileId; //0x03A4
    public UInt32 N000000C8; //0x03A8
    public UInt32 N000001DE; //0x03AC
    public UInt32 N000000C9; //0x03B0
    public UInt32 N000001E0; //0x03B4
    public UInt32 N000000CA; //0x03B8
    public UInt32 N000001E2; //0x03BC
    public UInt32 N000000CB; //0x03C0
    [LuaExposed] public UInt32 r_CurrentHealth; //0x03C4
    [LuaExposed] public UInt32 r_MaxHealth; //0x03C8
    public UInt32 N000001E6; //0x03CC
    public UInt16 N000000CD; //0x03D0
    [LuaExposed] public UInt16 r_LinkedStableBuildingId; // 0x03D2
    [LuaExposed] public UInt32 r_ShootingSalvoLeft; //0x03D4
    public UInt32 N000000CE; //0x03D8
    [LuaExposed] public UInt32 r_LinkedStableGlobalId; //0x03DC
    [LuaExposed] public UInt32 r_BlessedElapseTickTimer; //0x03E0
    [LuaExposed] public UInt16 r_ContextTargetTileX; //0x03E4
    [LuaExposed] public UInt16 r_ContextTargetTileY; //0x03E6
    public UInt16 N000000D0; //0x03E8
    [LuaExposed] public UInt16 r_UnknownAttackiterator; //0x03EA
    public UInt32 N000001EE; //0x03EC
    public UInt32 N000000D1; //0x03F0
    public UInt32 N000001F0; //0x03F4
    public UInt16 N000000D2; //0x03F8
    public UInt16 r_AISiegeEngineRelatedMaybe; //0x03FA (See c_game_ai_dissolve_siege_engines)
    public UInt16 N000001F2; //0x03FC
    public UInt16 r_SiegeEngineRelatedUnknown; //0x03FC (On arab. ballista shooting)
    [LuaExposed] public UInt32 r_ContextCurrentPositionTileId; //0x0400
    public UInt32 N000001F4; //0x0404
    public UInt32 N000000D4; //0x0408
    public UInt16 N000001F6; //0x040C
    public byte N000001F6_2; //0x040E
    public byte N000001F6_3; //0x040F
    public UInt32 N000000D5; //0x0410
    public UInt32 N000001F8; //0x0414
    public UInt32 N000000D6; //0x0418
    public UInt32 N000001FA; //0x041C
    public UInt32 N000000D7; //0x0420
    public UInt16 r_AITribeRoleRelatedUnknown; //0x0424
    [LuaExposed] public UInt16 r_AITribeRole; //0x0426
    public UInt32 N000000D8; //0x0428
    public UInt32 N000001FE; //0x042C
    public UInt16 N000000D9; //0x0430
    public UInt16 r_FarmerAIRelatedUnknown; //0x0430
    [LuaExposed] public UInt32 r_NearestEnemyWorldTiles; //0x0434
    public UInt32 N000000DA; //0x0438
    public UInt32 N00000202; //0x043C
    [LuaExposed] public UInt16 r_StealthTimer; //0x0440
    public UInt16 N000000DB;
    public UInt32 N00000204; //0x0444
    public UInt32 N000000DC; //0x0448
    public UInt32 N00000206; //0x044C
    public UInt32 N000000DD; //0x0450
    public UInt32 N00000208; //0x0454
    public UInt16 N000000DE; //0x0458
    [LuaExposed] public UInt16 r_DemolisherShieldHealth;
    [LuaExposed] public UInt32 r_DemolisherShieldLastTakenDamageCooldown; //0x045C
    public UInt32 N000000DF; //0x0460
    public UInt32 N0000020C; //0x0464
    public UInt32 N000000E3; //0x0468
    public UInt32 N0000020E; //0x046C
    public UInt32 N000000E4; //0x0470
    public UInt32 N00000210; //0x0474
    public UInt32 N000000E5; //0x0478
    public UInt32 N00000212; //0x047C
    public UInt32 N000000E6; //0x0480
    public UInt32 N00000214; //0x0484
    public UInt32 N000000E7; //0x0488
    public UInt32 N00000216; //0x048C

    /// <summary>
    /// IPositionable: CurrentTilePosition
    /// </summary>
    /// <returns>Current tile position as UnmanagedVector2*</returns>
    public UnmanagedVector2<UInt16>* CurrentTilePosition()
    {
        fixed (UInt16* ptr = &r_CurrentTilePositionX)
        {
            return (UnmanagedVector2<UInt16>*)ptr;
        }
    }

    /// <summary>
    /// Determines whether the specified <see cref="GameUnit"/> instance is equal to the current instance.
    /// </summary>
    /// <param name="other">The <see cref="GameUnit"/> instance to compare with the current instance.</param>
    /// <returns>true if the specified instance has the same global identifier as the current instance; otherwise, false.</returns>
    public bool Equals(GameUnit other)
    {
        return r_GlobalId == other.r_GlobalId;
    }
}; //Size: 0x0490