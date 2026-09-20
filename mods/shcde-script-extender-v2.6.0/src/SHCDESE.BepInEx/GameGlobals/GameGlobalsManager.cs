using Iced.Intel;
using RedBird.Core.Memory;
using RedBird.Core.Memory.Managed;
using RedBird.X64.Assembly.InstructionWalker;
using RedBird.X64.Assembly.Stateful;
using RedBird.X64.Memory.Scanners;
using Serilog;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.Interop.VTables;
using SHCDESE.Logging;
using System;

namespace SHCDESE.GameGlobals;

public sealed class GameGlobalsManager
{
    private static readonly Lazy<GameGlobalsManager> lazy = new Lazy<GameGlobalsManager>(() => new GameGlobalsManager());
    public static GameGlobalsManager Instance { get { return lazy.Value; } }


    private ManagedAssemblyStateManager _managedAssemblyStateManager;
    public ManagedAssemblyStateManager GetManagedAssemblyStateManager() => _managedAssemblyStateManager;

    private GameGlobalsManager()
    {
        _managedAssemblyStateManager = new ManagedAssemblyStateManager();
    }

    // c_game_update_player_popularity tuning values. These are managed values so
    // mods can temporarily Push/Pop overrides without replacing the hook.
    public ManagedValue<int> RationsPopModifierNone { get; } = new(-200);
    public ManagedValue<int> RationsPopModifierHalf { get; } = new(-100);
    public ManagedValue<int> RationsPopModifierFull { get; } = new(0);
    public ManagedValue<int> RationsPopModifierExtra { get; } = new(100);
    public ManagedValue<int> RationsPopModifierDouble { get; } = new(200);
    public ManagedValue<int> RationsPopModifierNoPreferredFood { get; } = new(-200);
    public ManagedValue<int> RationsPopModifierNoPopulation { get; } = new(200);

    public ManagedValue<int> FoodVarietyPopModifierTwoTypes { get; } = new(25);
    public ManagedValue<int> FoodVarietyPopModifierThreeTypes { get; } = new(50);
    public ManagedValue<int> FoodVarietyPopModifierFourTypes { get; } = new(75);

    public ManagedValue<int> OverpopulationRatioThresholdTierOne { get; } = new(100);
    public ManagedValue<int> OverpopulationRatioThresholdTierTwo { get; } = new(120);
    public ManagedValue<int> OverpopulationRatioThresholdTierThree { get; } = new(140);
    public ManagedValue<int> OverpopulationRatioThresholdTierFour { get; } = new(160);
    public ManagedValue<int> OverpopulationRatioThresholdTierFive { get; } = new(180);
    public ManagedValue<int> OverpopulationPopModifierTierOne { get; } = new(-50);
    public ManagedValue<int> OverpopulationPopModifierTierTwo { get; } = new(-100);
    public ManagedValue<int> OverpopulationPopModifierTierThree { get; } = new(-150);
    public ManagedValue<int> OverpopulationPopModifierTierFour { get; } = new(-200);
    public ManagedValue<int> OverpopulationPopModifierTierFive { get; } = new(-250);

    public ManagedValue<int> TaxPopModifierHugeDonation { get; } = new(175);
    public ManagedValue<int> TaxPopModifierBigDonation { get; } = new(125);
    public ManagedValue<int> TaxPopModifierSmallDonation { get; } = new(75);
    public ManagedValue<int> TaxPopModifierNone { get; } = new(25);
    public ManagedValue<int> TaxPopModifierLow { get; } = new(-50);
    public ManagedValue<int> TaxPopModifierModerate { get; } = new(-100);
    public ManagedValue<int> TaxPopModifierHigh { get; } = new(-150);
    public ManagedValue<int> TaxPopModifierMean { get; } = new(-200);
    public ManagedValue<int> TaxPopModifierUsurious { get; } = new(-300);
    public ManagedValue<int> TaxPopModifierCruel { get; } = new(-400);
    public ManagedValue<int> TaxPopModifierMostCruel { get; } = new(-500);
    public ManagedValue<int> TaxPopModifierCruellest { get; } = new(-600);
    public ManagedValue<int> TaxPopModifierUnaffordableDonation { get; } = new(25);
    public ManagedValue<int> TaxPopModifierUnknownMode { get; } = new(-600);

    public ManagedValue<int> MaxPopularityEventPopModifier { get; } = new(400);

    public ManagedValue<int> ReligionPercentThresholdTierOne { get; } = new(24);
    public ManagedValue<int> ReligionPercentThresholdTierTwo { get; } = new(49);
    public ManagedValue<int> ReligionPercentThresholdTierThree { get; } = new(74);
    public ManagedValue<int> ReligionPercentThresholdTierFour { get; } = new(94);
    public ManagedValue<int> ReligionPopModifierTierOne { get; } = new(50);
    public ManagedValue<int> ReligionPopModifierTierTwo { get; } = new(100);
    public ManagedValue<int> ReligionPopModifierTierThree { get; } = new(150);
    public ManagedValue<int> ReligionPopModifierTierFour { get; } = new(200);
    public ManagedValue<int> ChurchPopModifier { get; } = new(25);
    public ManagedValue<int> CathedralPopModifier { get; } = new(50);

    public ManagedValue<int> AlePercentThresholdTierOne { get; } = new(25);
    public ManagedValue<int> AlePercentThresholdTierTwo { get; } = new(50);
    public ManagedValue<int> AlePercentThresholdTierThree { get; } = new(75);
    public ManagedValue<int> AlePercentThresholdTierFour { get; } = new(100);
    public ManagedValue<int> AlePopModifierTierOne { get; } = new(50);
    public ManagedValue<int> AlePopModifierTierTwo { get; } = new(100);
    public ManagedValue<int> AlePopModifierTierThree { get; } = new(150);
    public ManagedValue<int> AlePopModifierTierFour { get; } = new(200);

    public ManagedValue<int> GoodBadThingPopModifierMultiplier { get; } = new(25);
    public UInt64 MeleeDamageTableRVA = 0;
    public UInt64 MeleeEunuchAOEDamageTableRVA = 0;
    public UInt64 RangedBoltDamageTableRVA = 0;
    public UInt64 RangedJavelinDamageTableRVA = 0;
    public UInt64 RangedSlingerDamageTableRVA = 0;
    public UInt64 RangedArrowDamageTableRVA = 0;

    public UInt64 UnitHealthTableRVA = 0;
    public UInt64 BuildingHealthTableRVA = 0;
    public UInt64 BuildingHousingPopulatonSpaceTableRVA = 0;
    public UInt64 SpeedTableRVA = 0;

    public UInt64 BuildingDefaultCostsTableVA = 0;
    public UInt64 BuildingDefaultCostsTableEndVA = 0;

    public UInt64 BuildingStoneCostsTableRVA = 0;
    public UInt64 BuildingRawPitchCostsTableRVA = 0;
    public UInt64 BuildingGoldCostsTableRVA = 0;
    public UInt64 BuildingWoodCostsTableRVA = 0;
    public UInt64 BuildingIronIngotsCostsTableRVA = 0;

    public UInt64 PlayerDefaultSkirmishSpawnGoldTable = 0;
    public UInt64 PlayerDefaultSkirmishResourcesVA = 0;

    public UInt64 AILordManagerRVA = 0;
    public UInt64 AIBoolPlayerListVA = 0;
    public UInt64 AILineUpVA = 0;
    public UInt64 TeamsListRVA = 0;
    public UInt64 LocalPlayerArmyCountsVA = 0;
    public UInt64 GamePausedVA = 0;
    public UInt64 CurrentMapNameVA = 0;
    public UInt64 CurrentMapNameLengthVA = 0;
    public UInt64 IsInMapEditorVA = 0;
    public UInt64 CurrentMapSizeVA = 0;
    public UInt64 PathfindingContextVA = 0;
    public UInt64 PlayerKeepIsEnclosedVA = 0;
    public UInt64 PlayerExtremePowersEnabledVA = 0;
    public UInt64 MonkAvailableVA = 0;
    public UInt64 EngineerAvailableVA = 0;
    public UInt64 LaddermanAvailableVA = 0;
    public UInt64 AIVDataTableVA = 0;

    public UInt64 TreeGrowthProgressionTableRVA = 0;

    public UInt64 PeasantSpawnRateIncrementsHighPopRVA = 0;
    public UInt64 PeasantSpawnRateIncrementsLowPopRVA = 0;
    public UInt64 PeasantSpawnRateIncrementsDefaultsRVA = 0;

    public UInt64 UnitEUGoldCostTableRVA = 0;
    public UInt64 UnitEUGoodTypeCostsTableRVA = 0;
    public UInt64 TreeProximityAreaLevelTableRVA = 0;

    public UInt64 ChoreManagerVA = 0;
    public UInt64 ChoreSendPhaseVA = 0;

    public UInt64 MessageManagerVA = 0;
    public UInt64 EngineMemoryLastCallDescriptorVA = 0;
    public UInt64 TilePCLTableRVA = 0;

    public ManagedAssemblyImmediate<UInt16>? BallistaDamageDefault = null;
    public ManagedAssemblyImmediate<UInt16>? BallistaDamageToPortableShields = null;
    public ManagedAssemblyImmediate<UInt16>? BallistaDamageToBallista = null;
    public ManagedAssemblyImmediate<UInt16>? BallistaDamageToMangonel = null;
    public ManagedAssemblyImmediate<UInt16>? BallistaDamageToArabBallista = null;
    public ManagedAssemblyImmediate<UInt16>? BallistaDamageToCatapult = null;
    public ManagedAssemblyImmediate<UInt16>? BallistaDamageToTrebutchet = null;
    public ManagedAssemblyImmediate<UInt16>? BallistaDamageToBatteringRamAndSiegeTower = null;

    public ManagedAssemblyMultiValue<UInt16>? PeasantRespawnTickTargetValue = null;
    public ManagedAssemblyMultiValue<UInt16>? PeasantRespawnTickResetValue = null;

    public ManagedAssemblyImmediate<UInt16>? GateHouseCloseDistance = null;
    public ManagedAssemblyImmediate<UInt16>? GateHouseReOpenDistance = null;
    public ManagedAssemblyImmediate<UInt16>? GateHouseUnkDistance = null;

    public ManagedAssemblyImmediate<UInt16>? MaxGoodYards = null;
    public ManagedAssemblyImmediate<UInt16>? MaxGranaries = null;
    public ManagedAssemblyImmediate<UInt16>? MaxArmories = null;

    public ManagedAssemblyImmediate<UInt32>? FriendlyWallToEnemyWallProximityDistAllowance = null;

    public ManagedAssemblyImmediate<UInt16>? BedouinDemolisherShieldHealth = null;

    public ManagedAssemblyImmediate<UInt16>? CatapultRestockStoneCost = null;
    public ManagedAssemblyImmediate<UInt16>? CatapultRestockStoneAmount = null;
    public ManagedAssemblyImmediate<byte>? CatapultInitialStoneAmount = null;

    public ManagedAssemblyImmediate<UInt16>? AssassinDetectionRange = null;
    public ManagedAssemblyImmediate<UInt16>? AssassinTransparencyThreshold = null;

    public ManagedAssemblyMultiValue<Int32>? FoodConsumptionTickThreshold = null;
    public ManagedAssemblyImmediate<UInt16>? AIMaxOxTethers = null;
    public ManagedAssemblyImmediate<UInt16>? AIStoneToOxenRatio = null;
    public ManagedAssemblyImmediate<UInt16>? AIHighGoldThresholdForSiege = null;
    public ManagedAssemblyImmediate<UInt16>? AIGoldThresholdForHarassmentSiegeEngines = null;

    public ManagedAssemblyImmediate<UInt32>? MaxMana = null;
    public ManagedAssemblyDisplacement<UInt32>? ManaRegenAmount = null;

    public ManagedAssemblyImmediate<UInt32>? MacemanMinDistanceToEnemyToRun = null;

    //public ManagedAssemblyImmediate<UInt16>? MinimumKeepPCLTilesToBeEnclosed = null;

    public UInt64 LocalPlayerIdVA = 0;
    public UInt64 ElapsedMapTicksVA = 0;
    public UInt64 CurrentlySelectedBuildingIdVA = 0;

    public UInt64 MapRowLookupTableRVA = 0;
    public UInt64 MapColumnLookupTableRVA = 0;

    public UInt64 GameUnitManagerVA = 0;
    public UInt64 GameTribeManagerVA = 0;
    public UInt64 GameBuildingManagerVA = 0;
    public UInt64 GamePlayerManagerVA = 0;
    public UInt64 GameVegetationManagerVA = 0;
    public UInt64 GameTileManagerVA = 0;
    public UInt64 GameSoundManagerVA = 0;
    public UInt64 GameProjectilesManagerVA = 0;
    public UInt64 PathConnectionRecordManagerRVA = 0;
    public UInt64 GameGatehouseFunctionsVA = 0;
    public NativePointer<GatehouseFunctionsVTable> GameGatehouseFunctionsVTable = null;
    public UInt64 GameUnitFunctionsRVA = 0;
    public NativePointer<UnitFunctionsVTable> GameUnitFunctionsVTable = null;
    public UInt64 GameBuildingFunctionsRVA = 0;
    public NativePointer<BuildingFunctionsVTable> GameBuildingFunctionsVTable = null;
    public UInt64 AIVCastleLayoutTableRVA = 0;

    public UInt64 GameStateChoreHandlersVA = 0;

    public UInt64 BuildingAvailabilityManager = 0;
    public UInt64 MapRulesInfo_AllowedTradingGoodsRVA = 0;
    public UInt64 MapRulesInfo_AllowedArabUnitsVA = 0;
    public UInt64 MapRulesInfo_AllowedEuroUnitsVA = 0;
    public UInt64 MapRulesInfo_AllowedBedouinUnitsVA = 0;
    public UInt64 MapRulesInfo_AllowedProductionGoodsVA = 0;

    public UInt64 AIResourceSellCategoryTableVA = 0;
    public UInt64 AIResourceSellCategoryTableEndVA = 0;

    public UInt64 CurrentSkirmishGameModeVA = 0;
    public UInt64 CurrentTrailTypeVA = 0;
    public UInt64 CurrentGameSkirmishModeVA = 0;
    public UInt64 CurrentGameTypeModeVA = 0;
    public UInt64 AIAdvantageVA = 0;
    public UInt64 CoopTrailIdVA = 0;
    public UInt64 CoopMissionIdVA = 0;

    public UInt64 CurrentContextMapperValueVA = 0;

    /// <summary>
    /// In many cases, this will point to the currently being processed' Unit Id.
    /// </summary>
    public UInt64 CurrentContextUnitValueVA = 0;

    public UInt64 CurrentContextUnitIdVA = 0;

    /// <summary>
    /// In many cases, this will point to the currently being processed' Building Id.
    /// </summary>
    public UInt64 CurrentContextBuildingValueVA = 0;

    public ManagedAssemblyMultiValue<UInt16>? OxStoneRequiredAmount = null;
    public ManagedAssemblyDisplacement<UInt16>? OxStoneReceiveAmount = null;

    // INFO: 
    /* Calculation is done like so in the function:
.text:00000001801786C1                 cmp     edi, 46h ; 'F' <-- unit chimp id
.text:00000001801786C4                 jnz     short loc_1801786CE
.text:00000001801786C6                 lea     ebx, [rdi+5] <-- relative value to deduce cost. 46h+5h=4Bh , to modify this we need to counteract the unitid completely.
.text:00000001801786C9                 jmp     loc_180178785
     * */
    //public ManagedAssemblyMultiDisplacement<UInt16>? UnitGoldCosts = null;
    public ManagedAssemblyImmediate<Int16>? GoldCostEngineer = null;
    public ManagedAssemblyDisplacement<Int16>? GoldCostTunneler = null;
    public ManagedAssemblyDisplacement<Int16>? GoldCostLadderman = null;
    public ManagedAssemblyDisplacement<Int16>? GoldCostMonk = null;
    public ManagedAssemblyDisplacement<Int16>? GoldCostArabBow = null;
    public ManagedAssemblyDisplacement<Int16>? GoldCostArabSlave = null;
    public ManagedAssemblyDisplacement<Int16>? GoldCostArabSlinger = null;
    public ManagedAssemblyDisplacement<Int16>? GoldCostArabAssassin = null;
    public ManagedAssemblyImmediate<Int16>? GoldCostArabhorsemanOrSwordsmanOrDemolisher = null;
    public ManagedAssemblyDisplacement<Int16>? GoldCostArabGrenadier = null;
    public ManagedAssemblyDisplacement<Int16>? GoldCostBedouinCamelLancer = null;
    public ManagedAssemblyDisplacement<Int16>? GoldCostBedouinEunuch = null;
    public ManagedAssemblyDisplacement<Int16>? GoldCostBedouinAmbusher = null;
    public ManagedAssemblyDisplacement<Int16>? GoldCostBedouinSkirmisher = null;
    public ManagedAssemblyDisplacement<Int16>? GoldCostBedouinHeavyCamel = null;
    public ManagedAssemblyDisplacement<Int16>? GoldCostBedouinSapper = null;
    public ManagedAssemblyDisplacement<Int16>? GoldCostBedouinHealer = null;
    public ManagedAssemblyDisplacement<Int16>? GoldCostDefault = null;
    public ManagedAssemblyDisplacement<Int16>? GoldCostDefaultUnknown = null;

    public ManagedAssemblyDisplacement<UInt32>? DateTimeCurrentYearRVA = null;
    public ManagedAssemblyDisplacement<UInt32>? DateTimeCurrentMonthRVA = null;
    public ManagedAssemblyDisplacement<UInt32>? DateTimeCurrentDayRVA = null;
    public ManagedAssemblyImmediate<Int16>? DateTimeDaysInMonthRVA = null;
    public ManagedAssemblyImmediate<Int16>? DateTimeMonthsInYearRVA = null;

    public ManagedAssemblyImmediate<UInt16>? KnightRunSpeedBonus = null;
    public ManagedAssemblyImmediate<UInt16>? ArabHorsemanRunSpeedBonus = null;
    public ManagedAssemblyImmediate<UInt16>? BedouinCamelLancerRunSpeedBonus = null;
    public ManagedAssemblyImmediate<UInt16>? BedouinHeavyCamelRunSpeedBonus = null;

    public ManagedAssemblyImmediate<UInt16>? BuildingRepairProximityCheckRange = null;
    public ManagedAssemblyImmediate<UInt16>? BuildingRepairProximityCheckExRange = null;

    public ManagedAssemblyImmediate<Int16>? StablesHorseRegenTickTarget = null;
    public ManagedAssemblyImmediate<sbyte>? StablesHorsesCap = null;

    public ManagedAssemblyImmediate<Int32>? DiseaseDamage1 = null;
    public ManagedAssemblyImmediate<Int32>? DiseaseDamage2 = null;
    public ManagedAssemblyImmediate<Int32>? DiseaseDamage3 = null;

    public ManagedAssemblyImmediate<Int16>? RabbitDespawnTickTime = null;

    public ManagedAssemblyImmediate<UInt16>? PathfindingMaxTilesConstraint = null;

    public ManagedAssemblyImmediate<UInt16>? CampPeasantsCap = null;

    public UInt64 DefaultTradeBuyPriceTableRVA = 0;

    public UInt64 GameCursorManagerVA = 0;

    //public UInt64 NextUnitIdVA = 0;

    public ManagedAssemblyImmediate<UInt32>? MaxRandomLions = null;
    public UInt64 NoKnockdownWallsVA = 0;
    public UInt64 GlobalImprovedSiegingBehaviourVA = 0;
    public UInt64 GlobalMoreAggressiveAISiegingBehaviourVA = 0;
    public UInt64 GlobalAdvancedOptionsVA = 0;

    public UInt64 GlobalIdsUsedVA = 0;

    public UInt64 PathfindingProfilesUnitTableRVA = 0;
    public UInt64 PathfindingConnectionClassesUnitTableVA = 0;

    /// <summary>
    /// This variable contains the playerid that sent the last in-game "message"
    /// Mostly used for ai-related lookups.
    /// The PlayState struct has one technically already, but its too volatile.
    /// </summary>
    public static byte LastMessageFromCharacterCached = 0;

    /// <summary>
    /// Try to retrieve all relevant game globals we need
    /// to properly interop with the game.
    /// </summary>
    internal unsafe void FindGameGlobals(ReadOnlySpan<byte> memory, ScanRegion region)
    {
        LogHelper.Information($"Finding Game Globals...");

        UInt64 currentImageBase = (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle;
        DataScanner scanner = DataScanner.Create(region, Plugin.Instance.LoggerFactory.CreateLogger("Scanner"));

        //
        // GameUnitManager: Is specified for the damage handler found when walking up the callstack of the c_game_unit_melee event
        //
        if (!scanner
            .Scan("48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B CB E8 ?? ?? ?? ?? 48 8B CB E8 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8D 0D")
            .TryReadRelativeAddress(out GameUnitManagerVA))
        {
            LogHelper.Error($"Could not find GameUnitManager");
        }
        LogHelper.Information($"GameUnitManager found at VA: {GameUnitManagerVA.ToString("X16")}");

        //
        // gRangedBoltDamageTableRVA: Can be found referenced within c_game_unit_takedamage_ranged
        //
        if (!scanner
            .Scan("42 8B 94 82 ?? ?? ?? ?? 41 83 F8")
            .TryReadDisplacement(out RangedBoltDamageTableRVA))
        {
            LogHelper.Error($"Could not find gRangedBoltDamageTableRVA");
        }
        LogHelper.Information($"gRangedBoltDamageTableRVA found at RVA: {RangedBoltDamageTableRVA.ToString("X16")}");

        //
        // gRangedJavelinDamageTableRVA: Can be found referenced within c_game_unit_takedamage_ranged
        //
        if (!scanner
            .Scan("42 8B 94 82 ?? ?? ?? ?? 48 8B 5C 24 ?? 41 83 F8 ?? 0F 85")
            .TryReadDisplacement(out RangedJavelinDamageTableRVA))
        {
            LogHelper.Error($"Could not find gRangedJavelinDamageTableRVA");
        }
        LogHelper.Information($"gRangedJavelinDamageTableRVA found at RVA: {RangedJavelinDamageTableRVA.ToString("X16")}");

        //
        // gRangedArrowDamageTableRVA: Can be found referenced within c_game_unit_takedamage_ranged
        //
        if (!scanner
            .Scan("42 8B 94 82 ?? ?? ?? ?? 48 8B 5C 24 ?? 41 83 F8 ?? 75")
            .TryReadDisplacement(out RangedArrowDamageTableRVA))
        {
            LogHelper.Error($"Could not find gRangedArrowDamageTableRVA");
        }
        LogHelper.Information($"gRangedArrowDamageTableRVA found at RVA: {RangedArrowDamageTableRVA.ToString("X16")}");

        //
        // gRangedSlingerDamageTableRVA: Can be found referenced within c_game_unit_takedamage_ranged
        //
        if (!scanner
            .Scan("42 8B 94 82 ?? ?? ?? ?? EB")
            .TryReadDisplacement(out RangedSlingerDamageTableRVA))
        {
            LogHelper.Error($"Could not find gRangedSlingerDamageTableRVA");
        }
        LogHelper.Information($"gRangedSlingerDamageTableRVA found at RVA: {RangedSlingerDamageTableRVA.ToString("X16")}");

        //
        // gMeleeDamageTableRVA: Can be found referenced within c_game_unit_takedamage_melee
        //
        if (!scanner
            .Scan("45 8B BC 90 ?? ?? ?? ?? 33 DB")
            .TryReadDisplacement(out MeleeDamageTableRVA))
        {
            LogHelper.Error($"Could not find gMeleeDamageTableRVA");
        }
        LogHelper.Information($"gMeleeDamageTableRVA found at RVA: {MeleeDamageTableRVA.ToString("X16")}");

        //
        // gMeleeEunuchAOEDamageTableRVA
        //
        if (!scanner
            .Scan("8B 84 81 ?? ?? ?? ?? 74")
            .TryReadDisplacement(out MeleeEunuchAOEDamageTableRVA))
        {
            LogHelper.Error($"Could not find gMeleeEunuchAOEDamageTableRVA");
        }
        LogHelper.Information($"gMeleeEunuchAOEDamageTableRVA found at RVA: {MeleeEunuchAOEDamageTableRVA.ToString("X16")}");

        //
        // gBallistaDamageDefault: Can be found within c_game_unit_takedamage_projectile
        //
        if (!scanner
            .Scan("BB ?? ?? ?? ?? 41 0F 44 DA")
            .TryGetManagedImmediate(out BallistaDamageDefault, operand: 1))
        {
            LogHelper.Error($"Could not find gBallistaDamageDefault");
        }
        _managedAssemblyStateManager.Register("gBallistaDamageDefault", BallistaDamageDefault);
        LogHelper.Information($"gBallistaDamageDefault: {BallistaDamageDefault.GetValue()}");

        //
        // gBallistaDamageToPortableShields: Can be found within c_game_unit_takedamage_projectile
        //
        if (!scanner
            .Scan("BB ?? ?? ?? ?? EB ? 41 83 F8 ?? 75 ?? BB ?? ?? ?? ?? EB ?? 41 83 F8 ?? 75 ?? BB ?? ?? ?? ?? EB ?? 41 83 F8 ?? 75 ?? BB ?? ?? ?? ?? EB ?? 41 83 F8 ?? 75 ?? BB ?? ?? ?? ?? EB ?? 41 83 F8 ?? 75 ?? BB")
            .TryGetManagedImmediate(out BallistaDamageToPortableShields, operand: 1))
        {
            LogHelper.Error($"Could not find gBallistaDamageToPortableShields");
        }
        _managedAssemblyStateManager.Register("gBallistaDamageToPortableShields", BallistaDamageToPortableShields);
        LogHelper.Information($"gBallistaDamageToPortableShields: {BallistaDamageToPortableShields.GetValue()}");

        //
        // gBallistaDamageToBallista: Can be found within c_game_unit_takedamage_projectile
        //
        if (!scanner
            .Scan("BB ?? ?? ?? ?? EB ?? 41 83 F8 ?? 75 ?? BB ?? ?? ?? ?? EB ?? 41 83 F8 ?? 75 ?? BB ?? ?? ?? ?? EB ?? 41 83 F8 ?? 75 ?? BB ?? ?? ?? ?? EB ?? 41 83 F8 ?? 75 ?? BB ?? ?? ?? ?? EB ?? 41 83 F8 ?? 75 ?? 41 8B DA")
            .TryGetManagedImmediate(out BallistaDamageToBallista, operand: 1))
        {
            LogHelper.Error($"Could not find gBallistaDamageToBallista");
        }
        _managedAssemblyStateManager.Register("gBallistaDamageToBallista", BallistaDamageToBallista);
        LogHelper.Information($"gBallistaDamageToBallista: {BallistaDamageToBallista.GetValue()}");

        //
        // gBallistaDamageToMangonel: Can be found within c_game_unit_takedamage_projectile
        //
        if (!scanner
            .Scan("BB ?? ?? ?? ?? EB ?? 41 83 F8 ?? 75 ?? BB ?? ?? ?? ?? EB ?? 41 83 F8 ?? 75 ?? BB ?? ?? ?? ?? EB ?? 41 83 F8 ?? 75 ?? BB ?? ?? ?? ?? EB ?? 41 83 F8 ?? 75 ?? 41 8B DA")
            .TryGetManagedImmediate(out BallistaDamageToMangonel, operand: 1))
        {
            LogHelper.Error($"Could not find gBallistaDamageToMangonel");
        }
        _managedAssemblyStateManager.Register("BallistaDamageToMangonel", BallistaDamageToMangonel);
        LogHelper.Information($"gBallistaDamageToMangonel: {BallistaDamageToMangonel.GetValue()}");

        //
        // gBallistaDamageToArabBallista: Can be found within c_game_unit_takedamage_projectile
        //
        if (!scanner
            .Scan("BB ?? ?? ?? ?? EB ?? 41 83 F8 ?? 75 ?? BB ?? ?? ?? ?? EB ?? 41 83 F8 ?? 75 ?? BB ?? ?? ?? ?? EB ?? 41 83 F8 ?? 75 ?? 41 8B DA")
            .TryGetManagedImmediate(out BallistaDamageToArabBallista, operand: 1))
        {
            LogHelper.Error($"Could not find gBallistaDamageToArabBallista");
        }
        _managedAssemblyStateManager.Register("gBallistaDamageToArabBallista", BallistaDamageToArabBallista);
        LogHelper.Information($"gBallistaDamageToArabBallista: {BallistaDamageToArabBallista.GetValue()}");

        //
        // gBallistaDamageToCatapult: Can be found within c_game_unit_takedamage_projectile
        //
        if (!scanner
            .Scan("BB ?? ?? ?? ?? EB ?? 41 83 F8 ?? 75 ?? BB ?? ?? ?? ?? EB ?? 41 83 F8 ?? 75 ?? 41 8B DA")
            .TryGetManagedImmediate(out BallistaDamageToCatapult, operand: 1))
        {
            LogHelper.Error($"Could not find gBallistaDamageToCatapult");
        }
        _managedAssemblyStateManager.Register("gBallistaDamageToCatapult", BallistaDamageToCatapult);
        LogHelper.Information($"gBallistaDamageToCatapult: {BallistaDamageToCatapult.GetValue()}");

        //
        // gBallistaDamageToTrebutchet: Can be found within c_game_unit_takedamage_projectile
        //
        if (!scanner
            .Scan("BB ?? ?? ?? ?? EB ?? 41 83 F8 ?? 75 ?? 41 8B DA")
            .TryGetManagedImmediate(out BallistaDamageToTrebutchet, operand: 1))
        {
            LogHelper.Error($"Could not find gBallistaDamageToTrebutchet");
        }
        _managedAssemblyStateManager.Register("gBallistaDamageToTrebutchet", BallistaDamageToTrebutchet);
        LogHelper.Information($"gBallistaDamageToTrebutchet: {BallistaDamageToTrebutchet.GetValue()}");

        //
        // gBallistaDamageToBatteringRam: Can be found within c_game_unit_takedamage_projectile
        //
        if (!scanner
            .Scan("41 BA ?? ?? ?? ?? 66 41 83 FF")
            .TryGetManagedImmediate(out BallistaDamageToBatteringRamAndSiegeTower, operand: 1))
        {
            LogHelper.Error($"Could not find gBallistaDamageToBatteringRam");
        }
        _managedAssemblyStateManager.Register("gBallistaDamageToBatteringRamAndSiegeTower", BallistaDamageToBatteringRamAndSiegeTower);
        LogHelper.Information($"gBallistaDamageToBatteringRam: {BallistaDamageToBatteringRamAndSiegeTower.GetValue()}");

        //
        // gMaxMana: Can be found within c_game_player_regenerate_mana_and_stuff
        //
        if (!scanner
            .Scan("41 81 F9 ? ? ? ? 7D ? 81 3D")
            .TryGetManagedImmediate(out MaxMana, operand: 1))
        {
            LogHelper.Error($"Could not find gMaxMana");
        }
        _managedAssemblyStateManager.Register("gMaxMana", MaxMana);
        LogHelper.Information($"gMaxMana: {MaxMana.GetValue()}");

        //
        // gManaRegenAmount: Can be found within c_game_player_regenerate_mana_and_stuff
        //
        if (!scanner
         .Scan("41 8D 41 ? 41 89 80")
         .TryGetManagedDisplacement(out ManaRegenAmount, operand: 1))
        {
            LogHelper.Error("Could not find gManaRegenAmount.");
        }
        _managedAssemblyStateManager.Register("gManaRegenAmount", ManaRegenAmount);
        LogHelper.Information($"gManaRegenAmount: {ManaRegenAmount.GetValue()}");

        //
        // gMacemanMinDistanceToEnemyToRun: Can be found within c_game_unit_maceman_update
        //
        if (!scanner
         .Scan("B8 ? ? ? ? 66 42 39 84 13 ? ? ? ? 7E ? 45 85 F6")
         .TryGetManagedImmediate(out MacemanMinDistanceToEnemyToRun, operand: 1))
        {
            LogHelper.Error("Could not find gMacemanMinDistanceToEnemyToRun.");
        }
        _managedAssemblyStateManager.Register("gMacemanMinDistanceToEnemyToRun", MacemanMinDistanceToEnemyToRun);
        LogHelper.Information($"gMacemanMinDistanceToEnemyToRun: {MacemanMinDistanceToEnemyToRun.GetValue()}");

        //
        // gMinimumKeepPCLTilesToBeEnclosed: Can be found within c_game_ai_is_keep_siegeable
        // disabled due to Fixes inline hook.
        //
        //
        //if (!scanner
        //.Scan("41 81 F8 ? ? ? ? 7C ? 8B 05")
        //.TryGetManagedImmediate(out MinimumKeepPCLTilesToBeEnclosed, operand: 1))
        //{
        //    LogHelper.Error("Could not find gMinimumKeepPCLTilesToBeEnclosed.");
        //}
        //_managedAssemblyStateManager.Register("gMinimumKeepPCLTilesToBeEnclosed", MinimumKeepPCLTilesToBeEnclosed);
        //LogHelper.Information($"gMinimumKeepPCLTilesToBeEnclosed: {MinimumKeepPCLTilesToBeEnclosed.GetValue()}");

        //
        // gUnitHealthTable: Can be found referenced within c_game_unit_init: 48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 49 63 F0
        //
        if (!scanner
            .Scan("45 8B 84 86 ?? ?? ?? ?? 85 D2")
            .TryReadDisplacement(out UnitHealthTableRVA))
        {
            LogHelper.Error($"Could not find gUnitHealthTable");
        }
        LogHelper.Information($"gUnitHealthTable found at RVA: {UnitHealthTableRVA.ToString("X16")}");

        //
        // gBuildingHealthTableRVA: Can be found referenced within c_game_building_spawn: 41 0F B7 84 9F ? ? ? ? 66 43 89 84 2E ? ? ? ? 66 43 89 84 2E
        //
        if (!scanner
            .Scan("41 0F B7 84 9C ? ? ? ? 66 43 89 84 2E ? ? ? ? 66 43 89 84 2E")
            .TryReadDisplacement(out BuildingHealthTableRVA))
        {
            LogHelper.Error($"Could not find gBuildingHealthTable");
        }
        LogHelper.Information($"gBuildingHealthTable found at RVA: {BuildingHealthTableRVA.ToString("X16")}");

        //
        // gBuildingHousingPopulatonSpaceTable: Can be found referenced within c_game_building_spawn: 41 0F B7 84 9F ? ? ? ? 66 43 89 84 2E ? ? ? ? 41 0F B7 84 9F ? ? ? ? 66 43 89 84 2E ? ? ? ? 41 0F B7 84 9F ? ? ? ? 66 43 89 84 2E ? ? ? ? 41 0F B7 84 9F
        //
        if (!scanner
            .Scan("41 0F B7 84 9C ? ? ? ? 66 43 89 84 2E ? ? ? ? 41 0F B7 84 9C ? ? ? ? 66 43 89 84 2E ? ? ? ? 41 0F B7 84 9C ? ? ? ? 66 43 89 84 2E ? ? ? ? 41 0F B7 84 9C")
            .TryReadDisplacement(out BuildingHousingPopulatonSpaceTableRVA))
        {
            LogHelper.Error($"Could not find gBuildingHousingPopulatonSpaceTable");
        }
        LogHelper.Information($"gBuildingHousingPopulatonSpaceTable found at RVA: {BuildingHousingPopulatonSpaceTableRVA.ToString("X16")}");

        //
        // gSpeedTable: Can be found referenced within c_game_unit_init
        //
        if (!scanner
            .Scan("41 0F B7 84 BE ?? ?? ?? ?? 66 89 83 ?? ?? ?? ?? 66 89 83 ?? ?? ?? ?? 48 89 AB")
            .TryReadDisplacement(out SpeedTableRVA))
        {
            LogHelper.Error($"Could not find gSpeedTable");
        }
        LogHelper.Information($"gSpeedTable found at RVA: {SpeedTableRVA.ToString("X16")}");

        //
        // gMapRowLookupTable: Can be found referenced within c_game_build_wall (unkWallEntry = &gMapRowLookupTable[3 * wall_y_copy3];)
        //
        if (!scanner
            .Scan("48 8D 91 ?? ?? ?? ?? 48 8D 14 82")
            .TryReadDisplacement(out MapRowLookupTableRVA))
        {
            LogHelper.Error($"Could not find gMapRowLookupTable");
        }
        LogHelper.Information($"gMapRowLookupTable found at RVA: {MapRowLookupTableRVA.ToString("X16")}");

        //
        // gMapColumnLookupTableRVA: Can be found referenced within c_game_bulldoze_wall
        //
        if (!scanner
            .Scan("4D 0F BF B4 7F")
            .TryReadDisplacement(out MapColumnLookupTableRVA))
        {
            LogHelper.Error($"Could not find gMapColumnLookupTableRVA");
        }
        LogHelper.Information($"gMapColumnLookupTableRVA found at RVA: {MapColumnLookupTableRVA.ToString("X16")}");

        //
        // gMapBuildingManager: Can be found referenced within sub_180010F70 : return c_game_build_wall(gMapBuildingManager
        // func: 40 53 48 83 EC ?? 8B 05 ?? ?? ?? ?? C7 05 ?? ?? ?? ?? ?? ?? ?? ?? 83 F8 ?? 0F 85 ?? ?? ?? ?? 33 DB 44 8D 40 ?? 44 8B C8 89 5C 24 ?? 48 8D 15 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 44 8D 4B ?? 89 5C 24 ?? 44 8D 43 ?? 48 8D 15 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 44 8D 4B ?? 89 5C 24 ?? 44 8D 43 ?? 48 8D 15 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 44 8D 4B
        //
        if (!scanner
            .Scan("48 8D 0D ?? ?? ?? ?? 66 83 FE")
            .TryReadDisplacement(out GameBuildingManagerVA))
        {
            LogHelper.Error($"Could not find gMapBuildingManager");
        }
        LogHelper.Information($"gMapBuildingManager found at RVA: {GameBuildingManagerVA.ToString("X16")}");

        //
        // gNextUnitId: Can be found referenced within __int64 __fastcall sub_180174790(__int64 pGameUnitManager)
        // func: 40 55 48 83 EC ?? 48 89 5C 24
        //
        //if (!AOBUtil.TryFindAndReadDisplacementValue(memory, "8B 15 ?? ?? ?? ?? 48 8B CB E8 ?? ?? ?? ?? 41 B9", out NextUnitIdVA))
        //{
        //    LogHelper.Error($"Could not find gNextUnitId");
        //}
        //LogHelper.Information($"gNextUnitId found at RVA: {NextUnitIdVA.ToString("X16")}");

        //
        // gMaxRandomLions: Can be found referenced within c_game_editor_place_animal
        //
        if (!scanner
            .Scan("83 C3 ? E9 ? ? ? ? 81 FE ? ? ? ? 75 ? 48 63 C1 41 F7 84 81 ? ? ? ? ? ? ? ? 0F 85 ? ? ? ? 48 69 CF ? ? ? ? B8 ? ? ? ? 66 89 44 29 ? 44 8D 68 ? 0F BF 1D ? ? ? ? 83 E3 ? 83 C3 ? E9 ? ? ? ? 81 FE ? ? ? ? 75 ? 48 63 C1 41 F7 84 81 ? ? ? ? ? ? ? ? 0F 85 ? ? ? ? 48 69 CF ? ? ? ? B8 ? ? ? ? 66 89 44 29 ? 44 8D 68 ? 0F BF 1D ? ? ? ? 83 E3 ? 83 C3 ? EB")
            .TryGetManagedImmediate(out MaxRandomLions, operand: 1))
        {
            LogHelper.Error($"Could not find gMaxRandomLions");
        }
        _managedAssemblyStateManager.Register("gMaxRandomLions", MaxRandomLions);
        LogHelper.Information($"gMaxRandomLions found at VA: {MaxRandomLions.InstructionAddress.ToString("X16")}");
        //MaxRandomLions.SetValue(2);
        LogHelper.Information($"gMaxRandomLions: {MaxRandomLions.GetValue()}");

        //
        // gGameTribeManagerVA: Can be found within: c_game_unload_map
        if (!scanner
            .Scan("48 8D 0D ? ? ? ? E8 ? ? ? ? 33 D2 48 8D 0D ? ? ? ? E8 ? ? ? ? 48 8D 0D ? ? ? ? E8 ? ? ? ? 48 8D 0D ? ? ? ? E8 ? ? ? ? 48 8D 0D ? ? ? ? E8 ? ? ? ? 48 8D 0D ? ? ? ? E8 ? ? ? ? 48 8D 0D")
            .TryReadDisplacement(out GameTribeManagerVA))
        {
            LogHelper.Error($"Could not find gGameTribeManagerVA");
        }
        LogHelper.Information($"gGameTribeManagerVA found at RVA: {GameTribeManagerVA.ToString("X16")}");

        // gPlayerManager: Can be found within: c_game_update_player_resource_stats
        if (!scanner
            .Scan("4C 8D 35 ? ? ? ? 48 63 FA")
            .TryReadDisplacement(out GamePlayerManagerVA))
        {
            LogHelper.Error($"Could not find gPlayerManager");
        }
        LogHelper.Information($"gPlayerManager found at RVA: {GamePlayerManagerVA.ToString("X16")}");

        //
        // gBuildingCostDefaults: Can be found referenced within c_game_building_load_default_costs
        //
        if (!scanner
            .Scan("48 8D 05 ? ? ? ? 48 89 1C 24")
            .TryReadDisplacement(out BuildingDefaultCostsTableVA))
        {
            LogHelper.Error($"Could not find gBuildingCostDefaults");
        }
        LogHelper.Information($"gBuildingCostDefaults found at VA: {BuildingDefaultCostsTableVA.ToString("X16")}");

        //
        // gBuildingCostDefaultsEnd: Can be found referenced within c_game_building_load_default_costs
        //
        if (!scanner
            .Scan("48 8D 1D ? ? ? ? 48 8D 91")
            .TryReadDisplacement(out BuildingDefaultCostsTableEndVA))
        {
            LogHelper.Error($"Could not find gBuildingCostDefaultsEnd");
        }
        LogHelper.Information($"gBuildingCostDefaultsEnd found at VA: {BuildingDefaultCostsTableEndVA.ToString("X16")}");

        //
        // gBuildingStoneCostsTable: Can be found referenced within c_game_player_pay_for_building
        //
        if (!scanner
            .Scan("44 8B A4 88 ? ? ? ? 8B 94 88")
            .TryReadDisplacement(out BuildingStoneCostsTableRVA))
        {
            LogHelper.Error($"Could not find gBuildingStoneCostsTable");
        }
        LogHelper.Information($"gBuildingStoneCostsTable found at RVA: {BuildingStoneCostsTableRVA.ToString("X16")}");

        //
        // gBuildingRawPitchCostsTable: Can be found referenced within c_game_player_pay_for_building
        //
        if (!scanner
            .Scan("8B 94 88 ? ? ? ? 44 8B AC 88")
            .TryReadDisplacement(out BuildingRawPitchCostsTableRVA))
        {
            LogHelper.Error($"Could not find gBuildingRawPitchCostsTable");
        }
        LogHelper.Information($"gBuildingRawPitchCostsTable found at RVA: {BuildingRawPitchCostsTableRVA.ToString("X16")}");

        //
        // gBuildingGoldCostsTable: Can be found referenced within c_game_player_pay_for_building
        //
        if (!scanner
            .Scan("44 8B AC 88 ? ? ? ? 44 8B 8C 88")
            .TryReadDisplacement(out BuildingGoldCostsTableRVA))
        {
            LogHelper.Error($"Could not find gBuildingGoldCostsTable");
        }
        LogHelper.Information($"gBuildingGoldCostsTable found at RVA: {BuildingGoldCostsTableRVA.ToString("X16")}");

        //
        // gBuildingWoodCostsTable: Can be found referenced within c_game_player_pay_for_building
        //
        if (!scanner
            .Scan("44 8B 8C 88 ? ? ? ? 44 8B B4 88")
            .TryReadDisplacement(out BuildingWoodCostsTableRVA))
        {
            LogHelper.Error($"Could not find gBuildingWoodCostsTable");
        }
        LogHelper.Information($"gBuildingWoodCostsTable found at RVA: {BuildingWoodCostsTableRVA.ToString("X16")}");

        //
        // gBuildingIronIngotsCostsTable: Can be found referenced within c_game_player_pay_for_building
        //
        if (!scanner
            .Scan("44 8B B4 88")
            .TryReadDisplacement(out BuildingIronIngotsCostsTableRVA))
        {
            LogHelper.Error($"Could not find gBuildingIronIngotsCostsTable");
        }
        LogHelper.Information($"gBuildingIronIngotsCostsTable found at RVA: {BuildingIronIngotsCostsTableRVA.ToString("X16")}");

        //
        // gMaxGoodYards: Can be found referenced within c_game_player_build_placement_validator
        //
        if (!scanner
            .Scan("83 F8 ?? 0F 8D ?? ?? ?? ?? C7 44 24 ?? ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? 45 8B CE 44 89 64 24 ?? 44 8B C6 8B D5 E8 ?? ?? ?? ?? 85 C0 0F 85 ?? ?? ?? ?? C7 83 ?? ?? ?? ?? ?? ?? ?? ?? C7 83 ?? ?? ?? ?? ?? ?? ?? ?? E9 ?? ?? ?? ?? 66 83 FA ?? 75 ?? 48 69 CD ?? ?? ?? ?? 48 8D 05 ?? ?? ?? ?? 39 BC 01 ?? ?? ?? ?? 0F 84 ?? ?? ?? ?? 41 B8 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? 8B D5 E8 ?? ?? ?? ?? 83 F8 ?? 0F 8D ?? ?? ?? ?? C7 44 24 ?? ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? 45 8B CE 44 89 64 24 ?? 44 8B C6 8B D5 E8 ?? ?? ?? ?? 85 C0 0F 85 ?? ?? ?? ?? C7 83 ?? ?? ?? ?? ?? ?? ?? ?? C7 83 ?? ?? ?? ?? ?? ?? ?? ?? E9 ?? ?? ?? ?? 66 83 FA")
            .TryGetManagedImmediate(out MaxGoodYards, operand: 1))
        {
            LogHelper.Error($"Could not find MaxGoodYards");
        }
        _managedAssemblyStateManager.Register("gMaxGoodYards", MaxGoodYards);
        LogHelper.Information($"gMaxGoodYards found at VA: {MaxGoodYards.InstructionAddress.ToString("X16")}");
        //MaxGoodYards.SetValue(64);
        LogHelper.Information($"gMaxGoodYards: {MaxGoodYards.GetValue()}");

        //
        // gMaxArmories: Can be found referenced within c_game_player_build_placement_validator
        //
        if (!scanner
            .Scan("83 F8 ?? 0F 8D ?? ?? ?? ?? C7 44 24 ?? ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? 45 8B CE 44 89 64 24 ?? 44 8B C6 8B D5 E8 ?? ?? ?? ?? 85 C0 0F 85 ?? ?? ?? ?? C7 83 ?? ?? ?? ?? ?? ?? ?? ?? C7 83 ?? ?? ?? ?? ?? ?? ?? ?? E9 ?? ?? ?? ?? 8D 42")
            .TryGetManagedImmediate(out MaxArmories, operand: 1))
        {
            LogHelper.Error($"Could not find MaxArmories");
        }
        _managedAssemblyStateManager.Register("gMaxArmories", MaxArmories);
        LogHelper.Information($"gMaxArmories found at VA: {MaxArmories.InstructionAddress.ToString("X16")}");
        //MaxArmories.SetValue(64);
        LogHelper.Information($"gMaxArmories: {MaxArmories.GetValue()}");

        //
        // gMaxGranaries: Can be found referenced within c_game_player_build_placement_validator
        //
        if (!scanner
            .Scan("83 F8 ?? 0F 8D ?? ?? ?? ?? C7 44 24 ?? ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? 45 8B CE 44 89 64 24 ?? 44 8B C6 8B D5 E8 ?? ?? ?? ?? 85 C0 0F 85 ?? ?? ?? ?? C7 83 ?? ?? ?? ?? ?? ?? ?? ?? C7 83 ?? ?? ?? ?? ?? ?? ?? ?? E9 ?? ?? ?? ?? 66 83 FA ?? 75 ?? 48 69 CD ?? ?? ?? ?? 48 8D 05 ?? ?? ?? ?? 39 BC 01 ?? ?? ?? ?? 0F 84 ?? ?? ?? ?? 41 B8 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? 8B D5 E8 ?? ?? ?? ?? 83 F8 ?? 0F 8D ?? ?? ?? ?? C7 44 24 ?? ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? 45 8B CE 44 89 64 24 ?? 44 8B C6 8B D5 E8 ?? ?? ?? ?? 85 C0 0F 85 ?? ?? ?? ?? C7 83 ?? ?? ?? ?? ?? ?? ?? ?? C7 83 ?? ?? ?? ?? ?? ?? ?? ?? E9 ?? ?? ?? ?? 8D 42")
            .TryGetManagedImmediate(out MaxGranaries, operand: 1))
        {
            LogHelper.Error($"Could not find MaxGranaries");
        }
        _managedAssemblyStateManager.Register("gMaxGranaries", MaxGranaries);
        LogHelper.Information($"gMaxGranaries found at VA: {MaxGranaries.InstructionAddress.ToString("X16")}");
        //MaxGranaries.SetValue(64);
        LogHelper.Information($"gMaxGranaries: {MaxGranaries.GetValue()}");

        //
        // gGateHouseCloseDistance: can be found referenced within c_game_gatehouse_handler
        //
        if (!scanner
            .Scan("41 81 F8 ?? ?? ?? ?? 7D ?? B8 ?? ?? ?? ?? EB ?? 41 81 F8 ?? ?? ?? ?? 7C ?? 48 8D 2D")
            .TryGetManagedImmediate(out GateHouseCloseDistance, operand: 1))
        {
            LogHelper.Error($"Could not find gGateHouseCloseDistance");
        }
        _managedAssemblyStateManager.Register("gGateHouseCloseDistance", GateHouseCloseDistance);
        LogHelper.Information($"gGateHouseCloseDistance found at VA: {GateHouseCloseDistance.InstructionAddress.ToString("X16")}");
        LogHelper.Information($"gGateHouseCloseDistance: {GateHouseCloseDistance.GetValue()}");

        //
        // GateHouseReOpenDistance:
        //
        if (!scanner
            .Scan("B8 ?? ?? ?? ?? EB ?? 41 81 F8 ?? ?? ?? ?? 7C ?? 48 8D 2D")
            .TryGetManagedImmediate(out GateHouseReOpenDistance, operand: 1))
        {
            LogHelper.Error($"Could not find gGateHouseReOpenDistance");
        }
        _managedAssemblyStateManager.Register("gGateHouseReOpenDistance", GateHouseReOpenDistance);
        LogHelper.Information($"gGateHouseReOpenDistance found at VA: {GateHouseReOpenDistance.InstructionAddress.ToString("X16")}");
        LogHelper.Information($"gGateHouseReOpenDistance: {GateHouseReOpenDistance.GetValue()}");

        //
        // gGateHouseUnkDistance:
        //
        if (!scanner
            .Scan("41 81 F8 ?? ?? ?? ?? 7C ?? 48 8D 2D")
            .TryGetManagedImmediate(out GateHouseUnkDistance, operand: 1))
        {
            LogHelper.Error($"Could not find gGateHouseUnkDistance");
        }
        _managedAssemblyStateManager.Register("gGateHouseUnkDistance", GateHouseUnkDistance);
        LogHelper.Information($"gGateHouseUnkDistance found at VA: {GateHouseUnkDistance.InstructionAddress.ToString("X16")}");
        LogHelper.Information($"gGateHouseUnkDistance: {GateHouseReOpenDistance.GetValue()}");

        //
        // gPeasantRespawnTickTargetValue: Can be found referenced within c_game_player_update_peasant_respawn_tick
        //
        if (!scanner.TryGetMultiManagedImmediate(
            patterns: [
            "81 FA ? ? ? ? 7C ? C7 43",
            "C7 43 ? ? ? ? ? BA ? ? ? ? EB",
            "BA ? ? ? ? EB ? 85 D2 79",
            "81 FA ? ? ? ? 0F 8C ? ? ? ? 8B 43"
            ],
            operands:
            [
                1,
                1,
                1,
                1
            ],
            out PeasantRespawnTickTargetValue))
        {
            LogHelper.Error($"Could not find gPeasantRespawnTickTargetValue");
        }
        _managedAssemblyStateManager.Register("gPeasantRespawnTickTargetValue", PeasantRespawnTickTargetValue);
        LogHelper.Information($"Peasant Respawn Tick Target Value: {PeasantRespawnTickTargetValue.GetValue()}");

        //
        // gPeasantRespawnTickResetValue: Can be found referenced within c_game_player_update_peasant_respawn_tick
        //
        if (!scanner.TryGetMultiManagedImmediate(
            patterns: [
            "C7 43 ? ? ? ? ? BA ? ? ? ? 44 39 73",
            "BA ? ? ? ? 44 39 73",
            "C7 43 ? ? ? ? ? 44 8B C7"
            ],
            operands:
            [
                1,
                1,
                1
            ],
            out PeasantRespawnTickResetValue))
        {
            LogHelper.Error($"Could not find gPeasantRespawnTickResetValue");
        }
        _managedAssemblyStateManager.Register("gPeasantRespawnTickResetValue", PeasantRespawnTickResetValue);
        LogHelper.Information($"Peasant Respawn Tick Reset Value: {PeasantRespawnTickResetValue.GetValue()}");

        //
        // gLocalPlayerId: Can be found in c_game_player_build_structure
        //
        if (!scanner
            .Scan("3B 3D ? ? ? ? 75 ? 48 8D 0D ? ? ? ? E8 ? ? ? ? 8B 6C 24")
            .TryReadDisplacement(out LocalPlayerIdVA))
        {
            LogHelper.Error($"Could not find gLocalPlayerId");
        }
        LogHelper.Information($"gLocalPlayerId found at VA: {LocalPlayerIdVA.ToString("X16")}");

        //
        // gElapsedMapTicksVA: Can be found in 48 89 5C 24 ? 57 48 83 EC ? 33 FF 48 8B D9 89 B9 ? ? ? ? B9
        //
        if (!scanner
            .Scan("8B 0D ? ? ? ? 89 8B")
            .TryReadDisplacement(out ElapsedMapTicksVA))
        {
            LogHelper.Error($"Could not find gElapsedMapTicksVA");
        }
        LogHelper.Information($"gElapsedMapTicksVA found at VA: {ElapsedMapTicksVA.ToString("X16")}");

        //
        // gCurrentlySelectedBuildingId: Can be found in DLL_SetAppMode
        //
        if (!scanner
            .Scan("48 89 05 ? ? ? ? 89 15")
            .TryReadDisplacement(out CurrentlySelectedBuildingIdVA, operandIndex: 0))
        {
            LogHelper.Error($"Could not find gCurrentlySelectedBuildingId");
        }
        LogHelper.Information($"gCurrentlySelectedBuildingId found at VA: {CurrentlySelectedBuildingIdVA.ToString("X16")}");

        //
        // gp_PathfindingContextVA: Can be found in c_game_player_state_handler
        //
        if (!scanner
            .Scan("48 8D 0D ? ? ? ? E8 ? ? ? ? FF C6 49 81 C6")
            .TryReadDisplacement(out PathfindingContextVA))
        {
            LogHelper.Error($"Could not find gp_PathfindingContextVA");
        }
        LogHelper.Information($"gp_PathfindingContextVA found at VA: {PathfindingContextVA.ToString("X16")}");

        //
        // gAIVDataTableVA: Can be found in DLL_ImportAIV
        //
        if (!scanner
            .Scan("48 8D 3D ? ? ? ? 49 03 D6")
            .TryReadDisplacement(out AIVDataTableVA))
        {
            LogHelper.Error($"Could not find gAIVDataTableVA");
        }
        LogHelper.Information($"gAIVDataTableVA found at VA: {AIVDataTableVA.ToString("X16")}");

        //
        // gSkirmishSpawnGoldTable
        //
        if (!scanner
            .Scan("8B 84 88 ?? ?? ?? ?? 0F 11 05")
            .TryReadDisplacement(out PlayerDefaultSkirmishSpawnGoldTable))
        {
            LogHelper.Error($"Could not find gSkirmishSpawnGoldTable");
        }
        LogHelper.Information($"gSkirmishSpawnGoldTable found at RVA: {PlayerDefaultSkirmishSpawnGoldTable.ToString("X16")}");

        //
        // gMonkAvailable: Can be found in c_game_playstatereturn_update
        //
        if (!scanner
            .Scan("0F B6 05 ? ? ? ? 88 83 ? ? ? ? E9 ? ? ? ? 48 63 05")
            .TryReadDisplacement(out MonkAvailableVA))
        {
            LogHelper.Error($"Could not find gMonkAvailable");
        }
        LogHelper.Information($"gMonkAvailable found at RVA: {MonkAvailableVA.ToString("X16")}");

        //
        // gEngineerAvailableVA: Can be found in c_game_playstatereturn_update
        //
        if (!scanner
            .Scan("0F B6 05 ? ? ? ? 88 83 ? ? ? ? 0F B6 05 ? ? ? ? 88 83 ? ? ? ? 8B 05")
            .TryReadDisplacement(out EngineerAvailableVA))
        {
            LogHelper.Error($"Could not find gEngineerAvailableVA");
        }
        LogHelper.Information($"gEngineerAvailableVA found at RVA: {EngineerAvailableVA.ToString("X16")}");

        //
        // gLaddermanAvailableVA: Can be found in c_game_playstatereturn_update
        //
        if (!scanner
            .Scan("0F B6 05 ? ? ? ? 88 83 ? ? ? ? 8B 05 ? ? ? ? 83 3D")
            .TryReadDisplacement(out LaddermanAvailableVA))
        {
            LogHelper.Error($"Could not find gLaddermanAvailableVA");
        }
        LogHelper.Information($"gLaddermanAvailableVA found at RVA: {LaddermanAvailableVA.ToString("X16")}");

        //
        // gPlayerDefaultSkirmishResourcesVA
        //
        if (!scanner
            .Scan("48 8D 15 ? ? ? ? FF C9")
            .TryReadDisplacement(out PlayerDefaultSkirmishResourcesVA))
        {
            LogHelper.Error($"Could not find gPlayerDefaultSkirmishResourcesVA");
        }
        LogHelper.Information($"gPlayerDefaultSkirmishResourcesVA found at VA: {PlayerDefaultSkirmishResourcesVA.ToString("X16")}");

        //
        // gPlayerDefaultSkirmishSettingsTableRVA
        // Seems to be a per-castle-instance AIV parse/working buffer of some kind, 28056 bytes per AI.
        //
        if (!scanner
            .Scan("48 8D 0D ? ? ? ? E8 ? ? ? ? 48 8B 85")
            .TryReadDisplacement(out AIVCastleLayoutTableRVA))
        {
            LogHelper.Error($"Could not find gAIVCastleLayoutTableRVA");
        }
        LogHelper.Information($"gAIVCastleLayoutTableRVA found at VA: {AIVCastleLayoutTableRVA.ToString("X16")}");

        //
        // gGameVegetationManager: Can be found referenced within c_game_create_tree_proximity_area
        //
        if (!scanner
            .Scan("4C 8D 35 ? ? ? ? 48 69 D8 ? ? ? ? 41 8B E8")
            .TryReadDisplacement(out GameVegetationManagerVA))
        {
            LogHelper.Error($"Could not find gGameVegetationManager");
        }
        LogHelper.Information($"gGameVegetationManager found at VA: {GameVegetationManagerVA.ToString("X16")}");

        //
        // gAILordManagerRVA: Can be found referenced within DLL_SetExtendedLordConfig
        //
        if (!scanner
            .Scan("4D 8D 8E ? ? ? ? BA")
            .TryReadDisplacement(out AILordManagerRVA))
        {
            LogHelper.Error($"Could not find gAILordManager");
        }
        LogHelper.Information($"gAILordManager found at RVA: {AILordManagerRVA.ToString("X16")} / VA: {((UInt64)CrusaderLibrary.Instance.LibraryModuleHandle + AILordManagerRVA).ToString("X16")}");

        //
        // gAIBoolPlayerListVA: Can be found referenced within DLL_GetMultiplayerChatInfo
        //
        if (!scanner
            .Scan("8B 05 ? ? ? ? 89 01 8B 05")
            .TryReadDisplacement(out AIBoolPlayerListVA))
        {
            LogHelper.Error($"Could not find gAIBoolPlayerListVA");
        }
        LogHelper.Information($"gAIBoolPlayerListVA found at VA: {AIBoolPlayerListVA.ToString("X16")}");

        //
        // AILineUpVA: Can be found referenced within c_game_init_trail
        //
        if (!scanner
            .Scan("48 8D 05 ? ? ? ? 41 BD")
            .TryReadDisplacement(out AILineUpVA))
        {
            LogHelper.Error($"Could not find gAILineUpVA");
        }
        LogHelper.Information($"gAILineUpVA found at VA: {AILineUpVA.ToString("X16")}");

        //
        // GameStateChoreHandlersVA: Can be found referenced within c_game_queue_chore
        //
        if (!scanner
            .Scan("4C 8D 25 ? ? ? ? 48 C7 83")
            .TryReadDisplacement(out GameStateChoreHandlersVA))
        {
            LogHelper.Error($"Could not find gGameStateChoreHandlersVA");
        }
        LogHelper.Information($"gGameStateChoreHandlersVA found at VA: {GameStateChoreHandlersVA.ToString("X16")}");

        //
        // gGamePausedVA: Can be found referenced within DLL_GameAction
        //
        if (!scanner
            .Scan("89 1D ?? ?? ?? ?? E9 ?? ?? ?? ?? 8B 05 ?? ?? ?? ?? 05")
            .TryReadDisplacement(out GamePausedVA, operandIndex: 0))
        {
            LogHelper.Error($"Could not find gGamePausedVA");
        }
        LogHelper.Information($"gGamePaused found at VA: {GamePausedVA.ToString("X16")}");

        //
        // gCurrentMapName
        //
        if (!scanner
            .Scan("48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 83 FB")
            .TryReadDisplacement(out CurrentMapNameVA, operandIndex: 1))
        {
            LogHelper.Error($"Could not find gCurrentMapName");
        }
        LogHelper.Information($"gCurrentMapName found at VA: {CurrentMapNameVA.ToString("X16")}");

        //
        // gCurrentMapNameLength
        //
        if (!scanner
            .Scan("89 15 ?? ?? ?? ?? 85 D2 7E ?? 4C 63 C2 48 8B D1 48 8D 0D ?? ?? ?? ?? E8")
            .TryReadDisplacement(out CurrentMapNameLengthVA, operandIndex: 0))
        {
            LogHelper.Error($"Could not find gCurrentMapNameLength");
        }
        LogHelper.Information($"gCurrentMapNameLength found at VA: {CurrentMapNameLengthVA.ToString("X16")}");

        //
        // gIsInMapEditorVA: Can be found referenced within DLL_SetDebugMode
        //
        if (!scanner
            .Scan("89 0D ?? ?? ?? ?? C3 83 F9 ?? 75 ?? 48 8D 05")
            .TryReadDisplacement(out IsInMapEditorVA, operandIndex: 0))
        {
            LogHelper.Error($"Could not find gIsInMapEditorVA");
        }
        LogHelper.Information($"gIsInMapEditorVA found at VA: {IsInMapEditorVA.ToString("X16")}");

        //
        // gTeamsListRVA: Can be found referenced within DLL_RegisterMultiplayerUser
        // 45 89 8C 80
        //
        if (!scanner
            .Scan("45 89 8C 80")
            .TryReadDisplacement(out TeamsListRVA, operandIndex: 0))
        {
            LogHelper.Error($"Could not find gTeamsListRVA");
        }
        LogHelper.Information($"gTeamsListRVA found at VA: {TeamsListRVA.ToString("X16")}");

        //
        // gTreeGrowthProgressionTableRVA: Can be found referenced within c_game_vegetation_growth_handler
        //
        if (!scanner
          .Scan("44 3B 8C 95")
          .TryReadDisplacement(out TreeGrowthProgressionTableRVA))
        {
            LogHelper.Error($"Could not find gTreeGrowthProgressionTableRVA");
        }
        LogHelper.Information($"gTreeGrowthProgressionTableRVA found at VA: {TreeGrowthProgressionTableRVA.ToString("X16")}");

        //
        // GameTileManagerVA: Can be found referenced within c_game_player_build_placement_handler
        //
        if (!scanner
          .Scan("48 8D 0D ?? ?? ?? ?? 89 44 24 ?? 0F B7 05")
          .TryReadDisplacement(out GameTileManagerVA))
        {
            LogHelper.Error($"Could not find gGameTileManagerVA");
        }
        LogHelper.Information($"gGameTileManagerVA found at VA: {GameTileManagerVA.ToString("X16")}");

        //
        // GameGatehouseManagerVA: Can be found referenced within c_game_gatehouse_state_handler
        //
        /*if (!scanner
          .Scan("48 8D 0D ?? ?? ?? ?? 89 44 24 ?? 0F B7 05\"")
          .TryReadDisplacement(out GameGatehouseManagerVA))
        {
            LogHelper.Error($"Could not find gGameTileManagerVA");
        }
        LogHelper.Information($"gGameGatehouseManagerVA found at VA: {GameGatehouseManagerVA.ToString("X16")}");
        */

        //
        // gGameGatehouseFunctionsVA
        //
        if (!scanner
          .Scan("48 8D 3D ? ? ? ? 48 89 81")
          .TryReadDisplacement(out GameGatehouseFunctionsVA))
        {
            LogHelper.Error($"Could not find gGameGatehouseFunctionsVA");
        } 
        else
        {
            GameGatehouseFunctionsVTable = new NativePointer<GatehouseFunctionsVTable>((IntPtr)GameGatehouseFunctionsVA);
            InstructionWalker walker = new((UInt64)GameGatehouseFunctionsVTable.Pointer->GatehouseSmallUpdate, new()
            {
                WalkRules =
                [
                    new InstructionWalkRule
                    {
                        InstructionSkip = 0,
                        StartFrom = InstructionWalkerStartMode.Begin,
                        Predicate = static (instr) =>
                        {
                            // Find: movsxd rax, dword ptr [r8+r10+DISPLACEMENT]
                            if (instr.Mnemonic != Mnemonic.Movsxd) return false;

                            // Writing to RAX
                            if (instr.Op0Register != Register.RAX) return false;

                            bool hasR8 = instr.MemoryBase == Register.R8 || instr.MemoryIndex == Register.R8;
                            bool hasR10 = instr.MemoryBase == Register.R10 || instr.MemoryIndex == Register.R10;

                            // Must have a non-zero displacement (the table offset we want)
                            return hasR8 && hasR10 && instr.MemoryDisplacement64 > 0;
                        },
                        Extractor = InstructionWalkerHelpers.ExtractDisplacement
                    }
                ]
            }, Plugin.Instance.LoggerFactory.CreateLogger("InstructionWalker"));
            if (!walker.TryWalk<UInt64>(out PathConnectionRecordManagerRVA))
            {
                LogHelper.Error($"Could not find GameGatehouseManagerRVA");
            }
            LogHelper.Information($"GameGatehouseManagerRVA found at RVA: {PathConnectionRecordManagerRVA.ToString("X16")} / VA {((UInt64)((UInt64)CrusaderLibrary.Instance.LibraryModuleHandle + PathConnectionRecordManagerRVA)).ToString("X16")}");
        }
        LogHelper.Information($"gGameGatehouseFunctionsVA found at VA: {GameGatehouseFunctionsVA.ToString("X16")}");

        //
        // gGameUnitFunctionsVA
        // call qword ptr ds:[r14+rax*8+0x31FC30]
        //
        if (!scanner
          .Scan("41 FF 94 C6 ? ? ? ? 8B 15")
          .TryReadDisplacement(out GameUnitFunctionsRVA, operandIndex: 0))
        {
            LogHelper.Error($"Could not find gGameUnitFunctionsVA");
        }
        else
        {
            GameUnitFunctionsVTable = new NativePointer<UnitFunctionsVTable>((IntPtr)(GameUnitFunctionsRVA + currentImageBase));
        }
        LogHelper.Information($"GameUnitFunctionsVTable found at VA: {(GameUnitFunctionsRVA + currentImageBase).ToString("X16")}");

        //
        // gGameBuildingFunctionsVA
        // call    (gBuildingFuncs - 180000000h)[r14+rax*8]
        //
        if (!scanner
          .Scan("41 FF 94 C6 ? ? ? ? 4C 63 05")
          .TryReadDisplacement(out GameBuildingFunctionsRVA, operandIndex: 0))
        {
            LogHelper.Error($"Could not find gGameBuildingFunctionsVA");
        }
        else
        {
            GameBuildingFunctionsVTable = new NativePointer<BuildingFunctionsVTable>((IntPtr)(GameBuildingFunctionsRVA + currentImageBase));
        }
        LogHelper.Information($"GameBuildingFunctionsVTable found at VA: {(GameBuildingFunctionsRVA + currentImageBase).ToString("X16")}");


        //
        // gLocalPlayerArmyCounts: Can be found referenced within c_game_count_player_army_units
        //
        if (!scanner
          .Scan("48 8D 3D ?? ?? ?? ?? 48 8B CF 33 D2")
          .TryReadDisplacement(out LocalPlayerArmyCountsVA))
        {
            LogHelper.Error($"Could not find gLocalPlayerArmyCounts");
        }
        LogHelper.Information($"gLocalPlayerArmyCounts found at VA: {LocalPlayerArmyCountsVA.ToString("X16")}");

        //
        // gPeasantSpawnRateIncrementsHighPopRVA: Can be found referenced within c_game_player_update_peasant_respawn_tick
        //
        if (!scanner
          .Scan("41 8B 8C 87 ? ? ? ? 89 0B")
          .TryReadDisplacement(out PeasantSpawnRateIncrementsHighPopRVA))
        {
            LogHelper.Error($"Could not find gPeasantSpawnRateIncrementsHighPopRVA");
        }
        LogHelper.Information($"gPeasantSpawnRateIncrementsHighPopRVA found at RVA: {PeasantSpawnRateIncrementsHighPopRVA.ToString("X16")}");

        //
        // gPeasantSpawnRateIncrementsLowPopRVA: Can be found referenced within c_game_player_update_peasant_respawn_tick
        //
        if (!scanner
          .Scan("41 8B 8C 87 ? ? ? ? EB")
          .TryReadDisplacement(out PeasantSpawnRateIncrementsLowPopRVA))
        {
            LogHelper.Error($"Could not find gPeasantSpawnRateIncrementsLowPopRVA");
        }
        LogHelper.Information($"gPeasantSpawnRateIncrementsLowPopRVA found at RVA: {PeasantSpawnRateIncrementsLowPopRVA.ToString("X16")}");

        //
        // gPeasantSpawnRateIncrementsDefaultsRVA: Can be found referenced within c_game_player_update_peasant_respawn_tick
        //
        if (!scanner
          .Scan("41 8B 94 87 ? ? ? ? 8B CA")
          .TryReadDisplacement(out PeasantSpawnRateIncrementsDefaultsRVA))
        {
            LogHelper.Error($"Could not find gPeasantSpawnRateIncrementsDefaultsRVA");
        }
        LogHelper.Information($"gPeasantSpawnRateIncrementsDefaultsRVA found at RVA: {PeasantSpawnRateIncrementsDefaultsRVA.ToString("X16")}");


        //
        // gBuildingAvailabilityManager: Can be found referenced within c_game_setup_tradegood_availability
        //
        if (!scanner
          .Scan("48 8D 0D ? ? ? ? E8 ? ? ? ? 44 8B EB")
          .TryReadDisplacement(out BuildingAvailabilityManager))
        {
            LogHelper.Error($"Could not find BuildingAvailabilityManagerVA");
        }
        LogHelper.Information($"BuildingAvailabilityManagerVA found at VA: {BuildingAvailabilityManager.ToString("X16")}");

        //
        // gMapRulesInfo_AllowedTradingGoodsRVA: Can be found referenced within c_game_setup_tradegood_availability
        //
        if (!scanner
          .Scan("42 89 8C 30 ?? ?? ?? ?? 8B 0A")
          .TryReadDisplacement(out MapRulesInfo_AllowedTradingGoodsRVA, operandIndex: 0))
        {
            LogHelper.Error($"Could not find gMapRulesInfo_AllowedTradingGoodsRVA");
        }
        LogHelper.Information($"gMapRulesInfo_AllowedTradingGoodsRVA found: {MapRulesInfo_AllowedTradingGoodsRVA.ToString("X16")}");

        //
        // gMapRulesInfo_AllowedEuroUnitsVA: Can be found referenced within c_game_setup_tradegood_availability
        //
        if (!scanner
          .Scan("89 1D ?? ?? ?? ?? 83 F9 ?? 89 1D")
          .TryReadDisplacement(out MapRulesInfo_AllowedEuroUnitsVA, operandIndex: 0))
        {
            LogHelper.Error($"Could not find gMapRulesInfo_AllowedEuroUnitsVA");
        }
        LogHelper.Information($"gMapRulesInfo_AllowedEuroUnitsVA found at VA: {MapRulesInfo_AllowedEuroUnitsVA.ToString("X16")}");

        //
        // gMapRulesInfo_AllowedArabUnitsVA: Can be found referenced within c_game_setup_tradegood_availability
        //
        if (!scanner
          .Scan("89 1D ? ? ? ? 41 8B C4")
          .TryReadDisplacement(out MapRulesInfo_AllowedArabUnitsVA, operandIndex: 0))
        {
            LogHelper.Error($"Could not find gMapRulesInfo_AllowedArabUnitsVA");
        }
        LogHelper.Information($"gMapRulesInfo_AllowedArabUnitsVA found at VA: {MapRulesInfo_AllowedArabUnitsVA.ToString("X16")}");

        //
        // gMapRulesInfo_AllowedBedouinUnitsVA: Can be found referenced within c_game_setup_tradegood_availability
        //
        if (!scanner
          .Scan("0F 11 05 ? ? ? ? 0F 11 05 ? ? ? ? 0F 11 0D ? ? ? ? 74")
          .TryReadDisplacement(out MapRulesInfo_AllowedBedouinUnitsVA, operandIndex: 0))
        {
            LogHelper.Error($"Could not find gMapRulesInfo_AllowedBedouinUnitsVA");
        }
        Log.Information($"gMapRulesInfo_AllowedBedouinUnitsVA found at VA: {MapRulesInfo_AllowedBedouinUnitsVA.ToString("X16")}");

        //
        // gCurrentSkirmishGameModeVA: Can be found referenced within c_game_init_trail
        //
        if (!scanner
          .Scan("C7 05 ? ? ? ? ? ? ? ? 83 F8 ? 0F 87")
          .TryReadDisplacement(out CurrentSkirmishGameModeVA, operandIndex: 0))
        {
            LogHelper.Error($"Could not find gCurrentSkirmishGameModeVA");
        }
        Log.Information($"gCurrentSkirmishGameModeVA found at VA: {CurrentSkirmishGameModeVA.ToString("X16")}");

        //
        // gCurrentTrailTypeVA: Can be found referenced within c_game_init_trail
        //
        if (!scanner
          .Scan("48 63 05 ? ? ? ? C7 05")
          .TryReadDisplacement(out CurrentTrailTypeVA, operandIndex: 1))
        {
            LogHelper.Error($"Could not find gCurrentTrailTypeVA");
        }
        Log.Information($"gCurrentTrailTypeVA found at VA: {CurrentTrailTypeVA.ToString("X16")}");

        //
        // gCurrentGameSkirmishModeVA: Can be found referenced within c_game_reset_player_incoming_resources
        //
        if (!scanner
          .Scan("8B 0D ? ? ? ? FF C9")
          .TryReadDisplacement(out CurrentGameSkirmishModeVA, operandIndex: 1))
        {
            LogHelper.Error($"Could not find gCurrentGameSkirmishModeVA");
        }
        Log.Information($"gCurrentGameSkirmishModeVA found at VA: {CurrentGameSkirmishModeVA.ToString("X16")}");

        //
        // CurrentGameTypeModeVA: Can be found referenced within c_game_attempt_recruit_required_troops
        //
        if (!scanner
          .Scan("83 3D ? ? ? ? ? 44 8D 50")
          .TryReadDisplacement(out CurrentGameTypeModeVA, operandIndex: 0))
        {
            LogHelper.Error($"Could not find gCurrentGameTypeModeVA");
        }
        Log.Information($"gCurrentGameTypeModeVA found at VA: {CurrentGameTypeModeVA.ToString("X16")}");

        //
        // gAIAdvantage: Can be found referenced within c_game_init_trail
        //
        if (!scanner
          .Scan("89 05 ? ? ? ? 8B 43 ? 89 05 ? ? ? ? 48 63 05")
          .TryReadDisplacement(out AIAdvantageVA, operandIndex: 0))
        {
            LogHelper.Error($"Could not find gAIAdvantage");
        }
        Log.Information($"gAIAdvantage found at VA: {AIAdvantageVA.ToString("X16")}");

        //
        // gCoopTrailIdVA: Can be found referenced within DLL_PreInitMap_Multiplayer
        //
        if (!scanner
          .Scan("89 05 ? ? ? ? 8B 44 24 ? 89 05 ? ? ? ? 0F B6 44 24")
          .TryReadDisplacement(out CoopTrailIdVA, operandIndex: 0))
        {
            LogHelper.Error($"Could not find gCoopTrailIdVA");
        }
        Log.Information($"gCoopTrailIdVA found at VA: {CoopTrailIdVA.ToString("X16")}");

        //
        // gCoopMissionIdVA: Can be found referenced within DLL_PreInitMap_Multiplayer
        //
        if (!scanner
          .Scan("89 05 ? ? ? ? 0F B6 44 24")
          .TryReadDisplacement(out CoopMissionIdVA, operandIndex: 0))
        {
            LogHelper.Error($"Could not find gCoopMissionIdVA");
        }
        Log.Information($"gCoopMissionIdVA found at VA: {CoopMissionIdVA.ToString("X16")}");

        //
        // gMapRulesInfo_AllowedProductionGoodsVA: Can be found referenced within c_game_start_game_handler
        //
        if (!scanner
          .Scan("89 1D ? ? ? ? 39 1D")
          .TryReadDisplacement(out MapRulesInfo_AllowedProductionGoodsVA, operandIndex: 0))
        {
            LogHelper.Error($"Could not find gMapRulesInfo_AllowedProductionGoodsVA");
        }
        LogHelper.Information($"gMapRulesInfo_AllowedProductionGoodsVA found at VA: {MapRulesInfo_AllowedProductionGoodsVA.ToString("X16")}");

        //
        // gGameCursorManagerVA: Can be found referenced within DLL_RunTick
        //
        if (!scanner
          .Scan("48 8D 0D ?? ?? ?? ?? 44 8B 84 24 ?? ?? ?? ?? 8B D3")
          .TryReadDisplacement(out GameCursorManagerVA))
        {
            LogHelper.Error($"Could not find gGameCursorManagerVA");
        }
        Log.Information($"gGameCursorManagerVA found at VA: {GameCursorManagerVA.ToString("X16")}");

        //
        // gFriendlyWallToEnemyWallProximityDistAllowance:
        //
        if (!scanner
          .Scan("C7 44 24 ?? ?? ?? ?? ?? E8 ?? ?? ?? ?? 85 C0 0F 85 ?? ?? ?? ?? 8B 94 24")
          .TryGetManagedImmediate(out FriendlyWallToEnemyWallProximityDistAllowance, operand: 1))
        {
            LogHelper.Error($"Could not find gFriendlyWallToEnemyWallProximityDistAllowance");
        }
        _managedAssemblyStateManager.Register("gFriendlyWallToEnemyWallProximityDistAllowance", FriendlyWallToEnemyWallProximityDistAllowance);
        LogHelper.Information($"gFriendlyWallToEnemyWallProximityDistAllowance found at VA: {FriendlyWallToEnemyWallProximityDistAllowance.InstructionAddress.ToString("X16")}");
        LogHelper.Information($"gFriendlyWallToEnemyWallProximityDistAllowance: {FriendlyWallToEnemyWallProximityDistAllowance.GetValue()}");

        //
        // gCurrentMapSize: Can be found referenced within DLL_SetMapEditorParam
        //
        if (!scanner
          .Scan("89 35 ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 8B 15")
          .TryReadDisplacement(out CurrentMapSizeVA, operandIndex: 0))
        {
            LogHelper.Error($"Could not find gCurrentMapSize");
        }
        LogHelper.Information($"gCurrentMapSize found at VA: {CurrentMapSizeVA.ToString("X16")}");

        //
        // gPlayerKeepIsEnclosedVA
        //
        if (!scanner
          .Scan("C7 05 ? ? ? ? ? ? ? ? 89 3D ? ? ? ? 8B C7")
          .TryReadDisplacement(out PlayerKeepIsEnclosedVA, operandIndex: 0))
        {
            LogHelper.Error($"Could not find gPlayerKeepIsEnclosedVA");
        }
        LogHelper.Information($"gPlayerKeepIsEnclosedVA found at VA: {PlayerKeepIsEnclosedVA.ToString("X16")}");

        //
        // gPlayerExtremePowersEnabledVA
        //
        if (!scanner
          .Scan("89 05 ? ? ? ? 8B 05 ? ? ? ? 89 05 ? ? ? ? 44 38 74 24")
          .TryReadDisplacement(out PlayerExtremePowersEnabledVA, operandIndex: 0))
        {
            LogHelper.Error($"Could not find gPlayerExtremePowersEnabledVA");
        }
        LogHelper.Information($"gPlayerExtremePowersEnabledVA found at VA: {PlayerExtremePowersEnabledVA.ToString("X16")}");

        //
        // gSoundManagerVA:
        //
        if (!scanner
          .Scan("48 8D 0D ? ? ? ? C7 05 ? ? ? ? ? ? ? ? E8 ? ? ? ? FF 15")
          .TryReadDisplacement(out GameSoundManagerVA, operandIndex: 1))
        {
            LogHelper.Error($"Could not find gSoundManagerVA");
        }
        LogHelper.Information($"gSoundManagerVA found at VA: {GameSoundManagerVA.ToString("X16")}");

        //
        // gProjectilesManagerVA:
        //
        if (!scanner
          .Scan("48 8D 0D ?? ?? ?? ?? 89 54 24 ?? 33 D2 E8 ?? ?? ?? ?? 4C 8B 9C 24")
          .TryReadDisplacement(out GameProjectilesManagerVA, operandIndex: 1))
        {
            LogHelper.Error($"Could not find gProjectilesManagerVA");
        }
        LogHelper.Information($"gProjectilesManagerVA found at VA: {GameProjectilesManagerVA.ToString("X16")}");

        //
        // ox-cart values
        //
        // grunt transporter:
        // 43 83 BC 1F ?? ?? ?? ?? ?? 49 8B CB -> cmp     dword ptr [r15+r11+18Ch], 8 <- the amount of stones until the ox gets its wares
        // 66 42 83 BC 03 ?? ?? ?? ?? ?? 66 0F 4D C5 -> cmp     word ptr [rbx+r8+752h], 8 <-- no clue
        // 8D 57 ?? 66 42 89 AC 23 -> lea edx,qword ptr ds:[rdi+8] <- the +8 is the amount of stones the ox gets
        // OX:
        // 66 42 83 BC 33 ?? ?? ?? ?? ?? 0F 8C ?? ?? ?? ?? 45 0F BF 89 -> cmp     word ptr [rbx+r14+9E0h], 8 <-- departs when it has atleast this amount
        if (!scanner.TryGetMultiManagedImmediate(
            patterns: [
                "43 83 BC 1F ?? ?? ?? ?? ?? 49 8B CB",                          // cmp dword ptr [r15+r11+18Ch], 8
                "66 42 83 BC 03 ?? ?? ?? ?? ?? 66 0F 4D C5",                    // cmp word ptr [rbx+r8+752h], 8
                "66 42 83 BC 33 ?? ?? ?? ?? ?? 0F 8C ?? ?? ?? ?? 45 0F BF 89"   // cmp word ptr [rbx+r14+9E0h], 8
            ],
            operands: [1, 1, 1],
            out OxStoneRequiredAmount))
        {
            LogHelper.Error("Could not find one or more Ox stone requirement instructions.");
        }
        _managedAssemblyStateManager.Register("gOxStoneRequiredAmount", OxStoneRequiredAmount);

        if (!scanner
          .Scan("8D 57 ?? 66 42 89 AC 23")
          .TryGetManagedDisplacement(out OxStoneReceiveAmount, operand: 1))
        {
            LogHelper.Error("Could not find the Ox stone receive amount instruction.");
        }
        _managedAssemblyStateManager.Register("gOxStoneReceiveAmount", OxStoneReceiveAmount);

        //OxStoneReceiveAmount.SetValue(4);
        //OxStoneRequiredAmount.SetValue(4);

        //
        // gUnitEUGoldCostTableRVA: Can be found referenced within c_game_can_afford_european_unit aka gUnitEUGoldCostTableMain
        //
        if (!scanner
          .Scan("41 8B 8C 93")
          .TryReadDisplacement(out UnitEUGoldCostTableRVA))
        {
            LogHelper.Error($"Could not find gUnitEUGoldCostTableRVA");
        }
        LogHelper.Information($"gUnitEUGoldCostTableRVA found at RVA: {UnitEUGoldCostTableRVA.ToString("X16")}");

        //
        // gUnitEUGoodTypeCostsTableRVA: Can be found referenced within c_game_player_buy_eu_mercenary
        //
        if (!scanner
          .Scan("4D 63 AC C3")
          .TryReadDisplacement(out UnitEUGoodTypeCostsTableRVA))
        {
            LogHelper.Error($"Could not find gUnitEUGoodTypeCostsTableRVA");
        }
        LogHelper.Information($"gUnitEUGoodTypeCostsTableRVA found at RVA: {UnitEUGoodTypeCostsTableRVA.ToString("X16")}");

        //
        // gTreeProximityAreaLevelTableRVA: Can be found referenced within c_game_create_tree_proximity_area
        //
        if (!scanner
          .Scan("41 8B B4 91")
          .TryReadDisplacement(out TreeProximityAreaLevelTableRVA))
        {
            LogHelper.Error($"Could not find gTreeProximityAreaLevelTableRVA");
        }
        LogHelper.Information($"gTreeProximityAreaLevelTableRVA found at RVA: {TreeProximityAreaLevelTableRVA.ToString("X16")}");

        //
        // mercenary gold costs
        //
        if (!scanner
          .Scan("83 FA ?? 75 ?? 8B DA")
          .TryGetManagedImmediate(out GoldCostEngineer, operand: 1))
        {
            LogHelper.Error("Could not find gGoldCostEngineer.");
        }
        _managedAssemblyStateManager.Register("gGoldCostEngineer", GoldCostEngineer);

        if (!scanner
          .Scan("8D 5F ?? E9 ?? ?? ?? ?? 83 FF ?? 75 ?? 8D 5F ?? E9 ?? ?? ?? ?? 83 FF ?? 75 ?? 8D 5F ?? E9 ?? ?? ?? ?? 83 FF ?? 75 ?? 8D 5F ?? E9 ?? ?? ?? ?? 83 FF ?? 0F 84")
          .TryGetManagedDisplacement(out GoldCostArabBow, operand: 1, constantOffset: 0x46))
        {
            LogHelper.Error("Could not find gGoldCostArabBow.");
        }
        _managedAssemblyStateManager.Register("gGoldCostArabBow", GoldCostArabBow);

        if (!scanner
          .Scan("8D 5F ? E9 ? ? ? ? 83 FF ? 75 ? 39 05 ? ? ? ? 8D 5F ? 0F 84 ? ? ? ? 83 3D")
          .TryGetManagedDisplacement(out GoldCostTunneler, operand: 1, constantOffset: 0x05))
        {
            LogHelper.Error("Could not find gGoldCostTunneler.");
        }
        _managedAssemblyStateManager.Register("gGoldCostTunneler", GoldCostTunneler);

        if (!scanner
          .Scan("8D 5F ? 0F 84 ? ? ? ? 83 3D")
          .TryGetManagedDisplacement(out GoldCostLadderman, operand: 1, constantOffset: 0x1D))
        {
            LogHelper.Error("Could not find gGoldCostLadderman.");
        }
        _managedAssemblyStateManager.Register("gGoldCostLadderman", GoldCostLadderman);

        if (!scanner
          .Scan("8D 5F ?? E9 ?? ?? ?? ?? 83 FF ?? 75 ?? 8D 5F ?? E9 ?? ?? ?? ?? 83 FF ?? 75 ?? 8D 5F ?? E9 ?? ?? ?? ?? 83 FF ?? 75 ?? 8D 5F ?? E9 ?? ?? ?? ?? 83 FF ?? 75 ?? 8D 5F ?? E9 ?? ?? ?? ?? 83 FF ?? 0F 84")
          .TryGetManagedDisplacement(out GoldCostMonk, operand: 1, constantOffset: 0x25))
        {
            LogHelper.Error("Could not find gGoldCostMonk.");
        }
        _managedAssemblyStateManager.Register("gGoldCostMonk", GoldCostMonk);

        if (!scanner
          .Scan("8D 5F ?? E9 ?? ?? ?? ?? 83 FF ?? 75 ?? 8D 5F ?? E9 ?? ?? ?? ?? 83 FF ?? 75 ?? 8D 5F ?? E9 ?? ?? ?? ?? 83 FF ?? 0F 84")
          .TryGetManagedDisplacement(out GoldCostArabSlave, operand: 1, constantOffset: 0x47))
        {
            LogHelper.Error("Could not find gGoldCostArabSlave.");
        }
        _managedAssemblyStateManager.Register("gGoldCostArabSlave", GoldCostArabSlave);

        if (!scanner
          .Scan("8D 5F ?? E9 ?? ?? ?? ?? 83 FF ?? 75 ?? 8D 5F ?? E9 ?? ?? ?? ?? 83 FF ?? 0F 84")
          .TryGetManagedDisplacement(out GoldCostArabSlinger, operand: 1, constantOffset: 0x48))
        {
            LogHelper.Error("Could not find gGoldCostArabSlinger.");
        }
        _managedAssemblyStateManager.Register("gGoldCostArabSlinger", GoldCostArabSlinger);

        if (!scanner
          .Scan("8D 5F ?? E9 ?? ?? ?? ?? 83 FF ?? 0F 84")
          .TryGetManagedDisplacement(out GoldCostArabAssassin, operand: 1, constantOffset: 0x49))
        {
            LogHelper.Error("Could not find gGoldCostAssassin.");
        }
        _managedAssemblyStateManager.Register("gGoldCostArabAssassin", GoldCostArabAssassin);

        if (!scanner
          .Scan("BB ?? ?? ?? ?? 4D 69 FE")
          .TryGetManagedImmediate(out GoldCostArabhorsemanOrSwordsmanOrDemolisher, operand: 1))
        {
            LogHelper.Error("Could not find gGoldCostArabhorsemanOrSwordsman.");
        }
        _managedAssemblyStateManager.Register("gGoldCostArabhorsemanOrSwordsman", GoldCostArabhorsemanOrSwordsmanOrDemolisher);

        if (!scanner
          .Scan("8D 5F ? E9 ? ? ? ? 83 FF ? 75 ? 8D 5F ? E9 ? ? ? ? 83 FF ? 75 ? 39 05")
          .TryGetManagedDisplacement(out GoldCostArabGrenadier, operand: 1, constantOffset: 0x4C))
        {
            LogHelper.Error("Could not find gGoldCostArabGrenadier.");
        }
        _managedAssemblyStateManager.Register("gGoldCostArabGrenadier", GoldCostArabGrenadier);

        if (!scanner
          .Scan("8D 5F ?? EB ?? 83 FF ?? 75 ?? 39 05")
          .TryGetManagedDisplacement(out GoldCostBedouinCamelLancer, operand: 1, constantOffset: 0x4E))
        {
            LogHelper.Error("Could not find gGoldCostBedouinCamelLancer.");
        }
        _managedAssemblyStateManager.Register("gGoldCostBedouinCamelLancer", GoldCostBedouinCamelLancer);

        if (!scanner
          .Scan("8D 5F ? 74")
          .TryGetManagedDisplacement(out GoldCostBedouinEunuch, operand: 1, constantOffset: 0x50))
        {
            LogHelper.Error("Could not find gGoldCostBedouinEunuch.");
        }
        _managedAssemblyStateManager.Register("gGoldCostBedouinEunuch", GoldCostBedouinEunuch);

        if (!scanner
         .Scan("8D 5F ?? EB ?? 83 FF ?? 75 ?? 8D 5F ?? EB ?? 83 FF ?? 75 ?? 8D 5F ?? 44 8D 57")
         .TryGetManagedDisplacement(out GoldCostBedouinAmbusher, operand: 1, constantOffset: 0x51))
        {
            LogHelper.Error("Could not find gGoldCostBedouinAmbusher.");
        }
        _managedAssemblyStateManager.Register("gGoldCostBedouinAmbusher", GoldCostBedouinAmbusher);

        if (!scanner
         .Scan("8D 5F ?? EB ?? 83 FF ?? 75 ?? 8D 5F ?? 44 8D 57")
         .TryGetManagedDisplacement(out GoldCostBedouinSkirmisher, operand: 1, constantOffset: 0x52))
        {
            LogHelper.Error("Could not find gGoldCostBedouinSkirmisher.");
        }
        _managedAssemblyStateManager.Register("gGoldCostBedouinSkirmisher", GoldCostBedouinSkirmisher);

        if (!scanner
         .Scan("8D 5F ?? 44 8D 57 ?? EB ?? 83 FF")
         .TryGetManagedDisplacement(out GoldCostBedouinHeavyCamel, operand: 1, constantOffset: 0x53))
        {
            LogHelper.Error("Could not find gGoldCostBedouinHeavyCamel.");
        }
        _managedAssemblyStateManager.Register("gGoldCostBedouinHeavyCamel", GoldCostBedouinHeavyCamel);

        if (!scanner
         .Scan("8D 5F ?? EB ?? 83 FF ?? 0F 85")
         .TryGetManagedDisplacement(out GoldCostBedouinSapper, operand: 1, constantOffset: 0x54))
        {
            LogHelper.Error("Could not find gGoldCostBedouinSapper.");
        }
        _managedAssemblyStateManager.Register("gGoldCostBedouinSapper", GoldCostBedouinSapper);

        if (!scanner
         .Scan("8D 5F ?? 74")
         .TryGetManagedDisplacement(out GoldCostBedouinHealer, operand: 1, constantOffset: 0x4F))
        {
            LogHelper.Error("Could not find gGoldCostBedouinHealer.");
        }
        _managedAssemblyStateManager.Register("gGoldCostBedouinHealer", GoldCostBedouinHealer);

        //LogHelper.Information($"Arab Bow Cost: {GoldCostArabBow.GetValue()}");
        //LogHelper.Information($"Arab Slave Cost: {GoldCostArabSlave.GetValue()}");
        //LogHelper.Information($"Arab Slinger Cost: {GoldCostArabSlinger.GetValue()}");
        //LogHelper.Information($"Bedouin Healer Cost: {GoldCostBedouinHealer.GetValue()}");

        // Print all vanilla costs
        //foreach (object? val in Enum.GetValues(typeof(eChimps)))
        //{
        //    ushort s = (ushort)val;
        //    LogHelper.Information($"[{(eChimps)s}]=[{GameData.getChimpGoldCost(s)}]");
        //}

        // DateTime stuff
        if (!scanner
         .Scan("FF 81 ?? ?? ?? ?? 44 89 91")
         .TryGetManagedDisplacement(out DateTimeCurrentYearRVA, operand: 0))
        {
            LogHelper.Error("Could not find gDateTimeCurrentYearRVA.");
        }
        _managedAssemblyStateManager.Register("gDateTimeCurrentYearRVA", DateTimeCurrentYearRVA, exposed: false);
        LogHelper.Information($"gDateTimeCurrentYearRVA: {DateTimeCurrentYearRVA.GetValue().ToString("X16")}");

        if (!scanner
         .Scan("83 B9 ?? ?? ?? ?? ?? 44 89 91")
         .TryGetManagedDisplacement(out DateTimeCurrentMonthRVA, operand: 0))
        {
            LogHelper.Error("Could not find gDateTimeCurrentMonthRVA.");
        }
        _managedAssemblyStateManager.Register("gDateTimeCurrentMonthRVA", DateTimeCurrentMonthRVA, exposed: false);

        if (!scanner
         .Scan("83 B9 ?? ?? ?? ?? ?? C7 81")
         .TryGetManagedDisplacement(out DateTimeCurrentDayRVA, operand: 0))
        {
            LogHelper.Error("Could not find gDateTimeCurrentDayRVA.");
        }
        _managedAssemblyStateManager.Register("gDateTimeCurrentDayRVA", DateTimeCurrentDayRVA, exposed: false);

        if (!scanner
         .Scan("83 B9 ?? ?? ?? ?? ?? C7 81")
         .TryGetManagedImmediate(out DateTimeDaysInMonthRVA, operand: 1))
        {
            LogHelper.Error("Could not find gDateTimeDaysInMonthRVA.");
        }
        _managedAssemblyStateManager.Register("gDateTimeDaysInMonthRVA", DateTimeDaysInMonthRVA, exposed: false);

        if (!scanner
         .Scan("83 B9 ?? ?? ?? ?? ?? 44 89 91")
         .TryGetManagedImmediate(out DateTimeMonthsInYearRVA, operand: 1))
        {
            LogHelper.Error("Could not find gDateTimeMonthsInYearRVA.");
        }
        _managedAssemblyStateManager.Register("gDateTimeMonthsInYearRVA", DateTimeMonthsInYearRVA, exposed: false);

        //
        // gBedouinDemolisherShieldHealth: can be found referenced within c_game_unit_spawn_ex
        //
        if (!scanner
         .Scan("B8 ?? ?? ?? ?? 66 45 89 A4 36")
         .TryGetManagedImmediate(out BedouinDemolisherShieldHealth, operand: 1))
        {
            LogHelper.Error($"Could not find gBedouinDemolisherShieldHealth");
        }
        _managedAssemblyStateManager.Register("gBedouinDemolisherShieldHealth", BedouinDemolisherShieldHealth);
        LogHelper.Information($"gBedouinDemolisherShieldHealth: {BedouinDemolisherShieldHealth.GetValue()}");

        //
        // gCatapultRestockStoneCost: can be found referenced within c_game_player_restock_catapult_ammo
        //
        if (!scanner
         .Scan("41 B9 ? ? ? ? C7 44 24 ? ? ? ? ? 8B D1")
         .TryGetManagedImmediate(out CatapultRestockStoneCost, operand: 1))
        {
            LogHelper.Error($"Could not find gCatapultRestockStoneCost");
        }
        _managedAssemblyStateManager.Register("gCatapultRestockStoneCost", CatapultRestockStoneCost);
        LogHelper.Information($"gCatapultRestockStoneCost: {CatapultRestockStoneCost.GetValue()}");

        //
        // gCatapultRestockStoneAmount: can be found referenced within c_game_player_restock_catapult_ammo
        //
        if (!scanner
         .Scan("66 83 84 39")
         .TryGetManagedImmediate(out CatapultRestockStoneAmount, operand: 1))
        {
            LogHelper.Error($"Could not find gCatapultRestockStoneAmount");
        }
        _managedAssemblyStateManager.Register("gCatapultRestockStoneAmount", CatapultRestockStoneAmount);
        LogHelper.Information($"gCatapultRestockStoneAmount: {CatapultRestockStoneAmount.GetValue()}");

        //
        // gCatapultInitialStoneAmount: can be found referenced within c_game_unit_spawn_ex
        //
        if (!scanner
         .Scan("B8 ? ? ? ? 66 41 89 84 36 ? ? ? ? E9 ? ? ? ? 83 FF ? 75 ? 41 83 BC AF")
         .TryGetManagedImmediate(out CatapultInitialStoneAmount, operand: 1))
        {
            LogHelper.Error($"Could not find gCatapultInitialStoneAmount");
        }
        _managedAssemblyStateManager.Register("gCatapultInitialStoneAmount", CatapultInitialStoneAmount);
        LogHelper.Information($"gCatapultInitialStoneAmount: {CatapultInitialStoneAmount.GetValue()}");

        //
        // AssassinDetectionRange: can be found referenced within c_game_unit_behaviour_handler
        // default: 160
        // Units outside this range are completely invisible (unless attacking)
        //
        if (!scanner
         .Scan("3D ? ? ? ? 7E ? 0F B7 84 2B")
         .TryGetManagedImmediate(out AssassinDetectionRange, operand: 1))
        {
            LogHelper.Error($"Could not find gAssassinDetectionRange");
        }
        _managedAssemblyStateManager.Register("gAssassinDetectionRange", AssassinDetectionRange);
        LogHelper.Information($"gAssassinDetectionRange: {AssassinDetectionRange.GetValue()}");

        //
        // AssassinTransparencyThreshold: can be found referenced within c_game_unit_behaviour_handler
        // default: 120
        // Units between this value and the Max will look ghostly
        //
        if (!scanner
         .Scan("83 F8 ? 0F 8E ? ? ? ? 83 3D")
         .TryGetManagedImmediate(out AssassinTransparencyThreshold, operand: 1))
        {
            LogHelper.Error($"Could not find gAssassinTransparencyThreshold");
        }
        _managedAssemblyStateManager.Register("gAssassinTransparencyThreshold", AssassinTransparencyThreshold);
        LogHelper.Information($"gAssassinTransparencyThreshold: {AssassinTransparencyThreshold.GetValue()}");

        //
        // AIMaxOxTethers: can be found referenced within c_game_ai_need_more_ox_bases
        // default: 10
        //
        if (!scanner
            .Scan("83 F8 ? 0F 8D ? ? ? ? 48 69 F5")
            .TryGetManagedImmediate(out AIMaxOxTethers, operand: 1))
        {
            LogHelper.Error($"Could not find gAIMaxOxTethers");
        }
        _managedAssemblyStateManager.Register("gAIMaxOxTethers", AIMaxOxTethers);
        LogHelper.Information($"gAIMaxOxTethers: {AIMaxOxTethers.GetValue()}");

        //
        // AIStoneToOxenRatio: can be found referenced within c_game_ai_need_more_ox_bases
        // default: 20
        // description: only build a new oxen tether if the best candidate quarry has a stone accumulation > 20
        //
        if (!scanner
            .Scan("83 FF ? 7E ? 44 89 36")
            .TryGetManagedImmediate(out AIStoneToOxenRatio, operand: 1))
        {
            LogHelper.Error($"Could not find gAIStoneToOxenRatio");
        }
        _managedAssemblyStateManager.Register("gAIStoneToOxenRatio", AIStoneToOxenRatio);
        LogHelper.Information($"gAIStoneToOxenRatio: {AIStoneToOxenRatio.GetValue()}");

        //
        // AIHighGoldThresholdForSiege: can be found referenced within c_game_ai_calculate_siege_size
        // default: 10000
        // description: the threshold for ais to consider sending stronger siege waves
        //
        if (!scanner
            .Scan("42 81 BC 37")
            .TryGetManagedImmediate(out AIHighGoldThresholdForSiege, operand: 1))
        {
            LogHelper.Error($"Could not find gAIHighGoldThresholdForSiege");
        }
        _managedAssemblyStateManager.Register("gAIHighGoldThresholdForSiege", AIHighGoldThresholdForSiege);
        LogHelper.Information($"gAIHighGoldThresholdForSiege: {AIHighGoldThresholdForSiege.GetValue()}");

        //
        // AIGoldThresholdForHarassmentSiegeEngines: can be found referenced within c_game_ai_deploy_harassing_siege_engine_tents
        // default: 500
        // description: the threshold for ais to consider sending harassment siege engines
        //
        if (!scanner
            .Scan("42 81 BC 2B")
            .TryGetManagedImmediate(out AIGoldThresholdForHarassmentSiegeEngines, operand: 1))
        {
            LogHelper.Error($"Could not find gAIGoldThresholdForHarassmentSiegeEngines");
        }
        _managedAssemblyStateManager.Register("gAIGoldThresholdForHarassmentSiegeEngines", AIGoldThresholdForHarassmentSiegeEngines);
        LogHelper.Information($"gAIGoldThresholdForHarassmentSiegeEngines: {AIGoldThresholdForHarassmentSiegeEngines.GetValue()}");

        //
        // gKnightRunSpeedBonus: Can be found referenced within c_game_unit_knight_update
        //
        if (!scanner
         .Scan("B8 ? ? ? ? 66 42 89 84 2B ? ? ? ? 42 C7 84 2B")
         .TryGetManagedImmediate(out KnightRunSpeedBonus, operand: 1))
        {
            LogHelper.Error($"Could not find gKnightRunSpeedBonus");
        }
        _managedAssemblyStateManager.Register("gKnightRunSpeedBonus", KnightRunSpeedBonus);
        LogHelper.Information($"gKnightRunSpeedBonus found at VA: {KnightRunSpeedBonus.InstructionAddress.ToString("X16")}");

        //
        // gArabHorsemanRunSpeedBonus: Can be found referenced within c_game_unit_arab_horseman_update
        //
        if (!scanner
         .Scan("BE ? ? ? ? 4C 8D 35 ? ? ? ? 66 42 89 B4 13")
         .TryGetManagedImmediate(out ArabHorsemanRunSpeedBonus, operand: 1))
        {
            LogHelper.Error($"Could not find gArabHorsemanRunSpeedBonus");
        }
        _managedAssemblyStateManager.Register("gArabHorsemanRunSpeedBonus", ArabHorsemanRunSpeedBonus);
        LogHelper.Information($"gArabHorsemanRunSpeedBonus found at VA: {ArabHorsemanRunSpeedBonus.InstructionAddress.ToString("X16")}");

        //
        // gBedouinCamelLancerRunSpeedBonus: Can be found referenced within c_game_unit_bedouin_camel_lancer_update
        //
        if (!scanner
         .Scan("B8 ? ? ? ? 42 C7 84 2B ? ? ? ? ? ? ? ? 66 42 89 84 2B")
         .TryGetManagedImmediate(out BedouinCamelLancerRunSpeedBonus, operand: 1))
        {
            LogHelper.Error($"Could not find gBedouinCamelLancerRunSpeedBonus");
        }
        _managedAssemblyStateManager.Register("gBedouinCamelLancerRunSpeedBonus", BedouinCamelLancerRunSpeedBonus);
        LogHelper.Information($"gBedouinCamelLancerRunSpeedBonus found at VA: {BedouinCamelLancerRunSpeedBonus.InstructionAddress.ToString("X16")}");

        //
        // gBedouinHeavyCamelRunSpeedBonus: Can be found referenced within c_game_unit_bedouin_heavy_camel_update
        //
        if (!scanner
         .Scan("BA ? ? ? ? 4C 8D 35")
         .TryGetManagedImmediate(out BedouinHeavyCamelRunSpeedBonus, operand: 1))
        {
            LogHelper.Error($"Could not find gBedouinHeavyCamelRunSpeedBonus");
        }
        _managedAssemblyStateManager.Register("gBedouinHeavyCamelRunSpeedBonus", BedouinHeavyCamelRunSpeedBonus);
        LogHelper.Information($"gBedouinHeavyCamelRunSpeedBonus found at VA: {BedouinHeavyCamelRunSpeedBonus.InstructionAddress.ToString("X16")}");

        //
        // gCurrentContextMapperValueVA: Can be found referenced within DLL_StartMapAction
        //
        if (!scanner
         .Scan("C7 05 ? ? ? ? ? ? ? ? 48 83 C4 ? C3 E8")
         .TryReadDisplacement(out CurrentContextMapperValueVA, operandIndex: 0))
        {
            LogHelper.Error($"Could not find gCurrentContextMapperValueVA");
        }
        LogHelper.Information($"gCurrentContextMapperValueVA found at VA: {CurrentContextMapperValueVA.ToString("X16")}");

        //
        // gCurrentContextUnitValueVA: Can be found referenced within c_game_unit_bedouin_healer
        //
        if (!scanner
         .Scan("48 63 2D ? ? ? ? 48 69 DD ? ? ? ? 49 69 CA")
         .TryReadDisplacement(out CurrentContextUnitValueVA, operandIndex: 1))
        {
            LogHelper.Error($"Could not find gCurrentContextUnitValueVA");
        }
        LogHelper.Information($"gCurrentContextUnitValueVA found at VA: {CurrentContextUnitValueVA.ToString("X16")}");


        //
        // gCurrentContextBuildingValueVA: Can be found referenced within c_game_building_woodcuttershut_update
        //
        if (!scanner
         .Scan("4C 63 05 ? ? ? ? 48 8D 35")
         .TryReadDisplacement(out CurrentContextBuildingValueVA, operandIndex: 1))
        {
            LogHelper.Error($"Could not find gCurrentContextBuildingValueVA");
        }
        LogHelper.Information($"gCurrentContextBuildingValueVA found at VA: {CurrentContextBuildingValueVA.ToString("X16")}");

        //
        // gBuildingRepairProximityCheckRange: Can be found referenced within c_game_repairs_allowed
        //
        if (!scanner
         .Scan("41 BA ? ? ? ? C6 44 24")
         .TryGetManagedImmediate(out BuildingRepairProximityCheckRange, operand: 1))
        {
            LogHelper.Error($"Could not find gBuildingRepairProximityCheckRange");
        }
        _managedAssemblyStateManager.Register("gBuildingRepairProximityCheckRange", BuildingRepairProximityCheckRange);
        LogHelper.Information($"gBuildingRepairProximityCheckRange found at VA: {BuildingRepairProximityCheckRange.InstructionAddress.ToString("X16")}");

        //
        // gBuildingRepairProximityCheckExRange: Can be found referenced within c_game_repairs_allowed
        //
        if (!scanner
         .Scan("B8 ? ? ? ? 41 BA ? ? ? ? C6 44 24")
         .TryGetManagedImmediate(out BuildingRepairProximityCheckExRange, operand: 1))
        {
            LogHelper.Error($"Could not find gBuildingRepairProximityCheckExRange");
        }
        _managedAssemblyStateManager.Register("gBuildingRepairProximityCheckExRange", BuildingRepairProximityCheckExRange);
        LogHelper.Information($"gBuildingRepairProximityCheckExRange found at VA: {BuildingRepairProximityCheckExRange.InstructionAddress.ToString("X16")}");

        //
        // ChoreManagerVA: Can be found referenced within c_game_repairs_allowed
        //
        if (!scanner
         .Scan("48 8D 1D ? ? ? ? C7 05 ? ? ? ? ? ? ? ? 48 8D 14 80 48 C1 E2 ? C7 84 1A ? ? ? ? ? ? ? ? 8B 05 ? ? ? ? 83 F8 ? 75 ? 45 33 C9 C7 44 24")
         .TryReadDisplacement(out ChoreManagerVA))
        {
            LogHelper.Error($"Could not find ChoreManagerVA");
        }
        LogHelper.Information($"ChoreManager found at VA: {ChoreManagerVA.ToString("X16")}");

        //
        // ChoreSendPhaseVA: Can be found referenced within c_game_reset_player_list
        //
        if (!scanner
         .Scan("8B 05 ? ? ? ? 83 F8 ? 75 ? 45 33 C9 C7 44 24 ? ? ? ? ? 44 8B C0")
         .TryReadDisplacement(out ChoreSendPhaseVA))
        {
            LogHelper.Error($"Could not find ChoreSendPhaseVA");
        }
        LogHelper.Information($"ChoreSendPhase found at VA: {ChoreSendPhaseVA.ToString("X16")}");

        //
        // MessageManagerVA: Can be found referenced within c_game_ai_fortress_damaged
        //
        if (!scanner
         .Scan("48 8D 0D ? ? ? ? 41 FF C8 41 B9 ? ? ? ? 8B D3 E8 ? ? ? ? 48 8B 5C 24")
         .TryReadDisplacement(out MessageManagerVA))
        {
            LogHelper.Error($"Could not find MessageManagerVA");
        }
        LogHelper.Information($"MessageManager found at VA: {MessageManagerVA.ToString("X16")}");

        //
        // EngineMemoryLastCallDescriptorVA: Can be found referenced within c_game_save_game_file
        //
        if (!scanner
         .Scan("48 8D 0D ? ? ? ? 4C 03 4B ? 4C 8B 47 ? 8B 54 83 ? E8 ? ? ? ? 48 63 53")
         .TryReadDisplacement(out EngineMemoryLastCallDescriptorVA))
        {
            LogHelper.Error($"Could not find EngineMemoryLastCallDescriptorVA");
        }
        LogHelper.Information($"EngineMemoryLastCallDescriptorVA found at VA: {EngineMemoryLastCallDescriptorVA.ToString("X16")}");

        //
        // PathfindingProfilesUnitTableRVA: Can be found referenced within c_game_unit_issueorder_movehere
        //
        if (!scanner
         .Scan("8B 84 82 ? ? ? ? 89 84 24")
         .TryReadDisplacement(out PathfindingProfilesUnitTableRVA))
        {
            LogHelper.Error($"Could not find PathfindingProfilesUnitTableRVA");
        }
        LogHelper.Information($"PathfindingProfilesUnitTableRVA found at RVA: {PathfindingProfilesUnitTableRVA.ToString("X16")}");

        //
        // PathfindingConnectionClassesUnitTableVA: Can be found referenced within c_game_pathfinding_unit_can_use_connection_class
        //
        if (!scanner
         .Scan("48 8D 0D ? ? ? ? 48 03 C2")
         .TryReadDisplacement(out PathfindingConnectionClassesUnitTableVA))
        {
            LogHelper.Error($"Could not find PathfindingConnectionClassesUnitTableVA");
        }
        LogHelper.Information($"PathfindingConnectionClassesUnitTableVA found at VA: {PathfindingConnectionClassesUnitTableVA.ToString("X16")}");

        //
        // TilePCLTableRVA: Can be found referenced within c_game_ai_find_harassment_tent_spot_internal
        //
        if (!scanner
         .Scan("41 0F BF 84 41 ? ? ? ? FF 83")
         .TryReadDisplacement(out TilePCLTableRVA))
        {
            LogHelper.Error($"Could not find TilePCLTableRVA");
        }
        LogHelper.Information($"TilePCLTableRVA found at RVA: {TilePCLTableRVA.ToString("X16")}");

        //
        // gNoKnockdownWalls
        //
        if (!scanner
          .Scan("4C 89 25 ? ? ? ? EB")
          .TryReadDisplacement(out NoKnockdownWallsVA, operandIndex: 0))
        {
            LogHelper.Error("Could not find gNoKnockdownWalls.");
        }
        LogHelper.Information($"gNoKnockdownWalls found at VA: {NoKnockdownWallsVA.ToString("X16")}");

        //
        // gGlobalImprovedSiegingBehaviourVA
        //
        if (!scanner
          .Scan("89 05 ? ? ? ? 8B 82")
          .TryReadDisplacement(out GlobalImprovedSiegingBehaviourVA, operandIndex: 0))
        {
            LogHelper.Error("Could not find gGlobalImprovedSiegingBehaviourVA");
        }
        LogHelper.Information($"gGlobalImprovedSiegingBehaviourVA found at VA: {GlobalImprovedSiegingBehaviourVA.ToString("X16")}");

        //
        // gGlobalMoreAggressiveAISiegingBehaviourVA
        //
        if (!scanner
          .Scan("89 05 ? ? ? ? 8B 82 ? ? ? ? 89 81")
          .TryReadDisplacement(out GlobalMoreAggressiveAISiegingBehaviourVA, operandIndex: 0))
        {
            LogHelper.Error("Could not find gGlobalMoreAggressiveAISiegingBehaviourVA");
        }
        LogHelper.Information($"gGlobalMoreAggressiveAISiegingBehaviourVA found at VA: {GlobalMoreAggressiveAISiegingBehaviourVA.ToString("X16")}");

        //
        // gGlobalAdvancedOptions
        //
        if (!scanner
          .Scan("83 3D ? ? ? ? ? 74 ? BF")
          .TryReadDisplacement(out GlobalAdvancedOptionsVA, operandIndex: 0))
        {
            LogHelper.Error("Could not find gGlobalAdvancedOptions");
        }
        LogHelper.Information($"gGlobalAdvancedOptions found at VA: {GlobalAdvancedOptionsVA.ToString("X16")}");

        //
        // StablesHorseRegenTickTarget: can be found referenced within c_game_horse_stable_update
        // default: 550
        //
        if (!scanner
         .Scan("B8 ? ? ? ? 66 39 84 37")
         .TryGetManagedImmediate(out StablesHorseRegenTickTarget, operand: 1))
        {
            LogHelper.Error($"Could not find gStablesHorseRegenTickTarget");
        }
        _managedAssemblyStateManager.Register("gStablesHorseRegenTickTarget", StablesHorseRegenTickTarget);
        LogHelper.Information($"gStablesHorseRegenTickTarget: {StablesHorseRegenTickTarget.GetValue()}");

        //
        // StablesHorsesCap: can be found referenced within c_game_horse_stable_update
        // default: 4
        // WARNING: Any cap above 4 will cause the game to not be able to link used horses anymore
        // meaning they will simply re-charge when used.
        //
        if (!scanner
         .Scan("80 BC 37 ? ? ? ? ? 7D")
         .TryGetManagedImmediate(out StablesHorsesCap, operand: 1))
        {
            LogHelper.Error($"Could not find gStablesHorsesCap");
        }
        _managedAssemblyStateManager.Register("gStablesHorsesCap", StablesHorsesCap);
        LogHelper.Information($"gStablesHorsesCap: {StablesHorsesCap.GetValue()}");

        //
        // DiseaseDamage1: can be found referenced within c_game_unit_takedamage_projectile
        // default: 150
        //
        if (!scanner
         .Scan("BA ? ? ? ? B8 ? ? ? ? 0F 44 D0 4A 0F BF 84 3E")
         .TryGetManagedImmediate(out DiseaseDamage1, operand: 1))
        {
            LogHelper.Error($"Could not find gDiseaseDamage1");
        }
        _managedAssemblyStateManager.Register("gDiseaseDamage1", DiseaseDamage1);
        LogHelper.Information($"gDiseaseDamage1: {DiseaseDamage1.GetValue()}");

        //
        // DiseaseDamage2: can be found referenced within c_game_unit_takedamage_projectile
        // default: 200
        //
        if (!scanner
         .Scan("B8 ? ? ? ? 0F 44 D0 4A 0F BF 84 3E")
         .TryGetManagedImmediate(out DiseaseDamage2, operand: 1))
        {
            LogHelper.Error($"Could not find gDiseaseDamage2");
        }
        _managedAssemblyStateManager.Register("gDiseaseDamage2", DiseaseDamage2);
        LogHelper.Information($"gDiseaseDamage2: {DiseaseDamage2.GetValue()}");

        //
        // DiseaseDamage3: can be found referenced within c_game_unit_takedamage_projectile
        // default: 400
        //
        if (!scanner
         .Scan("BA ? ? ? ? EB ? 41 83 F9 ? BA")
         .TryGetManagedImmediate(out DiseaseDamage3, operand: 1))
        {
            LogHelper.Error($"Could not find gDiseaseDamage3");
        }
        _managedAssemblyStateManager.Register("gDiseaseDamage3", DiseaseDamage3);
        LogHelper.Information($"gDiseaseDamage3: {DiseaseDamage3.GetValue()}");

        //
        // gGlobalIdsUsedVA
        //
        if (!scanner
          .Scan("8B 05 ? ? ? ? 89 46 ? FF 05 ? ? ? ? 33 D2")
          .TryReadDisplacement(out GlobalIdsUsedVA, operandIndex: 1))
        {
            LogHelper.Error($"Could not find gGlobalIdsUsedVA");
        }
        LogHelper.Information($"gGlobalIdsUsedVA found at VA: {GlobalIdsUsedVA.ToString("X16")}");

        //
        // AIResourceSellCategoryTableVA (found within c_game_ai_sell_item_handler)
        //
        if (!scanner
          .Scan("48 8D 35 ? ? ? ? 48 63 7E")
          .TryReadDisplacement(out AIResourceSellCategoryTableVA, operandIndex: 1))
        {
            LogHelper.Error($"Could not find AIResourceSellCategoryTableVA");
        }
        LogHelper.Information($"AIResourceSellCategoryTableVA found at VA: {AIResourceSellCategoryTableVA.ToString("X16")}");

        //
        // AIResourceSellCategoryTableEndVA (found within c_game_ai_sell_item_handler)
        //
        if (!scanner
          .Scan("48 8D 05 ? ? ? ? 48 3B F0")
          .TryReadDisplacement(out AIResourceSellCategoryTableEndVA, operandIndex: 1))
        {
            LogHelper.Error($"Could not find AIResourceSellCategoryTableEndVA");
        }
        LogHelper.Information($"AIResourceSellCategoryTableEndVA found at VA: {AIResourceSellCategoryTableEndVA.ToString("X16")}");

        //
        // DefaultTradePriceTable: can be found within c_game_setup_tradegood_availability
        //
        if (!scanner
          .Scan("42 8B 8C 30 ? ? ? ? 49 8D 96")
          .TryReadDisplacement(out DefaultTradeBuyPriceTableRVA, operandIndex: 1))
        {
            LogHelper.Error($"Could not find gDefaultTradePriceTable");
        }
        LogHelper.Information($"gDefaultTradePriceTable found at VA: {DefaultTradeBuyPriceTableRVA.ToString("X16")}");

        //
        // RabbitDespawnTickTime: can be found referenced within c_game_unit_rabbit_update, AIState 0x6E
        // default: 32
        //
        if (!scanner
         .Scan("B9 ? ? ? ? 3A D1")
         .TryGetManagedImmediate(out RabbitDespawnTickTime, operand: 1))
        {
            LogHelper.Error($"Could not find gRabbitDespawnTickTime");
        }
        _managedAssemblyStateManager.Register("gRabbitDespawnTickTime", RabbitDespawnTickTime);
        LogHelper.Information($"gRabbitDespawnTickTime: {RabbitDespawnTickTime.GetValue()}");

        //
        // CurrentContextUnitIdVA: can be found referenced within c_game_DLL_TroopSelectionChangedInternal and c_game_unit_tick_handler
        // -Usually- points to the current unit id through context or an attacking unit id depending on context.
        //
        if (!scanner
          .Scan("89 15 ? ? ? ? 4C 8B C2")
          .TryReadDisplacement(out CurrentContextUnitIdVA, operandIndex: 0))
        {
            LogHelper.Error($"Could not find gCurrentContextUnitIdVA");
        }
        LogHelper.Information($"gCurrentContextUnitIdVA found at VA: {CurrentContextUnitIdVA.ToString("X16")}");

        //
        // FoodConsumptionTickThreshold: can be found referenced within c_game_player_food_consumption_update
        // default: 15000
        //
        if (!scanner.TryGetMultiManagedImmediate(
            patterns: [
                "B8 ? ? ? ? 99 F7 F9",                                          //  mov     eax, 3A98h
                "81 BB ? ? ? ? ? ? ? ? 0F 8E ? ? ? ? 8B 83"                     //  cmp     dword ptr [rbx+1EE0h], 3A98h
            ],
            operands: [1, 1],
            out FoodConsumptionTickThreshold))
        {
            LogHelper.Error("Could not find one or more Ox stone requirement instructions.");
        }
        _managedAssemblyStateManager.Register("gFoodConsumptionTickThreshold", FoodConsumptionTickThreshold);

        //
        // CampPeasantsCap: can be found referenced within c_game_player_update_peasant_respawn_tick
        // default: 24
        //
        if (!scanner
         .Scan("83 7B ? ? 7C ? 44 39 35")
         .TryGetManagedImmediate(out CampPeasantsCap, operand: 1))
        {
            LogHelper.Error($"Could not find gCampPeasantsCap");
        }
        _managedAssemblyStateManager.Register("gCampPeasantsCap", CampPeasantsCap);
        LogHelper.Information($"gCampPeasantsCap: {CampPeasantsCap.GetValue()}");

        //
        // PathfindingMaxTilesConstraint: can be found referenced within c_game_unit_get_path_plan_internal
        // default: 2000 / 0x7D0
        //
        if (!scanner
         .Scan("81 BB ? ? ? ? ? ? ? ? 7D ? 8B 8C 24")
         .TryGetManagedImmediate(out PathfindingMaxTilesConstraint, operand: 1))
        {
            LogHelper.Error($"Could not find gPathfindingMaxTilesConstraint");
        }
        _managedAssemblyStateManager.Register("gPathfindingMaxTilesConstraint", PathfindingMaxTilesConstraint);
        LogHelper.Information($"gPathfindingMaxTilesConstraint: {PathfindingMaxTilesConstraint.GetValue()}");

        /*Log.Information($"EU Archer Good Costs: {GameUnitManagerAPI.Instance.GetUnitGoodCosts(eChimps.CHIMP_TYPE_ARCHER)}");
        Log.Information($"EU Archer Gold Costs: {GameUnitManagerAPI.Instance.GetUnitGoldCost(eChimps.CHIMP_TYPE_ARCHER)}");

        GameUnitManagerAPI.Instance.SetUnitGoldCost(eChimps.CHIMP_TYPE_ARCHER, 1);
        GameUnitManagerAPI.Instance.SetUnitGoodCosts(eChimps.CHIMP_TYPE_ARCHER, new UnitGoodCosts() { cost1 = eGoods.STORED_PIKES });

        Log.Information($"EU Archer Good Costs 2: {GameUnitManagerAPI.Instance.GetUnitGoodCosts(eChimps.CHIMP_TYPE_ARCHER)}");
        Log.Information($"EU Archer Gold Costs 2: {GameUnitManagerAPI.Instance.GetUnitGoldCost(eChimps.CHIMP_TYPE_ARCHER)}");
        Log.Information($"EU Spearman Good Costs: {GameUnitManagerAPI.Instance.GetUnitGoodCosts(eChimps.CHIMP_TYPE_SPEARMAN)}");
        Log.Information($"EU Spearman Gold Costs: {GameUnitManagerAPI.Instance.GetUnitGoldCost(eChimps.CHIMP_TYPE_SPEARMAN)}");
        Log.Information($"EU Maceman Good Costs: {GameUnitManagerAPI.Instance.GetUnitGoodCosts(eChimps.CHIMP_TYPE_MACEMAN)}");
        Log.Information($"EU Maceman Gold Costs: {GameUnitManagerAPI.Instance.GetUnitGoldCost(eChimps.CHIMP_TYPE_MACEMAN)}");*/
        //foreach (object x in Enum.GetValues(typeof(eChimps)))
        //{
        //    eChimps value = (eChimps)x;
        //    Log.Information($"Unit [{value} good-cost = [{GameUnitManagerAPI.Instance.GetUnitGoodCosts(value)}]");
        //}

        LogHelper.Information($"Tree Growth of Oak Stage0: {GameVegetationManagerAPI.Instance.GetTreeGrowthStageDuration(VegetationType.OakTree, TreeGrowthStage.Stage0)}");
        LogHelper.Information($"Default Wood for Skirmish: {GamePlayerManagerAPI.Instance.GetPlayerSkirmishDefaultResources(eGoods.STORED_WOOD_PLANKS)}");

        //GameUnitManager.Instance.DamageLookupTable.SetDamageFromTo(attacker: Enums.eChimps.CHIMP_TYPE_SWORDSMAN, Enums.eChimps.CHIMP_TYPE_KNIGHT, 99999);
        LogHelper.Information($"Knight Damage: {GameUnitManagerAPI.Instance.GetMeleeDamageFromTo(eChimps.CHIMP_TYPE_SWORDSMAN, eChimps.CHIMP_TYPE_KNIGHT)}");
        LogHelper.Information($"Knight Health: {GameUnitManagerAPI.Instance.GetDefaultHealth(eChimps.CHIMP_TYPE_KNIGHT)}");
        LogHelper.Information($"Knight Speed: {GameUnitManagerAPI.Instance.GetDefaultSpeed(eChimps.CHIMP_TYPE_KNIGHT)}");
        //gUnitDamageTable[(int)Enums.eChimps.CHIMP_TYPE_KNIGHT] = 9999;

        LogHelper.Information($"NULL DEFAULT Wood Cost: {GameBuildingManagerAPI.Instance.GetDefaultCost(building: eStructs.STRUCT_NULL)}");
        LogHelper.Information($"HOVEL DEFAULT Wood Cost: {GameBuildingManagerAPI.Instance.GetDefaultCost(building: eStructs.STRUCT_HOVEL)}");
        LogHelper.Information($"Woodcutters Hut DEFAULT Wood Cost: {GameBuildingManagerAPI.Instance.GetDefaultCost(building: eStructs.STRUCT_WOODCUTTERS_HUT)}");
        //GameBuildingManagerAPI.Instance.SetBuildingDefaultCost(building: eStructs.STRUCT_WOODCUTTERS_HUT, new BuildingCost() { Wood = 500 });

        //Log.Information($"Woodcutters Hut Wood Cost: {GameBuildingManagerAPI.Instance.GetBuildingWoodCost(building: eStructs.STRUCT_WOODCUTTERS_HUT)}");
        //Log.Information($"Woodcutters Hut RawPitch Cost: {GameBuildingManagerAPI.Instance.GetBuildingRawPitchCost(building: eStructs.STRUCT_WOODCUTTERS_HUT)}");
        //Log.Information($"Woodcutters Hut Gold Cost: {GameBuildingManagerAPI.Instance.GetBuildingGoldCost(building: eStructs.STRUCT_WOODCUTTERS_HUT)}");
        //Log.Information($"Woodcutters Hut IronIngot Cost: {GameBuildingManagerAPI.Instance.GetBuildingIronIngotCost(building: eStructs.STRUCT_WOODCUTTERS_HUT)}");
        //Log.Information($"Woodcutters Hut Stone Cost: {GameBuildingManagerAPI.Instance.GetBuildingStoneCost(building: eStructs.STRUCT_WOODCUTTERS_HUT)}");
        //GameBuildingManagerAPI.Instance.SetBuildingWoodCost(building: eStructs.STRUCT_WOODCUTTERS_HUT, 500);

        LogHelper.Information($"Lion->Spearman Damage: {GameUnitManagerAPI.Instance.GetMeleeDamageFromTo(eChimps.CHIMP_TYPE_LION, eChimps.CHIMP_TYPE_SPEARMAN)}");
        //LogHelper.Information($"Lion->Spearman Damage Addr: {new IntPtr(GameUnitManagerAPI.Instance.MeleeDamageLookupTable.GetDamageFromToEx(attacker: eChimps.CHIMP_TYPE_LION, eChimps.CHIMP_TYPE_SPEARMAN)).ToString("X16")}");

    }

}
