using System;
using System.Runtime.InteropServices;

namespace SHCDESE.Interop.VTables;

[StructLayout(LayoutKind.Sequential, Pack = 8)]
public unsafe struct BuildingFunctionsVTable
{
    // 0 - 2
    public delegate* unmanaged[Cdecl]<Int64> NullUpdate;
    public delegate* unmanaged[Cdecl]<Int64> HovelUpdate;
    public delegate* unmanaged[Cdecl]<Int64> OutpostBedouinUpdate;

    // 3 - 10
    public delegate* unmanaged[Cdecl]<Int64> WoodcuttersHutUpdate;
    public delegate* unmanaged[Cdecl]<Int64> OxenBaseUpdate;
    public delegate* unmanaged[Cdecl]<Int64> IronMineUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PitchDiggerUpdate;
    public delegate* unmanaged[Cdecl]<Int64> HuntersHutUpdate;
    public delegate* unmanaged[Cdecl]<Int64> BarracksWoodUpdate;
    public delegate* unmanaged[Cdecl]<Int64> BarracksStoneUpdate;
    public delegate* unmanaged[Cdecl]<Int64> GoodsYardUpdate;

    // 11 - 20
    public delegate* unmanaged[Cdecl]<Int64> ArmouryUpdate;
    public delegate* unmanaged[Cdecl]<Int64> FletchersWorkshopUpdate;
    public delegate* unmanaged[Cdecl]<Int64> BlacksmithsWorkshopUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PoleturnersWorkshopUpdate;
    public delegate* unmanaged[Cdecl]<Int64> ArmourersWorkshopUpdate;
    public delegate* unmanaged[Cdecl]<Int64> TannersWorkshopUpdate;
    public delegate* unmanaged[Cdecl]<Int64> BakersWorkshopUpdate;
    public delegate* unmanaged[Cdecl]<Int64> BrewersWorkshopUpdate;
    public delegate* unmanaged[Cdecl]<Int64> GranaryUpdate;
    public delegate* unmanaged[Cdecl]<Int64> QuarryUpdate;

    // 21 - 30
    public delegate* unmanaged[Cdecl]<Int64> QuarrypileUpdate;
    public delegate* unmanaged[Cdecl]<Int64> InnUpdate;
    public delegate* unmanaged[Cdecl]<Int64> HealerUpdate;
    public delegate* unmanaged[Cdecl]<Int64> EngineersGuildUpdate;
    public delegate* unmanaged[Cdecl]<Int64> TunnellersGuildUpdate;
    public delegate* unmanaged[Cdecl]<Int64> TradepostUpdate;
    public delegate* unmanaged[Cdecl]<Int64> WellUpdate;
    public delegate* unmanaged[Cdecl]<Int64> OilSmelterUpdate;
    public delegate* unmanaged[Cdecl]<Int64> SiegeTentUpdate;
    public delegate* unmanaged[Cdecl]<Int64> WheatfarmUpdate;

    // 31 - 40
    public delegate* unmanaged[Cdecl]<Int64> HopsfarmUpdate;
    public delegate* unmanaged[Cdecl]<Int64> ApplefarmUpdate;
    public delegate* unmanaged[Cdecl]<Int64> CattlefarmUpdate;
    public delegate* unmanaged[Cdecl]<Int64> MillUpdate;
    public delegate* unmanaged[Cdecl]<Int64> StablesUpdate;
    public delegate* unmanaged[Cdecl]<Int64> Church1Update;
    public delegate* unmanaged[Cdecl]<Int64> Church2Update;
    public delegate* unmanaged[Cdecl]<Int64> Church3Update;
    public delegate* unmanaged[Cdecl]<Int64> RuinsUpdate;
    public delegate* unmanaged[Cdecl]<Int64> KeepOneUpdate;

    // 41 - 50
    public delegate* unmanaged[Cdecl]<Int64> KeepTwoUpdate;
    public delegate* unmanaged[Cdecl]<Int64> KeepThreeUpdate;
    public delegate* unmanaged[Cdecl]<Int64> KeepFourUpdate;
    public delegate* unmanaged[Cdecl]<Int64> KeepFiveUpdate;
    public delegate* unmanaged[Cdecl]<Int64> GateMainUpdate;
    public delegate* unmanaged[Cdecl]<Int64> GateInnerUpdate;
    public delegate* unmanaged[Cdecl]<Int64> GateWoodUpdate;
    public delegate* unmanaged[Cdecl]<Int64> GatePosternUpdate;
    public delegate* unmanaged[Cdecl]<Int64> DrawbridgeUpdate;
    public delegate* unmanaged[Cdecl]<Int64> TunnelEntranceUpdate;

    // 51 - 60
    public delegate* unmanaged[Cdecl]<Int64> ParadegroundOilUpdate;
    public delegate* unmanaged[Cdecl]<Int64> SignpostUpdate;
    public delegate* unmanaged[Cdecl]<Int64> ParadegroundEngUpdate;
    public delegate* unmanaged[Cdecl]<Int64> SiegeTentArabBallistaUpdate;
    public delegate* unmanaged[Cdecl]<Int64> CampgroundUpdate;
    public delegate* unmanaged[Cdecl]<Int64> ParadegroundMissUpdate;
    public delegate* unmanaged[Cdecl]<Int64> ParadegroundLgtUpdate;
    public delegate* unmanaged[Cdecl]<Int64> ParadegroundHvyUpdate;
    public delegate* unmanaged[Cdecl]<Int64> ParadegroundTunUpdate;
    public delegate* unmanaged[Cdecl]<Int64> GatehouseUpdate;

    // 61 - 70
    public delegate* unmanaged[Cdecl]<Int64> TowerUpdate;
    public delegate* unmanaged[Cdecl]<Int64> GallowsUpdate;
    public delegate* unmanaged[Cdecl]<Int64> StocksUpdate;
    public delegate* unmanaged[Cdecl]<Int64> WitchHoistUpdate;
    public delegate* unmanaged[Cdecl]<Int64> MaypoleUpdate;
    public delegate* unmanaged[Cdecl]<Int64> GardenUpdate;
    public delegate* unmanaged[Cdecl]<Int64> KillingPitUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PitchDitchUpdate;
    public delegate* unmanaged[Cdecl]<Int64> SiegeTowerUpdate;
    public delegate* unmanaged[Cdecl]<Int64> WaterpotUpdate;

    // 71 - 80
    public delegate* unmanaged[Cdecl]<Int64> KeepdoorLeftUpdate;
    public delegate* unmanaged[Cdecl]<Int64> KeepdoorRightUpdate;
    public delegate* unmanaged[Cdecl]<Int64> KeepdoorUpdate;
    public delegate* unmanaged[Cdecl]<Int64> Tower1Update;
    public delegate* unmanaged[Cdecl]<Int64> Tower2Update;
    public delegate* unmanaged[Cdecl]<Int64> Tower3Update;
    public delegate* unmanaged[Cdecl]<Int64> Tower4Update;
    public delegate* unmanaged[Cdecl]<Int64> Tower5Update;
    public delegate* unmanaged[Cdecl]<Int64> Tower5DestroyedUpdate;
    public delegate* unmanaged[Cdecl]<Int64> SiegeTentCatapultUpdate;

    // 81 - 90
    public delegate* unmanaged[Cdecl]<Int64> SiegeTentTrebuchetUpdate;
    public delegate* unmanaged[Cdecl]<Int64> SiegeTentSiegeTowerUpdate;
    public delegate* unmanaged[Cdecl]<Int64> SiegeTentBatteringRamUpdate;
    public delegate* unmanaged[Cdecl]<Int64> SiegeTentPortableShieldUpdate;
    public delegate* unmanaged[Cdecl]<Int64> TunnelConstructionUpdate;
    public delegate* unmanaged[Cdecl]<Int64> Tower1DestroyedUpdate;
    public delegate* unmanaged[Cdecl]<Int64> Tower2DestroyedUpdate;
    public delegate* unmanaged[Cdecl]<Int64> Tower3DestroyedUpdate;
    public delegate* unmanaged[Cdecl]<Int64> Tower4DestroyedUpdate;
    public delegate* unmanaged[Cdecl]<Int64> WasWallUpdate;

    // 91 - 100
    public delegate* unmanaged[Cdecl]<Int64> CessPitUpdate;
    public delegate* unmanaged[Cdecl]<Int64> BurningStakeUpdate;
    public delegate* unmanaged[Cdecl]<Int64> GibbetUpdate;
    public delegate* unmanaged[Cdecl]<Int64> DungeonUpdate;
    public delegate* unmanaged[Cdecl]<Int64> RackStretchingUpdate;
    public delegate* unmanaged[Cdecl]<Int64> RackFloggingUpdate;
    public delegate* unmanaged[Cdecl]<Int64> ChoppingBlockUpdate;
    public delegate* unmanaged[Cdecl]<Int64> DunkingStoolUpdate;
    public delegate* unmanaged[Cdecl]<Int64> DogCageUpdate;
    public delegate* unmanaged[Cdecl]<Int64> StatueUpdate;

    // 101 - 108
    public delegate* unmanaged[Cdecl]<Int64> ShrineUpdate;
    public delegate* unmanaged[Cdecl]<Int64> BeeHiveUpdate;
    public delegate* unmanaged[Cdecl]<Int64> DancingBearUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PondUpdate;
    public delegate* unmanaged[Cdecl]<Int64> BearCaveUpdate;
    public delegate* unmanaged[Cdecl]<Int64> OutpostUpdate;
    public delegate* unmanaged[Cdecl]<Int64> OutpostArabUpdate;
    public delegate* unmanaged[Cdecl]<Int64> BedouinStockadeUpdate;
    public delegate* unmanaged[Cdecl]<Int64> DockUpdate;
    public delegate* unmanaged[Cdecl]<Int64> StructMax; // 108

    // 109 - Gap
    private readonly delegate* unmanaged[Cdecl]<void> _pad109;

    // 110 - 120
    public delegate* unmanaged[Cdecl]<Int64> WoodWallUpdate; // 110
    public delegate* unmanaged[Cdecl]<Int64> StoneWallUpdate;
    public delegate* unmanaged[Cdecl]<Int64> CrenalWallUpdate;
    public delegate* unmanaged[Cdecl]<Int64> StairsUpdate;
    public delegate* unmanaged[Cdecl]<Int64> BrazierUpdate;
    public delegate* unmanaged[Cdecl]<Int64> MangonelUpdate;
    public delegate* unmanaged[Cdecl]<Int64> BallistaUpdate;
    public delegate* unmanaged[Cdecl]<Int64> HeadOnSpikeUpdate;
    public delegate* unmanaged[Cdecl]<Int64> GardenSmallUpdate;
    public delegate* unmanaged[Cdecl]<Int64> GardenMedUpdate;
    public delegate* unmanaged[Cdecl]<Int64> GardenLargeUpdate;

    // 121 - 130
    public delegate* unmanaged[Cdecl]<Int64> PondSmallUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PondLargeUpdate;
    public delegate* unmanaged[Cdecl]<Int64> Flag1Update;
    public delegate* unmanaged[Cdecl]<Int64> Flag2Update;
    public delegate* unmanaged[Cdecl]<Int64> Flag3Update;
    public delegate* unmanaged[Cdecl]<Int64> Flag4Update;
    public delegate* unmanaged[Cdecl]<Int64> GateWood1AUpdate;
    public delegate* unmanaged[Cdecl]<Int64> GateWood1BUpdate;
    public delegate* unmanaged[Cdecl]<Int64> GateWood1CUpdate;
    public delegate* unmanaged[Cdecl]<Int64> GateWood1DUpdate;

    // 131 - 140
    public delegate* unmanaged[Cdecl]<Int64> GateStone1AUpdate;
    public delegate* unmanaged[Cdecl]<Int64> GateStone1BUpdate;
    public delegate* unmanaged[Cdecl]<Int64> GateStone2AUpdate;
    public delegate* unmanaged[Cdecl]<Int64> GateStone2BUpdate;
    public delegate* unmanaged[Cdecl]<Int64> Ruins01Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins02Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins03Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins04Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins05Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins06Update;

    // 141 - 150
    public delegate* unmanaged[Cdecl]<Int64> Ruins07Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins08Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins09Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins10Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins11Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins12Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins13Update;
    public delegate* unmanaged[Cdecl]<Int64> PeopleArchersUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeopleSpearmenUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeoplePikemenUpdate;

    // 151 - 160
    public delegate* unmanaged[Cdecl]<Int64> PeopleMacemenUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeopleXbowmenUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeopleSwordsmenUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeopleKnightsUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeopleLaddermenUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeopleEngineersUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeopleEngineersPotsUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeopleMonksUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeopleCatapultsUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeopleTrebuchetsUpdate;

    // 161 - 167
    public delegate* unmanaged[Cdecl]<Int64> PeopleBatteringRamsUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeopleSiegeTowersUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeoplePortableShieldsUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeopleTunnelersUpdate;
    public delegate* unmanaged[Cdecl]<Int64> _pad165;
    public delegate* unmanaged[Cdecl]<Int64> _pad166;
    public delegate* unmanaged[Cdecl]<Int64> _pad167;

    // 168 - 175
    public delegate* unmanaged[Cdecl]<Int64> NewDigMoatUpdate; // 168
    public delegate* unmanaged[Cdecl]<Int64> NewFillMoatUpdate;
    public delegate* unmanaged[Cdecl]<Int64> MarkerPoint1Update;
    public delegate* unmanaged[Cdecl]<Int64> MarkerPoint2Update;
    public delegate* unmanaged[Cdecl]<Int64> MarkerPoint3Update;
    public delegate* unmanaged[Cdecl]<Int64> MarkerPoint4Update;
    public delegate* unmanaged[Cdecl]<Int64> MarkerPoint5Update;
    public delegate* unmanaged[Cdecl]<Int64> MarkerPoint6Update;

    // 176 - 185
    public delegate* unmanaged[Cdecl]<Int64> MarkerPoint7Update;
    public delegate* unmanaged[Cdecl]<Int64> MarkerPoint8Update;
    public delegate* unmanaged[Cdecl]<Int64> MarkerPoint9Update;
    public delegate* unmanaged[Cdecl]<Int64> MarkerPoint10Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins14Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins15Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins16Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins17Update;
    public delegate* unmanaged[Cdecl]<Int64> Pond5Update;
    public delegate* unmanaged[Cdecl]<Int64> Pond6Update;

    // 186 - 190
    public delegate* unmanaged[Cdecl]<Int64> Pond7Update;
    public delegate* unmanaged[Cdecl]<Int64> Pond8Update;
    private readonly delegate* unmanaged[Cdecl]<void> _pad188;
    private readonly delegate* unmanaged[Cdecl]<void> _pad189;
    public delegate* unmanaged[Cdecl]<Int64> InReportsUpdate; // 190

    // 191 - 199 Gap
    private fixed long _pad191[9];

    // 200 - 210
    public delegate* unmanaged[Cdecl]<Int64> SubMenuTowersUpdate; // 200
    public delegate* unmanaged[Cdecl]<Int64> SubMenuMilitaryUpdate;
    public delegate* unmanaged[Cdecl]<Int64> SubMenuGatehousesUpdate;
    public delegate* unmanaged[Cdecl]<Int64> SubMenuKeepsUpdate;
    public delegate* unmanaged[Cdecl]<Int64> SubMenuGatehousesWoodUpdate;
    public delegate* unmanaged[Cdecl]<Int64> SubMenuGatehousesStoneSmallUpdate;
    public delegate* unmanaged[Cdecl]<Int64> SubMenuGatehousesStoneLargeUpdate;
    public delegate* unmanaged[Cdecl]<Int64> SubMenuGoodUpdate;
    public delegate* unmanaged[Cdecl]<Int64> SubMenuBadUpdate;
    public delegate* unmanaged[Cdecl]<Int64> NewEditorDeleteUpdate;
    public delegate* unmanaged[Cdecl]<Int64> MenuReturnTowersUpdate;

    // 211 - 216
    public delegate* unmanaged[Cdecl]<Int64> MenuReturnGatehousesUpdate;
    public delegate* unmanaged[Cdecl]<Int64> MenuReturnMilitaryUpdate;
    public delegate* unmanaged[Cdecl]<Int64> MenuReturnKeepsUpdate;
    public delegate* unmanaged[Cdecl]<Int64> MenuReturnGoodUpdate;
    public delegate* unmanaged[Cdecl]<Int64> MenuReturnBadUpdate;
    public delegate* unmanaged[Cdecl]<Int64> NewDeleteUpdate;

    // 217 - 219 Gap
    private fixed long _pad217[3];

    // 220 - 227
    public delegate* unmanaged[Cdecl]<Int64> PeopleArabBowUpdate; // 220
    public delegate* unmanaged[Cdecl]<Int64> PeopleArabSlaveUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeopleArabSlingerUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeopleArabAssasinUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeopleArabHorsemanUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeopleArabSwordsmanUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeopleArabGrenadierUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeopleArabBallistaUpdate;

    // 228 - 229 Gap
    private readonly delegate* unmanaged[Cdecl]<void> _pad228;
    private readonly delegate* unmanaged[Cdecl]<void> _pad229;

    // 230 - 240
    public delegate* unmanaged[Cdecl]<Int64> Ruins18Update; // 230
    public delegate* unmanaged[Cdecl]<Int64> Ruins19Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins20Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins21Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins22Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins23Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins24Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins25Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins26Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins27Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins28Update;

    // 241 - 250
    public delegate* unmanaged[Cdecl]<Int64> Ruins29Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins30Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins31Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins32Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins33Update;
    public delegate* unmanaged[Cdecl]<Int64> Ruins34Update;
    public delegate* unmanaged[Cdecl]<Int64> PeopleBedouinCamelLancerUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeopleBedouinHealerUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeopleBedouinEunuchUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeopleBedouinAmbusherUpdate;

    // 251 - 254
    public delegate* unmanaged[Cdecl]<Int64> PeopleBedouinSkirmisherUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeopleBedouinHeavyCamelUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeopleBedouinSapperUpdate;
    public delegate* unmanaged[Cdecl]<Int64> PeopleBedouinDemolisherUpdate;
}