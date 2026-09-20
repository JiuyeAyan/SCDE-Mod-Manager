using System;

namespace SHCDESE.Interop.Enums;

/// <summary>
/// The game uses a "Chore" system to is responsible for game state changes, specifically to keep multiplayer games in sync with the tick system.
/// There is a individual enum value for specific tasks, where each enum has a custom "packet" that contains metadata that the game will interpret alongside other clients if in a active
/// multiplayer session.
/// 
/// Many of these also get used by gGamestateHandlers in connection with c_game_queue_chore
/// There are many more of these chore types, but currently almost none of them are mapped.
/// 
/// 28 might be castle placement
/// </summary>
public enum ChoreType : byte
{
    None = 0,
    Unused1 = 1,
    InitialAnnounceToHost = 2,
    InitialAnnounceReply = 3,
    /// <summary>
    /// Used in c_game_init_trail, c_game_start_non_skirmish_game_handler
    /// </summary>
    AskForPlayerSlotAssignment = 4,
    /// <summary>
    /// which does a bit more in HD than in DE, in HD it checks AIV availability and communicates it to the host
    /// </summary>
    AssignPlayerIdToPlayerSlot = 5,
    HostShareLobbyState = 6,
    /// <summary>
    /// or Chore Protocol Version.
    /// </summary>
    AnnounceGameVersion = 7,
    SetChorePacketLengthToZero = 8,
    /// <summary>
    /// also triggers AnnounceGameVersion, AnnouncePlayerInformationSuchAsNameLordTypeAndAvailableAIVS, and ShareAIVHash
    /// </summary>
    TriggerLobbyPlayerInformationRefresh = 9,
    AnnouncePlayerInformationSuchAsNameLordTypeAndAvailableAIVS = 10,
    /// <summary>
    /// doesn't really exist in DE
    /// </summary>
    ShareGameSeedAndMultiplayerSettingsAndStartGame = 11,
    /// <summary>
    /// checks game state identical state situation
    /// </summary>
    CommandCheckSync = 12,
    AnnounceTeamsAndPositions = 13,
    /// <summary>
    /// removed in DE
    /// </summary>
    ClickTauntOrChat = 14,
    /// <summary>
    /// c_game_handle_player_unselect_units, UnselectUnit(s) = 15
    /// c_game_player_action_click_navigate_menu_or_escape1
    /// c_game_player_action_deselect_type:         INT32 = -1 (?), INT32 = chimpType
    /// c_game_player_action_deselect_except_type:  INT32 = -2 (?), INT32 = chimpType
    /// c_game_player_action_deselect_all
    /// 
    /// ucp:ClickNavigateMenuOrEscapeOr: possibly repurposed for DE.
    /// </summary>
    ClickNavigateMenuOrEscapeOr = 15,
    /// <summary>
    /// create a Tribe
    /// c_game_handle_player_select_unit, SelectUnit(s) = 16
    /// </summary>
    MakeUnitSelection = 16,
    /// <summary>
    /// INT32: TribeId
    /// INT32: TileX
    /// INT32: TileY
    /// INT32: Unknown
    /// INT32: UnknownBitflags
    /// </summary>
    GiveTribeInstruction = 17,
    ClickErase = 18,
    ClickSetLand = 19,
    /// <summary>
    /// RaiseOrLowerTile
    /// </summary>
    ClickRaiseLand = 20,
    SetTileProperty = 21,
    SetEvenTileHeight = 22,
    SetMinTileHeight = 23,
    SetTileHeight = 24,
    /// <summary>
    /// used in c_game_player_build_placement_handler2 for placing walls, associated with c_game_player_queue_build_wall
    /// INT32: BeginPositionX
    /// INT32: BeginPositionY
    /// INT32: EndPositionX
    /// INT32: EndPositionY
    /// INT32: SourcePlayerId
    /// INT32: Unknown
    /// </summary>
    PlaceWall = 25,
    PlaceVegetation = 26,
   
    /// <summary>
    /// used in c_game_player_build_placement_handler2, there are multiple categories not yet mapped.
    /// TODO
    /// </summary>
    PlaceBuildingCategory2 = 28,
    BuiltFirstStoneBarracks = 30,
    /// <summary>
    /// MakeTroop refers to the game buildings such as the arab outpost, barracks, etc. Its called once the
    /// player hires units. Its chore temp packet looks like the following:
    /// INT32: UnitType
    /// INT32: Amount
    /// </summary>
    MakeTroop = 31,
    /// <summary>
    /// INT32: BuildingId
    /// INT32: UNKNOWN (likely eGoods)
    /// </summary>
    SetNextWeaponMade = 33,
    /// <summary>
    /// Used when a player changes his tax rate.
    /// INT32: TaxesMode
    /// </summary>
    TaxChange = 34,
    /// <summary>
    /// Used when a player changes his rations rate (c_game_handle_player_set_rationing_for_chore or c_game_handle_player_food_restriction_for_chore)
    /// FoodRestriction:
    /// INT32: 100 (?)
    /// INT32: GranaryFoodType
    /// 
    /// RationsChange:
    /// INT32: RationsMode
    /// </summary>
    RationsChangeOrFoodRestriction = 35,
    /// <summary>
    /// aka c_game_player_action_launch_cow and by association Enums.GameActionCommand.Troops_Cow -> private void ButtonUnitLaunchCow(object parameter)
    /// INT32: TribeId
    /// INT32: Unknown
    /// INT32: BuildingIdOrUnitId (optional)
    /// INT32: BuildingGlobalIdOrUnitGlobalId (optional)
    /// INT32: Unknown (optional, unit related? occasionaly very high values like 1000)
    /// </summary>
    RelatedToLaunchingCows = 36,
    /// <summary>
    /// Used in c_game_handle_player_drawbridgestate_for_chore
    /// INT32: BuildingId
    /// INT32: UNKNOWN
    /// INT32: UNKNOWN
    /// </summary>
    DrawBridgeState = 37,
    /// <summary>
    /// Used when the player buys a good through the market (c_game_action_marketplace_interact)
    /// INT32: bSelling
    /// INT32: Unused?
    /// INT32: eGoods
    /// </summary>
    BuyGood = 38,
    /// <summary>
    /// Used in DLL_TriggerMPSave
    /// INT32: gCurrentGameTick
    /// INT32: rotate-and-add checksum of chore out delta data
    /// INT32: Unknown
    /// </summary>
    SaveGame = 39,
    /// <summary>
    /// Used in DLL_TriggerMPLoad
    /// </summary>
    LoadGame = 40,
    WallBulldoze = 41,
    /// <summary>
    /// Used when a player toggle sleep on a building (c_game_handle_player_set_sleep_for_chore)
    /// INT32: BuildingId
    /// </summary>
    SetToggleSleep = 43,
    /// <summary>
    /// Used whenever a gatehouse changes its open/closed state. Used in c_game_handle_player_gatehousestate_for_chore
    /// INT32: BuildingId
    /// INT32: GateHouseState, 0x0A = Close, 0x0B = Open
    /// INT32: BuildingGlobalId
    /// </summary>
    GatehouseState = 45,
    /// <summary>
    /// Used when a player repairs a building (c_game_player_action_repair_building)
    /// INT32: BuildingId
    /// INT32: iRepairWoodNeeded
    /// INT32: iRepairStoneNeeded
    /// INT32: BuildingGlobalId
    /// </summary>
    RepairBuilding = 68,
    /// <summary>
    /// INT32: TribeId
    /// INT32: TribeStance
    /// </summary>
    SetTribeStanceForSelected = 70,
    /// <summary>
    /// INT32: TribeId
    /// INT32: TileX
    /// INT32: TileY
    /// INT32: Unknown
    /// </summary>
    Unknown71 = 71,
    /// <summary>
    /// possibly related to merging of tribes via shift modifier?
    /// INT32: Unknown
    /// INT32: TribeId
    /// </summary>
    SelectionSomethingUnknown = 72,
    /// <summary>
    /// INT32: TribeId
    /// </summary>
    UnknownTribeRelated = 73,
    /// <summary>
    /// No clue, seems vaguely related to wildlife or otherwise farm animals.
    /// </summary>
    Unknown79 = 79,
    /// <summary>
    /// INT32: UnitId (or TribeId if 2nd param is 1)
    /// INT32: Mode (0/unused: Unit aka single, 1: Tribe-level recharge)
    /// </summary>
    AmmoRecharge = 86,
    /// <summary>
    /// Reserved for SHCDE:SE's own script-extender packets. 
    /// Carries an arbitrary length-prefixed blob: 
    /// [2 bytes: CustomNetworkPacketType/dynamic packet ID][N bytes: MessagePack body].
    /// </summary>
    ScriptExtenderChore = 106,

    /// <summary>
    /// Used in c_game_player_action_troop_selection_changed
    /// </summary>
    TroopSelectionChanged = 108,
    /// <summary>
    /// INT32: bPause
    /// </summary>
    AutoTradePause = 109,
    /// <summary>
    /// INCOMPLETE, all GameActions that involve Ally AIs route through this chore.
    /// INT32: UNKNOWN
    /// INT32: targetPlayerId
    /// INT32: allyPlayerId
    /// INT32: sourcePlayerId
    /// </summary>
    AllyOrdersOrAnythingSimiliar = 113,

    /// <summary>
    /// INT32: QWORD ExtremePower
    /// INT32: ...
    /// INT32: sourcePlayerId
    /// </summary>
    ExtremePower = 119,
    /// <summary>
    /// Periodically broadcast by the scheduling/authoritative side every
    /// gSyncIntervalTicks ticks. 
    /// Contains a target-tick checkpoint, a running sequence number, and a list of up to 25 unique chore IDs that this client has queued and expects every
    /// other client to have received by that tick. 
    /// 
    /// Used purely for desync detection/enforcement, receiving clients check their local chore ring buffer against this manifest and will stall
    /// (up to a 3-second timeout, after which they force-proceed and log "SyncEvent - Forced run") if any listed chore hasn't arrived yet. Does not carry gameplay data itself.
    /// INT32: TargetTick
    /// INT32: ChoreIdCount (capped at 25)
    /// INT32: SequenceNumber
    /// INT32[ChoreIdCount]: UniqueChoreIds [UNCONFIRMED]
    /// </summary>
    SyncManifest = 120
}

/*
 * UNIFY gSourcePlayerId, g_choreTempPacket_pot, g_choreTempPacket2_pot and more into a coherent struct!
 * 
 * */