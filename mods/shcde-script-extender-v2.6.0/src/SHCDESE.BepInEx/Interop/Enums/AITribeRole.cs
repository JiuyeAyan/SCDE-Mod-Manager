using System;

namespace SHCDESE.Interop.Enums;

/// <summary>
/// The AI has certain "role" ids for certain tribes during sieges or misc. this covers a subset of them that are currently known
/// (or perceived to be known)
/// The comments are below are likely members that are not confirmed yet.
/// Use in combination with gAITribeRoleMap
/// </summary>
public enum AITribeRole32 : Int32
{
    // map[10]=18: gets assigned to engineers in relation to sieges. (see: c_game_ai_reassign_unassigned_engineers_to_siege)
    SiegeStormTribe = 15,
    SiegeCoverTribe = 186,
    SiegeReserveTribe = 190,
    SiegeWallTribe = 192
}
public enum AITribeRole16 : Int16
{
    SiegeStormTribe = 15,
    SiegeCoverTribe = 186,
    SiegeReserveTribe = 190,
    SiegeWallTribe = 192
}
