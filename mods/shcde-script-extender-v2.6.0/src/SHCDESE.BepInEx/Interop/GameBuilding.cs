using SHCDESE.API.Components.Spatial;
using SHCDESE.Interop.Enums;
using SHCDESE.Lua.CodeGen;
using System;
using System.Runtime.InteropServices;

namespace SHCDESE.Interop;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct GameBuilding : IPositionable, IEquatable<GameBuilding>
{
    public UInt32 N00012005;
    [LuaExposed] public GM r_GameMaterialIndex;
    public UInt16 N00012072;
    [LuaExposed] public UInt32 r_SpritePlayerColorId;
    public UInt32 N00012022;
    public UInt32 N00012007;
    [LuaExposed] public UInt32 r_TimeSpentInProductionTotal1;
    [LuaExposed] public UInt32 r_TimeSpentInProductionTotal2;
    public UInt32 N00012026;
    public UInt32 N00012009;
    public UInt32 N00012028;
    public UInt32 N0001200A;
    public UInt32 N0001202A;
    public UInt32 N0001200B;
    public UInt32 N0001202C;
    public UInt32 N0001200C;
    public UInt32 N0001202E;
    public UInt32 N0001200D;
    public UInt32 N00012030;
    public UInt32 N0001200E;
    public UInt32 N00012032;
    public UInt32 N0001200F;
    public UInt32 N00012034;
    public UInt32 N00012010;
    public UInt32 N00012036;
    public UInt32 N00012011;
    public UInt32 N00012038;
    public UInt32 N00012012;
    public UInt32 N0001203A;
    public UInt32 N00012013;
    public UInt32 N0001203C;
    public UInt32 N00012014;
    public UInt32 N0001203E;
    public UInt32 N0001206C;
    public UInt32 N00012015;
    public UInt32 N00012040;
    [LuaExposed] public UInt32 r_TileIdOriginBottomRight;
    public UInt32 N00012042;
    public UInt32 N00012017;
    public UInt32 N00012044;
    public UInt32 N00012018;
    [LuaExposed] public UInt32 r_ProductionAnimationIndex;
    public UInt32 N00012019;
    public UInt32 N00012048;
    public UInt32 N0001201A;
    public UInt32 N0001204A;
    public UInt32 N0001201B;
    public UInt32 N0001204C;
    public UInt32 N0001201C;
    [LuaExposed] public UInt32 r_TicksWhenProductionLive;
    public UInt32 N0001201D;
    [LuaExposed] public UInt32 r_TileIdOriginBottomRightInnerOne;
    [LuaExposed] public GatePathOverrideMode r_AIWalkableState; // Related to gatehouses states (auto-close, etc) (See 
    public UInt16 N0001201E_2;
    [LuaExposed] public AliveState r_AliveState;
    [LuaExposed] public eStructs r_BuildingType;
    public UInt16 N000017BB;
    [LuaExposed] public UInt16 r_PlayerIdOwner;
    [LuaExposed] public UInt32 r_GlobalId;
    public UInt32 N000017BD;
    public UInt32 N00001751;
    public UInt32 N000017BF;
    [LuaExposed] public UInt16 r_WorldPositionX;
    [LuaExposed] public UInt16 r_WorldPositionY;
    [LuaExposed] public UInt16 r_HeightElevation;
    [LuaExposed] public UInt16 r_TilePositionXBegin;
    [LuaExposed] public UInt16 r_TilePositionYBegin;
    public UInt16 N00002C21;
    [LuaExposed] public UInt32 r_TileIdBegin;
    [LuaExposed] public UInt32 r_OccupyTileGridSize;
    public UInt16 N000017C5;
    [LuaExposed] public UInt16 r_TilePositionXEnd;
    [LuaExposed] public UInt16 r_TilePositionYEnd;
    [LuaExposed] public UInt16 r_SpriteVariationIndex;
    [LuaExposed] public byte r_RandomShort;
    public byte N000057A8;
    public UInt16 N00002C2D;
    public UInt32 N00001756;
    [LuaExposed] public Int16 r_CurrentHealth;
    [LuaExposed] public UInt16 r_MaxHealth;
    public UInt32 N00001757;
    public UInt32 N000017CB;
    public UInt32 N00001758;
    public UInt32 N000017CD;
    [LuaExposed] public UInt32 r_NullAmount;
    [LuaExposed] public UInt32 r_WoodLogsAmount;
    [LuaExposed] public UInt32 r_WoodPlanksAmount;
    [LuaExposed] public UInt32 r_RawHopsAmount;
    [LuaExposed] public UInt32 r_StoneBlocksAmount;
    [LuaExposed] public UInt32 r_CowHidesAmount;
    [LuaExposed] public UInt32 r_IronIngotsAmount;
    [LuaExposed] public UInt32 r_PitchRawAmount;
    [LuaExposed] public UInt32 r_PitchRefinedAmount;
    [LuaExposed] public UInt32 r_RawWheatAmount;
    [LuaExposed] public UInt32 r_BreadAmount;
    [LuaExposed] public UInt32 r_CheeseAmount;
    [LuaExposed] public UInt32 r_MeatAmount;
    [LuaExposed] public UInt32 r_FruitAmount;
    [LuaExposed] public UInt32 r_AleAmount;
    [LuaExposed] public UInt32 r_GoldAmount;
    [LuaExposed] public UInt32 r_FlourAmount;
    [LuaExposed] public UInt32 r_ArmouryBowsAmount;
    [LuaExposed] public UInt32 r_ArmouryCrossbowsAmount;
    [LuaExposed] public UInt32 r_ArmourySpearsAmount;
    [LuaExposed] public UInt32 r_ArmouryPikesAmount;
    [LuaExposed] public UInt32 r_ArmouryMacesAmount;
    [LuaExposed] public UInt32 r_ArmourySwordsAmount;
    [LuaExposed] public UInt32 r_ArmouryLeatherArmorAmount;
    [LuaExposed] public UInt32 r_ArmouryMetalArmorAmount;
    [LuaExposed] public UInt32 r_CurrentGoodStackAmount;
    [LuaExposed] public UInt32 r_MaxGoodStackAmount;
    [LuaExposed] public eGoods r_LocalStorageGoodType;
    public UInt16 N00002C19;
    public UInt16 N00001767;
    [LuaExposed] public UInt16 r_StoneQuarry_StockPileBuildingId;
    public UInt16 N000017EB;
    [LuaExposed] public UInt16 r_HousingPopulationSpace;
    [LuaExposed] public UInt16 r_TotalWorkersRequired;
    [LuaExposed] public UInt16 r_TotalCurrentWorkers;
    [LuaExposed] public UInt16 r_TotalMissingWorkers;
    [LuaExposed] public UInt16 r_WorkerId_1;
    [LuaExposed] public UInt16 r_WorkerId_2;
    [LuaExposed] public UInt16 r_WorkerId_3;
    [LuaExposed] public UInt32 r_WorkerId_4;
    public UInt32 N0000176A;
    public UInt32 N000017F1;
    public UInt32 N0000176B;
    public UInt32 N000017F3;
    public UInt32 N0000176C;
    public UInt32 N000017F5;
    public UInt32 N0000176D;
    public UInt32 N000017F7;
    [LuaExposed] public UInt32 r_OccupiedTileIdsArrayBegin;
    [LuaExposed] public UInt32 r_OTA1;
    [LuaExposed] public UInt32 r_OTA2;
    [LuaExposed] public UInt32 r_OTA3;
    [LuaExposed] public UInt32 r_OTA4;
    [LuaExposed] public UInt32 r_OTA5;
    [LuaExposed] public UInt32 r_OTA6;
    [LuaExposed] public UInt32 r_OTA7;
    [LuaExposed] public UInt32 r_OTA8;
    [LuaExposed] public UInt32 r_OTA9;
    [LuaExposed] public UInt32 r_OTA10;
    [LuaExposed] public UInt32 r_OTA11;
    [LuaExposed] public UInt32 r_OTA12;
    [LuaExposed] public UInt32 r_OTA13;
    [LuaExposed] public UInt32 r_OTA14;
    [LuaExposed] public UInt32 r_OTA15;
    [LuaExposed] public UInt32 r_OTA16;
    [LuaExposed] public UInt32 r_OTA17;
    [LuaExposed] public UInt32 r_OTA18;
    [LuaExposed] public UInt32 r_OTA19;
    [LuaExposed] public UInt32 r_OTA20;
    [LuaExposed] public UInt32 r_OTA21;
    [LuaExposed] public UInt32 r_OTA22;
    [LuaExposed] public UInt32 r_OTA23;
    [LuaExposed] public UInt32 r_OTA24;
    [LuaExposed] public UInt32 r_OTA25;
    [LuaExposed] public UInt32 r_OTA26;
    [LuaExposed] public UInt32 r_OTA27;
    [LuaExposed] public UInt32 r_OTA28;
    [LuaExposed] public UInt32 r_OTA29;
    [LuaExposed] public UInt32 r_OTA30;
    [LuaExposed] public UInt32 r_OTA31;
    [LuaExposed] public UInt32 r_OTA32;
    [LuaExposed] public UInt32 r_OTA33;
    [LuaExposed] public UInt32 r_OTA34;
    [LuaExposed] public UInt32 r_OTA35;
    public UInt32 Unknown22;
    public UInt32 Unknown23;
    public UInt32 Unknown24;
    public UInt32 Unknown25;
    public UInt32 Unknown26;
    public UInt32 Unknown27;
    public UInt32 Unknown28;
    public UInt32 Unknown29;
    public UInt32 Unknown30;
    [LuaExposed] public UInt32 r_TicksAlive;
    public UInt32 N00001785;
    [LuaExposed] public UInt32 r_BuildingVariation;
    public UInt32 N00001786;
    public UInt16 Unknown32;
    [LuaExposed] public eGoods r_NextProducedGoodId;
    [LuaExposed] public eGoods r_ProducedGoodId;
    public UInt16 Unknown32_2;
    public UInt16 N0000182B;
    [LuaExposed] public byte r_IsSleeping;
    [LuaExposed] public byte r_TotalHorses;
    public UInt16 N00001788;
    [LuaExposed] public UInt16 r_HorseRechargeTimer; // Recharges to 550 and then spawns a new horse.
    public UInt32 N0000182D;
    public UInt16 N00001789;
    [LuaExposed] public UInt16 r_GateState; // 0x0B00: Open? TODO
    public UInt16 N00001789_IDK;
    public byte N00004896;
    [LuaExposed] public byte r_UsedHorses;
    [LuaExposed] public UInt32 r_UsedInSiegeAttemptId;
    public UInt32 N00001831;
    [LuaExposed] public UInt16 r_ContextFoodAmount;
    public UInt16 Unknown34;
    public UInt32 Unknown33;
    [LuaExposed] public Int16 r_GateDoNotCloseForTicks;
    public UInt16 N00005DB8;
    public UInt16 N00001835;
    [LuaExposed] public UInt16 r_OnFireTicks;
    [LuaExposed] public UInt32 r_GateDoNotOpenForTicks;
    [LuaExposed] public UInt16 p_CooldownTimer;
    [LuaExposed] public UInt16 r_CapturedByPlayerId;
    public UInt32 N0000178E;
    public UInt32 N00001839;
    public UInt16 N0000178F;
    [LuaExposed] public UInt16 r_GatehouseId;
    public UInt32 N0000183B;
    public UInt32 N00001790;
    public UInt16 N0000183D;
    [LuaExposed] public UInt16 r_CausedFireByPlayer;
    [LuaExposed] public UInt16 r_UsedHorse1UnitId;
    [LuaExposed] public UInt16 r_UsedHorse2UnitId;
    [LuaExposed] public UInt16 r_UsedHorse3UnitId;
    [LuaExposed] public UInt16 r_UsedHorse4UnitId;
    [LuaExposed] public UInt32 r_UsedHorse1GlobalId;
    [LuaExposed] public UInt32 r_UsedHorse2GlobalId;
    [LuaExposed] public UInt32 r_UsedHorse3GlobalId;
    [LuaExposed] public UInt32 r_UsedHorse4GlobalId;
    public UInt32 N00001794;
    public UInt32 N00001845;
    public UInt32 N00001795;
    public UInt32 N00001847;
    public UInt32 N00001796;
    public UInt32 N00001849;
    public UInt32 N00001797;
    public UInt32 N0000184B;
    public UInt32 N00001798;
    public UInt32 N0000184D;
    public UInt32 N00001799;
    public UInt32 N0000184F;
    public UInt32 N0000179A;

    /// <summary>
    /// IPositionable: CurrentTilePosition
    /// </summary>
    /// <returns>Current tile position as UnmanagedVector2*</returns>
    public UnmanagedVector2<UInt16>* CurrentTilePosition()
    {
        fixed (UInt16* ptr = &r_TilePositionXBegin)
        {
            return (UnmanagedVector2<UInt16>*)ptr;
        }
    }

    /// <summary>
    /// Determines whether the specified <see cref="GameBuilding"/> instance is equal to the current instance.
    /// </summary>
    /// <param name="other">The <see cref="GameBuilding"/> instance to compare with the current instance.</param>
    /// <returns>true if the specified instance has the same global identifier as the current instance; otherwise, false.</returns>
    public bool Equals(GameBuilding other)
    {
        return this.r_GlobalId == other.r_GlobalId;
    }
}; //Size: 0x032C

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct GameBuildingManager
{
    public UInt32 ActiveBuildings; //0x0000
    public UInt32 N0000184D; //0x0004
    public UInt32 N0000179C; //0x0008
    public UInt32 N0000184F; //0x000C
    public UInt32 N0000179D; //0x0010
    public UInt32 N00001851; //0x0014
    public UInt32 N0000179E; //0x0018
    public UInt32 N00001853; //0x001C
    public UInt32 N0000179F; //0x0020
    public UInt32 N00001855; //0x0024
    public UInt32 N000017A0; //0x0028
    public UInt32 Unk; //0x002C
    public UInt32 N000017A1; //0x0030
    public UInt32 N00001859; //0x0034
    public UInt32 N000017A2; //0x0038
    public UInt32 N000017F3; //0x003C
    public UInt32 N000017A3; //0x0040
    public UInt32 N000017F5; //0x0044
    public UInt32 N000017A4; //0x0048
    public UInt32 N000017F7; //0x004C
    public UInt32 BuildingsAllocated; //0x0050
    public UInt32 FreeBuildings; //0x0054
    public fixed byte pad_0058[816]; //0x0058
    
    public GameBuilding BuildingsArray; //0x0388
};