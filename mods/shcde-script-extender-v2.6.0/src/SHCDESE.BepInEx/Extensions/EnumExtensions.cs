using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;

namespace SHCDESE.Extensions;

public static class EnumExtensions
{
    /// <summary>
    /// Returns a short version of the full enum.
    /// Example: CHIMP_TYPE_NULL returns NULL
    /// </summary>
    /// <param name="self">Enum</param>
    /// <returns>Short version string</returns>
    public static string ToShortString(this Interop.eChimps self)
    {
        return self.ToString().TrimStart("CHIMP_TYPE_").ToString();
    }

    /// <summary>
    /// Returns a short version of the full enum.
    /// Example: CHIMP_TYPE_NULL returns NULL
    /// </summary>
    /// <param name="self">Enum</param>
    /// <returns>Short version string</returns>
    public static string ToShortString(this Interop.eStructs self)
    {
        return self.ToString().TrimStart("STRUCT_").ToString();
    }

    /// <summary>
    /// Returns a short version of the full enum.
    /// Example: CHIMP_TYPE_NULL returns NULL
    /// </summary>
    /// <param name="self">Enum</param>
    /// <returns>Short version string</returns>
    public static string ToShortString(this Interop.eGoods self)
    {
        return self.ToString().TrimStart("STORED_").ToString();
    }

    /// <summary>
    /// Convert a eMappers enum to a eStructs enum.
    /// 1:1 mirror from c_game_convert_emappers_to_estructs
    /// </summary>
    /// <param name="mv">The eMappers enum</param>
    /// <returns>Converted eStructs enum</returns>
    public static eStructs ConvertToEStructs(this eMappers mv) => mv switch
    {
        eMappers.MAPPER_TOWER => eStructs.STRUCT_TOWER,
        eMappers.MAPPER_FLETCHER => eStructs.STRUCT_FLETCHERS_WORKSHOP,
        eMappers.MAPPER_WOODSMAN => eStructs.STRUCT_WOODCUTTERS_HUT,
        eMappers.MAPPER_STORES => eStructs.STRUCT_GOODS_YARD,
        eMappers.MAPPER_OUTPOST_BEDOUIN => eStructs.STRUCT_OUTPOST_BEDOUIN,
        eMappers.MAPPER_HOVEL => eStructs.STRUCT_HOVEL,
        eMappers.MAPPER_OXENBASE => eStructs.STRUCT_OXEN_BASE,
        eMappers.MAPPER_QUARRY => eStructs.STRUCT_QUARRY,
        eMappers.MAPPER_TUNNEL => eStructs.STRUCT_TUNNEL_ENTERANCE,
        eMappers.MAPPER_SIGNPOST => eStructs.STRUCT_SIGNPOST,
        eMappers.MAPPER_KEEP1 => eStructs.STRUCT_KEEP_ONE,
        eMappers.MAPPER_KEEP2 => eStructs.STRUCT_KEEP_TWO,
        eMappers.MAPPER_KEEP3 => eStructs.STRUCT_KEEP_THREE,
        eMappers.MAPPER_KEEP4 => eStructs.STRUCT_KEEP_FOUR,
        eMappers.MAPPER_KEEP5 => eStructs.STRUCT_KEEP_FIVE,
        eMappers.MAPPER_STABLES => eStructs.STRUCT_STABLES,
        eMappers.MAPPER_TUNNEL_CONSTRUCTION => eStructs.STRUCT_TUNNEL_CONSTRUCTION,
        eMappers.MAPPER_WHEATFARM => eStructs.STRUCT_WHEATFARM,
        eMappers.MAPPER_HOPSFARM => eStructs.STRUCT_HOPSFARM,
        eMappers.MAPPER_APPLEFARM => eStructs.STRUCT_APPLEFARM,
        eMappers.MAPPER_CATTLEFARM => eStructs.STRUCT_CATTLEFARM,
        eMappers.MAPPER_MILL => eStructs.STRUCT_MILL,
        eMappers.MAPPER_BAKER => eStructs.STRUCT_BAKERS_WORKSHOP,
        eMappers.MAPPER_BREWER => eStructs.STRUCT_BREWERS_WORKSHOP,
        eMappers.MAPPER_TRADEPOST => eStructs.STRUCT_TRADEPOST,
        eMappers.MAPPER_HUNTER => eStructs.STRUCT_HUNTERS_HUT,
        eMappers.MAPPER_BEDOUIN_STOCKADE => eStructs.STRUCT_BEDOUIN_STOCKADE,
        eMappers.MAPPER_GRANARY => eStructs.STRUCT_GRANARY,
        eMappers.MAPPER_ARMOURY => eStructs.STRUCT_ARMOURY,
        eMappers.MAPPER_POLETURNER => eStructs.STRUCT_POLETURNERS_WORKSHOP,
        eMappers.MAPPER_BLACKSMITH => eStructs.STRUCT_BLACKSMITHS_WORKSHOP,
        eMappers.MAPPER_ARMOURER => eStructs.STRUCT_ARMOURERS_WORKSHOP,
        eMappers.MAPPER_TANNER => eStructs.STRUCT_TANNERS_WORKSHOP,
        eMappers.MAPPER_BARRACKS_WOOD => eStructs.STRUCT_BARRACKS_WOOD,
        eMappers.MAPPER_BARRACKS_STONE => eStructs.STRUCT_BARRACKS_STONE,
        eMappers.MAPPER_ENGINEERS_GUILD => eStructs.STRUCT_ENGINEERS_GUILD,
        eMappers.MAPPER_TUNNELERS_GUILD => eStructs.STRUCT_TUNNELLERS_GUILD,
        eMappers.MAPPER_IRON_MINE => eStructs.STRUCT_IRON_MINE,
        eMappers.MAPPER_PITCH_WORKINGS => eStructs.STRUCT_PITCH_DIGGER,
        eMappers.MAPPER_INN => eStructs.STRUCT_INN,
        eMappers.MAPPER_HEALER => eStructs.STRUCT_HEALER,
        eMappers.MAPPER_SIEGE_TOWER_BASE => eStructs.STRUCT_SIEGE_TOWER,
        eMappers.MAPPER_CHURCH1 => eStructs.STRUCT_CHURCH1,
        eMappers.MAPPER_CHURCH2 => eStructs.STRUCT_CHURCH2,
        eMappers.MAPPER_CHURCH3 => eStructs.STRUCT_CHURCH3,
        eMappers.MAPPER_KILLING_PIT => eStructs.STRUCT_KILLING_PIT,
        eMappers.MAPPER_PITCH_DITCH => eStructs.STRUCT_PITCH_DITCH,

        eMappers.MAPPER_GATE_MAIN or
        eMappers.MAPPER_GATE_STONE2A or
        eMappers.MAPPER_GATE_STONE2B => eStructs.STRUCT_GATE_MAIN,

        eMappers.MAPPER_GATE_INNER or
        eMappers.MAPPER_GATE_STONE1A or
        eMappers.MAPPER_GATE_STONE1B => eStructs.STRUCT_GATE_INNER,

        eMappers.MAPPER_GATE_POSTERN => eStructs.STRUCT_GATE_POSTERN,
        eMappers.MAPPER_DRAWBRIDGE => eStructs.STRUCT_DRAWBRIDGE,
        eMappers.MAPPER_QUARRYPILE => eStructs.STRUCT_QUARRYPILE,
        eMappers.MAPPER_TOWER1 => eStructs.STRUCT_TOWER1,
        eMappers.MAPPER_TOWER2 => eStructs.STRUCT_TOWER2,
        eMappers.MAPPER_TOWER3 => eStructs.STRUCT_TOWER3,
        eMappers.MAPPER_TOWER4 => eStructs.STRUCT_TOWER4,
        eMappers.MAPPER_TOWER5 => eStructs.STRUCT_TOWER5,

        eMappers.MAPPER_TOWER1_DESTROYED or
        eMappers.MAPPER_MANGONEL => eStructs.STRUCT_TOWER1_DESTROYED,

        eMappers.MAPPER_TOWER2_DESTROYED or
        eMappers.MAPPER_BALLISTA => eStructs.STRUCT_TOWER2_DESTROYED,

        eMappers.MAPPER_TOWER3_DESTROYED => eStructs.STRUCT_TOWER3_DESTROYED,
        eMappers.MAPPER_TOWER4_DESTROYED => eStructs.STRUCT_TOWER4_DESTROYED,
        eMappers.MAPPER_TOWER5_DESTROYED => eStructs.STRUCT_TOWER5_DESTROYED,

        eMappers.MAPPER_GATE_WOOD1A or
        eMappers.MAPPER_GATE_WOOD1B or
        eMappers.MAPPER_GATE_WOOD1C or
        eMappers.MAPPER_GATE_WOOD1D => eStructs.STRUCT_GATE_WOOD,

        eMappers.MAPPER_GARDEN1 or eMappers.MAPPER_GARDEN2 or eMappers.MAPPER_GARDEN3 or
        eMappers.MAPPER_GARDEN4 or eMappers.MAPPER_GARDEN5 or eMappers.MAPPER_GARDEN6 or
        eMappers.MAPPER_GARDEN7 or eMappers.MAPPER_GARDEN8 or eMappers.MAPPER_GARDEN9 or
        eMappers.MAPPER_GARDEN10 or eMappers.MAPPER_GARDEN11 or eMappers.MAPPER_GARDEN12 => eStructs.STRUCT_GARDEN,

        eMappers.MAPPER_MAYPOLE => eStructs.STRUCT_MAYPOLE,
        eMappers.MAPPER_GALLOWS => eStructs.STRUCT_GALLOWS,
        eMappers.MAPPER_STOCKS => eStructs.STRUCT_STOCKS,
        eMappers.MAPPER_OUTPOST => eStructs.STRUCT_OUTPOST,
        eMappers.MAPPER_OUTPOST_ARAB => eStructs.STRUCT_OUTPOST_ARAB,
        eMappers.MAPPER_OIL_SMELTER => eStructs.STRUCT_OIL_SMELTER,
        eMappers.MAPPER_CATAPULT => eStructs.STRUCT_SIEGE_TENT_CATAPULT,
        eMappers.MAPPER_TREBUCHET => eStructs.STRUCT_SIEGE_TENT_TREBUCHET,
        eMappers.MAPPER_SIEGE_TOWER => eStructs.STRUCT_SIEGE_TENT_SIEGE_TOWER,
        eMappers.MAPPER_BATTERING_RAM => eStructs.STRUCT_SIEGE_TENT_BATTERING_RAM,
        eMappers.MAPPER_PORTABLE_SHIELD => eStructs.STRUCT_SIEGE_TENT_PORTABLE_SHIELD,

        eMappers.MAPPER_DOCK or eMappers.MAPPER_DOCK2 or
        eMappers.MAPPER_DOCK3 or eMappers.MAPPER_DOCK4 => eStructs.STRUCT_DOCK,

        eMappers.MAPPER_POND5 or eMappers.MAPPER_POND6 or eMappers.MAPPER_POND7 or eMappers.MAPPER_POND8 or
        eMappers.MAPPER_POND1 or eMappers.MAPPER_POND2 or eMappers.MAPPER_POND3 or eMappers.MAPPER_POND4 or
        eMappers.MAPPER_POND9_RAVINE1A or eMappers.MAPPER_POND10_RAVINE1B or eMappers.MAPPER_POND11_RAVINE1C or
        eMappers.MAPPER_POND12_RAVINE1AR or eMappers.MAPPER_POND13_RAVINE1BR or eMappers.MAPPER_POND14_RAVINE1CR or
        eMappers.MAPPER_POND15_RAVINE2A or eMappers.MAPPER_POND16_RAVINE2B or eMappers.MAPPER_POND17_RAVINE2C or
        eMappers.MAPPER_POND18_RAVINE2AR or eMappers.MAPPER_POND19_RAVINE2BR or eMappers.MAPPER_POND20_RAVINE2CR => eStructs.STRUCT_POND,

        eMappers.MAPPER_CESS_PIT1 or eMappers.MAPPER_CESS_PIT2 or
        eMappers.MAPPER_CESS_PIT3 or eMappers.MAPPER_CESS_PIT4 => eStructs.STRUCT_CESS_PIT,

        eMappers.MAPPER_BURNING_STAKE => eStructs.STRUCT_BURNING_STAKE,
        eMappers.MAPPER_GIBBET => eStructs.STRUCT_GIBBET,
        eMappers.MAPPER_DUNGEON => eStructs.STRUCT_DUNGEON,
        eMappers.MAPPER_RACK_STRETCHING => eStructs.STRUCT_RACK_STRETCHING,
        eMappers.MAPPER_RACK_FLOGGING => eStructs.STRUCT_RACK_FLOGGING,
        eMappers.MAPPER_CHOPPING_BLOCK => eStructs.STRUCT_CHOPPING_BLOCK,
        eMappers.MAPPER_DUNKING_STOOL => eStructs.STRUCT_DUNKING_STOOL,
        eMappers.MAPPER_DOG_CAGE => eStructs.STRUCT_DOG_CAGE,

        eMappers.MAPPER_STATUE1 or eMappers.MAPPER_STATUE2 or eMappers.MAPPER_STATUE3 or
        eMappers.MAPPER_STATUE4 or eMappers.MAPPER_STATUE5 => eStructs.STRUCT_STATUE,

        eMappers.MAPPER_SHRINE1 or eMappers.MAPPER_SHRINE2 or eMappers.MAPPER_SHRINE3 or
        eMappers.MAPPER_SHRINE4 or eMappers.MAPPER_SHRINE5 => eStructs.STRUCT_SHRINE,

        eMappers.MAPPER_BEE_HIVE => eStructs.STRUCT_BEE_HIVE,
        eMappers.MAPPER_DANCING_BEAR => eStructs.STRUCT_DANCING_BEAR,
        eMappers.MAPPER_BEAR_CAVE => eStructs.STRUCT_BEAR_CAVE,
        eMappers.MAPPER_WELL => eStructs.STRUCT_WELL,
        eMappers.MAPPER_WATERPOT => eStructs.STRUCT_WATERPOT,

        eMappers.MAPPER_PEOPLE_ARAB_BALLISTA or
        eMappers.MAPPER_ARAB_BALLISTA => eStructs.STRUCT_SIEGE_TENT_ARAB_BALLISTA,

        eMappers.MAPPER_RUINS1 or eMappers.MAPPER_RUINS2 or eMappers.MAPPER_RUINS3 or eMappers.MAPPER_RUINS4 or
        eMappers.MAPPER_RUINS5 or eMappers.MAPPER_RUINS6 or eMappers.MAPPER_RUINS7 or eMappers.MAPPER_RUINS8 or
        eMappers.MAPPER_RUINS9 or eMappers.MAPPER_RUINS10 or eMappers.MAPPER_RUINS11 or eMappers.MAPPER_RUINS12 or
        eMappers.MAPPER_RUINS13 or eMappers.MAPPER_RUINS14 or eMappers.MAPPER_RUINS15 or eMappers.MAPPER_RUINS16 or
        eMappers.MAPPER_RUINS17 or eMappers.MAPPER_RUINS18 or eMappers.MAPPER_RUINS19 or eMappers.MAPPER_RUINS20 or
        eMappers.MAPPER_RUINS21 or eMappers.MAPPER_RUINS22 or eMappers.MAPPER_RUINS23 or eMappers.MAPPER_RUINS24 or
        eMappers.MAPPER_RUINS25 or eMappers.MAPPER_RUINS26 or eMappers.MAPPER_RUINS27 or eMappers.MAPPER_RUINS28 or
        eMappers.MAPPER_RUINS29 or eMappers.MAPPER_RUINS30 or eMappers.MAPPER_RUINS31 or eMappers.MAPPER_RUINS32 or
        eMappers.MAPPER_RUINS33 or eMappers.MAPPER_RUINS34 => eStructs.STRUCT_RUINS,

        _ => eStructs.STRUCT_NULL,
    };

    /// <summary>
    /// Convert a eStructs enum to its primary eMappers equivalent.
    /// Note: Since multiple mappers can point to one struct (like variants of ruins/ponds), 
    /// this returns the default/first variant for that structure.
    /// </summary>
    /// <param name="st">The eStructs enum</param>
    /// <returns>The primary eMappers enum</returns>
    public static eMappers ConvertToEMappers(this eStructs st) => st switch
    {
        eStructs.STRUCT_TOWER => eMappers.MAPPER_TOWER,
        eStructs.STRUCT_FLETCHERS_WORKSHOP => eMappers.MAPPER_FLETCHER,
        eStructs.STRUCT_WOODCUTTERS_HUT => eMappers.MAPPER_WOODSMAN,
        eStructs.STRUCT_GOODS_YARD => eMappers.MAPPER_STORES,
        eStructs.STRUCT_OUTPOST_BEDOUIN => eMappers.MAPPER_OUTPOST_BEDOUIN,
        eStructs.STRUCT_HOVEL => eMappers.MAPPER_HOVEL,
        eStructs.STRUCT_OXEN_BASE => eMappers.MAPPER_OXENBASE,
        eStructs.STRUCT_QUARRY => eMappers.MAPPER_QUARRY,
        eStructs.STRUCT_TUNNEL_ENTERANCE => eMappers.MAPPER_TUNNEL,
        eStructs.STRUCT_SIGNPOST => eMappers.MAPPER_SIGNPOST,
        eStructs.STRUCT_KEEP_ONE => eMappers.MAPPER_KEEP1,
        eStructs.STRUCT_KEEP_TWO => eMappers.MAPPER_KEEP2,
        eStructs.STRUCT_KEEP_THREE => eMappers.MAPPER_KEEP3,
        eStructs.STRUCT_KEEP_FOUR => eMappers.MAPPER_KEEP4,
        eStructs.STRUCT_KEEP_FIVE => eMappers.MAPPER_KEEP5,
        eStructs.STRUCT_STABLES => eMappers.MAPPER_STABLES,
        eStructs.STRUCT_TUNNEL_CONSTRUCTION => eMappers.MAPPER_TUNNEL_CONSTRUCTION,
        eStructs.STRUCT_WHEATFARM => eMappers.MAPPER_WHEATFARM,
        eStructs.STRUCT_HOPSFARM => eMappers.MAPPER_HOPSFARM,
        eStructs.STRUCT_APPLEFARM => eMappers.MAPPER_APPLEFARM,
        eStructs.STRUCT_CATTLEFARM => eMappers.MAPPER_CATTLEFARM,
        eStructs.STRUCT_MILL => eMappers.MAPPER_MILL,
        eStructs.STRUCT_BAKERS_WORKSHOP => eMappers.MAPPER_BAKER,
        eStructs.STRUCT_BREWERS_WORKSHOP => eMappers.MAPPER_BREWER,
        eStructs.STRUCT_TRADEPOST => eMappers.MAPPER_TRADEPOST,
        eStructs.STRUCT_HUNTERS_HUT => eMappers.MAPPER_HUNTER,
        eStructs.STRUCT_BEDOUIN_STOCKADE => eMappers.MAPPER_BEDOUIN_STOCKADE,
        eStructs.STRUCT_GRANARY => eMappers.MAPPER_GRANARY,
        eStructs.STRUCT_ARMOURY => eMappers.MAPPER_ARMOURY,
        eStructs.STRUCT_POLETURNERS_WORKSHOP => eMappers.MAPPER_POLETURNER,
        eStructs.STRUCT_BLACKSMITHS_WORKSHOP => eMappers.MAPPER_BLACKSMITH,
        eStructs.STRUCT_ARMOURERS_WORKSHOP => eMappers.MAPPER_ARMOURER,
        eStructs.STRUCT_TANNERS_WORKSHOP => eMappers.MAPPER_TANNER,
        eStructs.STRUCT_BARRACKS_WOOD => eMappers.MAPPER_BARRACKS_WOOD,
        eStructs.STRUCT_BARRACKS_STONE => eMappers.MAPPER_BARRACKS_STONE,
        eStructs.STRUCT_ENGINEERS_GUILD => eMappers.MAPPER_ENGINEERS_GUILD,
        eStructs.STRUCT_TUNNELLERS_GUILD => eMappers.MAPPER_TUNNELERS_GUILD,
        eStructs.STRUCT_IRON_MINE => eMappers.MAPPER_IRON_MINE,
        eStructs.STRUCT_PITCH_DIGGER => eMappers.MAPPER_PITCH_WORKINGS,
        eStructs.STRUCT_INN => eMappers.MAPPER_INN,
        eStructs.STRUCT_HEALER => eMappers.MAPPER_HEALER,
        eStructs.STRUCT_SIEGE_TOWER => eMappers.MAPPER_SIEGE_TOWER_BASE,
        eStructs.STRUCT_CHURCH1 => eMappers.MAPPER_CHURCH1,
        eStructs.STRUCT_CHURCH2 => eMappers.MAPPER_CHURCH2,
        eStructs.STRUCT_CHURCH3 => eMappers.MAPPER_CHURCH3,
        eStructs.STRUCT_KILLING_PIT => eMappers.MAPPER_KILLING_PIT,
        eStructs.STRUCT_PITCH_DITCH => eMappers.MAPPER_PITCH_DITCH,
        eStructs.STRUCT_GATE_MAIN => eMappers.MAPPER_GATE_MAIN,
        eStructs.STRUCT_GATE_INNER => eMappers.MAPPER_GATE_INNER,
        eStructs.STRUCT_GATE_POSTERN => eMappers.MAPPER_GATE_POSTERN,
        eStructs.STRUCT_DRAWBRIDGE => eMappers.MAPPER_DRAWBRIDGE,
        eStructs.STRUCT_QUARRYPILE => eMappers.MAPPER_QUARRYPILE,
        eStructs.STRUCT_TOWER1 => eMappers.MAPPER_TOWER1,
        eStructs.STRUCT_TOWER2 => eMappers.MAPPER_TOWER2,
        eStructs.STRUCT_TOWER3 => eMappers.MAPPER_TOWER3,
        eStructs.STRUCT_TOWER4 => eMappers.MAPPER_TOWER4,
        eStructs.STRUCT_TOWER5 => eMappers.MAPPER_TOWER5,
        eStructs.STRUCT_TOWER1_DESTROYED => eMappers.MAPPER_TOWER1_DESTROYED,
        eStructs.STRUCT_TOWER2_DESTROYED => eMappers.MAPPER_TOWER2_DESTROYED,
        eStructs.STRUCT_TOWER3_DESTROYED => eMappers.MAPPER_TOWER3_DESTROYED,
        eStructs.STRUCT_TOWER4_DESTROYED => eMappers.MAPPER_TOWER4_DESTROYED,
        eStructs.STRUCT_TOWER5_DESTROYED => eMappers.MAPPER_TOWER5_DESTROYED,
        eStructs.STRUCT_GATE_WOOD => eMappers.MAPPER_GATE_WOOD,
        eStructs.STRUCT_GARDEN => eMappers.MAPPER_GARDEN1,
        eStructs.STRUCT_MAYPOLE => eMappers.MAPPER_MAYPOLE,
        eStructs.STRUCT_GALLOWS => eMappers.MAPPER_GALLOWS,
        eStructs.STRUCT_STOCKS => eMappers.MAPPER_STOCKS,
        eStructs.STRUCT_OUTPOST => eMappers.MAPPER_OUTPOST,
        eStructs.STRUCT_OUTPOST_ARAB => eMappers.MAPPER_OUTPOST_ARAB,
        eStructs.STRUCT_OIL_SMELTER => eMappers.MAPPER_OIL_SMELTER,
        eStructs.STRUCT_SIEGE_TENT_CATAPULT => eMappers.MAPPER_CATAPULT,
        eStructs.STRUCT_SIEGE_TENT_TREBUCHET => eMappers.MAPPER_TREBUCHET,
        eStructs.STRUCT_SIEGE_TENT_SIEGE_TOWER => eMappers.MAPPER_SIEGE_TOWER,
        eStructs.STRUCT_SIEGE_TENT_BATTERING_RAM => eMappers.MAPPER_BATTERING_RAM,
        eStructs.STRUCT_SIEGE_TENT_PORTABLE_SHIELD => eMappers.MAPPER_PORTABLE_SHIELD,
        eStructs.STRUCT_DOCK => eMappers.MAPPER_DOCK,
        eStructs.STRUCT_POND => eMappers.MAPPER_POND1,
        eStructs.STRUCT_CESS_PIT => eMappers.MAPPER_CESS_PIT1,
        eStructs.STRUCT_BURNING_STAKE => eMappers.MAPPER_BURNING_STAKE,
        eStructs.STRUCT_GIBBET => eMappers.MAPPER_GIBBET,
        eStructs.STRUCT_DUNGEON => eMappers.MAPPER_DUNGEON,
        eStructs.STRUCT_RACK_STRETCHING => eMappers.MAPPER_RACK_STRETCHING,
        eStructs.STRUCT_RACK_FLOGGING => eMappers.MAPPER_RACK_FLOGGING,
        eStructs.STRUCT_CHOPPING_BLOCK => eMappers.MAPPER_CHOPPING_BLOCK,
        eStructs.STRUCT_DUNKING_STOOL => eMappers.MAPPER_DUNKING_STOOL,
        eStructs.STRUCT_DOG_CAGE => eMappers.MAPPER_DOG_CAGE,
        eStructs.STRUCT_STATUE => eMappers.MAPPER_STATUE1,
        eStructs.STRUCT_SHRINE => eMappers.MAPPER_SHRINE1,
        eStructs.STRUCT_BEE_HIVE => eMappers.MAPPER_BEE_HIVE,
        eStructs.STRUCT_DANCING_BEAR => eMappers.MAPPER_DANCING_BEAR,
        eStructs.STRUCT_BEAR_CAVE => eMappers.MAPPER_BEAR_CAVE,
        eStructs.STRUCT_WELL => eMappers.MAPPER_WELL,
        eStructs.STRUCT_WATERPOT => eMappers.MAPPER_WATERPOT,
        eStructs.STRUCT_SIEGE_TENT_ARAB_BALLISTA => eMappers.MAPPER_ARAB_BALLISTA,
        eStructs.STRUCT_RUINS => eMappers.MAPPER_RUINS1,

        _ => eMappers.MAPPER_NULL
    };

    /// <summary>
    /// Convert a eMappers enum to a VegetationType enum.
    /// 1:1 mirror from c_game_convert_mapper_to_vegetation_type
    /// </summary>
    /// <param name="mv">The eMappers enum</param>
    /// <returns>The vegetationType enum</returns>
    public static VegetationType ConvertMapperToVegetationType(this eMappers mv) => mv switch
    {
        eMappers.MAPPER_CHESTNUT => VegetationType.ChestnutTree,
        eMappers.MAPPER_OAK => VegetationType.OakTree,
        eMappers.MAPPER_PINE => VegetationType.PineTree,
        eMappers.MAPPER_BIRCH => VegetationType.BirchTree,

        eMappers.MAPPER_SHRUB1A => VegetationType.Shrub1A,
        eMappers.MAPPER_SHRUB1B => VegetationType.Shrub1B,
        eMappers.MAPPER_SHRUB1C => VegetationType.Shrub1C,
        eMappers.MAPPER_SHRUB1D => VegetationType.Shrub1D,
        eMappers.MAPPER_SHRUB1E => VegetationType.Shrub1E,

        eMappers.MAPPER_SHRUB2A => VegetationType.Shrub2A,
        eMappers.MAPPER_SHRUB2B => VegetationType.Shrub2B_Unused,
        eMappers.MAPPER_SHRUB2C => VegetationType.Shrub2C_Unused,
        eMappers.MAPPER_SHRUB2D => VegetationType.Shrub2D_Unused,
        eMappers.MAPPER_SHRUB2E => VegetationType.Shrub2E_Unused,

        eMappers.MAPPER_SHRUB3A => VegetationType.Shrub3A,
        eMappers.MAPPER_SHRUB3B => VegetationType.Shrub3B,
        eMappers.MAPPER_SHRUB3C => VegetationType.Shrub3C,
        eMappers.MAPPER_SHRUB3D => VegetationType.Shrub3D,

        _ => VegetationType.None
    };
}