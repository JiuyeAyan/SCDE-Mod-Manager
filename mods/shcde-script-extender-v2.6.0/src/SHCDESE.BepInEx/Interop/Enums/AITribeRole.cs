using System;

namespace SHCDESE.Interop.Enums;

/// <summary>
/// Index into the per-player AI tribe ID/global-ID storage tables at GamePlayerResources+0x48A0/+0x4AF8.
/// Names describe the currently inferred purpose of each storage slot.
/// </summary>
public enum AITribeStorageRole16 : Int16
{
    CastleDefenseTribeA = 0,
    CastleDefenseTribeB = 1,
    MoatDiggers = 10,
    SiegeAssassins = 11,
    SiegeDiversion = 12,
    SiegeLaddermen = 13,
    SiegeTunnelers = 14,
    SiegeStorm1 = 15,
    SiegeStorm2 = 16,
    SiegeMoat = 17,
    SiegeEngineers = 18,

    ArcherDefensive = 20,
    CrossbowDefensive = 30,
    ArabSlingerDefensive = 40,
    ArabBowDefensive = 50,
    ArabGrenadierDefensive = 60,

    OilSmelterEngineer0 = 70,
    OilSmelterEngineer1= 71,
    OilSmelterEngineer2= 72,
    OilSmelterEngineer3 = 73,
    OilSmelterEngineer4 = 74,
    OilSmelterEngineer5 = 75,
    OilSmelterEngineer6 = 76,
    OilSmelterEngineer7 = 77,
    OilSmelterEngineer8 = 78,
    OilSmelterEngineer9 = 79,

    SpearmanDefensive = 80,
    MacemanDefensive = 90,
    Class13Defensive = 100,
    ArabHorsemanDefensive = 110,
    PikemanDefensive = 120,
    SwordsmanDefensive = 130,
    KnightDefensive = 140,
    AssassinDefensive = 150,
    ArabSwordsmanDefensive = 160,

    Unknown164 = 164,
    HarassmentSiegeTentEngineers = 165,
    EconomyProtection = 166,
    Bodyguards = 167,

    MobileDefensePatrol0 = 170,
    MobileDefensePatrol1 = 171,
    MobileDefensePatrol2 = 172,
    MobileDefensePatrol3 = 173,
    MobileDefensePatrol4 = 174,
    MobileDefensePatrol5 = 175,
    MobileDefensePatrol6 = 176,
    MobileDefensePatrol7 = 177,
    MobileDefensePatrol8 = 178,
    MobileDefensePatrol9 = 179,

    HarassmentCombat0 = 180,
    HarassmentCombat1 = 181,
    HarassmentCombat2 = 182,
    HarassmentCombat3 = 183,
    HarassmentCombat4 = 184,
    HarassmentCombat5 = 185,

    SiegeCover0 = 186,
    SiegeCover1 = 187,
    SiegeCover2 = 188,

    SiegeShock = 189,

    SiegeReserve0 = 190,
    SiegeReserve1 = 191,

    SiegeWall0 = 192,
    SiegeWall1 = 193,
    SiegeWall2 = 194,
    SiegeWall3 = 195,
    SiegeWall4 = 196,
    SiegeWall5 = 197,
    SiegeWall6 = 198,
    SiegeWall7 = 199,

    BedouinCamelLancerDefensive = 200,
    BedouinHealerDefensive = 210,
    BedouinEunuchDefensive = 220,
    BedouinAmbusherDefensive = 230,
    BedouinSkirmisherDefensive = 240,
    BedouinHeavyCamelDefensive = 250,
    BedouinSapperDefensive = 260,
    BedouinDemolisherDefensive = 270,
}
