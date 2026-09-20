using System;
using System.Collections.Generic;
using System.Linq;
namespace SHCDESE.Interop.Enums;

/// <summary>
/// Controls the alternating sell/buy handler phase.
/// See c_game_ai_run_buy_and_sell_handler
/// </summary>
public enum AISellBuyPhase : Int32
{
    Buy = 0,
    Sell = 1
}
