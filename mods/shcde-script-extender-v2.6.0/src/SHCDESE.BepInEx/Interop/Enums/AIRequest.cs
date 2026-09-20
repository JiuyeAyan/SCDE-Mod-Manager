using System;

namespace SHCDESE.Interop.Enums;

/// <summary>
/// AI Request types
/// Used in c_game_player_ai_request_handler
/// </summary>
public enum AIRequest
{
    RequestOrder = 0,
    RequestGood = 1,
    SendGood = 2,
    RefuseSendGood = 3,
    OrderAgree = 4,
    OrderRefuse = 5
}