using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.Core.Memory.Scanners;
using RedBird.X64.Assembly.InstructionWalker;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using RedBird.X64.Memory.Scanners;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Player;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.Logging;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;

namespace SHCDESE.Detours;

[SuppressUnmanagedCodeSecurity]
internal unsafe class BulkPlayerDetours
{
    private HookTransaction? tx;
    public BulkPlayerDetours(ReadOnlySpan<byte> memory, ScanRegion region)
    {
        LogHelper.Information($"Applying");

        UInt64 currentImageBase = (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle;
        tx ??= new HookTransaction(region, Plugin.Instance.LoggerFactory);
        DataScanner scanner = DataScanner.Create(region);

        tx.AddDetour(c_game_player_subtract_resources_hook,
            "45 85 C0 0F 84 ? ? ? ? 44 89 4C 24",
            c_game_player_subtract_resources_hook_impl);

        tx.AddDetour(c_game_player_add_resources_hook,
            "89 54 24 ?? 48 89 4C 24 ?? 53 55 56 41 54",
            c_game_player_add_resources_hook_impl);

        tx.AddDetour(c_game_player_market_buy_hook,
            "48 89 5C 24 ?? 48 89 6C 24 ?? 56 57 41 56 48 83 EC ?? 45 85 C9",
            c_game_player_market_buy_hook_impl);

        tx.AddDetour(c_game_player_calculate_drunk_percentage_hook,
            "48 63 C2 48 69 D0 ? ? ? ? 8B 84 0A ? ? ? ? 85 C0",
            c_game_player_calculate_drunk_percentage_hook_impl);

        tx.AddDetour(c_game_ai_request_eval_defend_order_hook,
            "48 83 EC ? 48 63 C2 4C 8D 1D ? ? ? ? 4C 69 D0 ? ? ? ? 4C 8B C9",
            c_game_ai_request_eval_defend_order_hook_impl);

        tx.AddDetour(c_game_ai_request_eval_attack_order_hook,
            "48 83 EC ? 4C 63 DA 4C 8D 05",
            c_game_ai_request_eval_attack_order_hook_impl);

        tx.AddDetour(c_game_ai_can_spare_resource_amount_hook,
            "48 89 5C 24 ? 48 89 7C 24 ? 4C 63 D2 48 8D 3D ? ? ? ? 49 69 C2 ? ? ? ? 48 8B D9",
            c_game_ai_can_spare_resource_amount_hook_impl);

        tx.AddDetour(c_game_player_ai_request_good_hook,
            "48 89 5C 24 ? 48 89 74 24 ? 57 41 54 41 55 41 56 41 57 48 83 EC ? 48 63 F1",
            c_game_player_ai_request_good_hook_impl);

        tx.AddDetour(c_game_player_ai_send_good_hook,
            "40 55 56 41 54 48 83 EC",
            c_game_player_ai_send_good_hook_impl);

        tx.AddDetour(c_game_calculate_tax_income_hook,
            "33 C0 4C 63 D2",
            c_game_calculate_tax_income_hook_impl);

        tx.AddDetour(c_game_calculate_donation_expense_hook,
            "48 63 C2 48 69 D0 ? ? ? ? B8 ? ? ? ? 41 2B C0",
            c_game_calculate_donation_expense_hook_impl);

        tx.AddContextHook(c_game_player_food_consumption_update_baseconsumptionrate,
            "8B 83 ? ? ? ? 8D 0C 40",
            static ctx =>
            {
                //mov     eax, [rbx+1FACh] // total population of player
                //lea     ecx, [rax+rax*2] // food consumption rate (3 * total pops)
                //mov     [rbx+1EDCh], ecx
                //cmp     cs:gLocalPlayerState_2, ebp
                //jz      short loc_1800CCFCB
                //cmp     dword ptr [rsi-78h], 0FFFFFFFFh
                //jnz     short loc_1800CCFCB
                int playerId = (int)ctx.Pointer->RDI;
                LogHelper.Verbose($"c_game_player_food_consumption_update_baseconsumptionrate: playerId={playerId}");

                if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesByIdEx(playerId, out NativePointer<GamePlayerResources> playerRes))
                {
                    LogHelper.Error($"Failed to get player resources for playerid: {playerId}");
                    return;
                }
                int foodConsumptionRate = GamePlayerManagerAPI.Instance.FoodConsumptionRate.GetValue();
                int totalPopulationForPlayer = (int)playerRes.Pointer->r_TotalPopulation;
                int foodConsumptionThisTick = totalPopulationForPlayer * foodConsumptionRate;

                PlayerFoodConsumptionTickEventArgs eventArgs = new(EventHookPhase.Pre, playerId, foodConsumptionThisTick);
                PlayerR3EventHooks.OnPlayerFoodConsumptionTick.Raise(eventArgs);

                ctx.Pointer->RCX = (UInt64)eventArgs.FoodConsumptionThisTick;

                PlayerFoodConsumptionTickEventArgs postEventArgs = new(EventHookPhase.Post, playerId, eventArgs.FoodConsumptionThisTick);
                PlayerR3EventHooks.OnPlayerFoodConsumptionTick.Raise(postEventArgs);

            }, new RedBird.X64.Hooks.Context.ContextHookOptions()
            {
                Registers = RedBird.X64.Assembly.X64SmartCPUContextRegs.Volatile | RedBird.X64.Assembly.X64SmartCPUContextRegs.RDI,
                InstructionSelector = instrs =>
                {
                    return instrs.Skip(2).ToArray();
                }
            });

        tx.AddContextHook(c_game_add_tax_money_addgold,
            "01 93 ? ? ? ? 01 55",
            static ctx =>
            {
                int income = (int)ctx.Pointer->RDX;
                int playerId = (int)ctx.Pointer->RDI;

                LogHelper.Verbose($"c_game_add_tax_money_addgold: playerId={playerId}, income={income}");
                PlayerTaxIncomeEventArgs eventArgs = new(EventHookPhase.Pre, playerId, income);
                PlayerR3EventHooks.OnPlayerTaxIncome.Raise(eventArgs);

                ctx.Pointer->RDX = (UInt64)eventArgs.Income;

            }, new RedBird.X64.Hooks.Context.ContextHookOptions() { Registers = RedBird.X64.Assembly.X64SmartCPUContextRegs.Volatile | RedBird.X64.Assembly.X64SmartCPUContextRegs.RDI });

        tx.AddContextHook(c_game_add_tax_money_removegold,
            "29 93",
            static ctx =>
            {
                int expenditure = -(int)ctx.Pointer->RDX;
                int playerId = (int)ctx.Pointer->RDI;

                LogHelper.Verbose($"c_game_add_tax_money_removegold: playerId={playerId}, expenditure={expenditure}");
                PlayerTaxExpenditureEventArgs eventArgs = new(EventHookPhase.Pre, playerId, expenditure);
                PlayerR3EventHooks.OnPlayerTaxExpenditure.Raise(eventArgs);

                ctx.Pointer->RDX = (UInt64)eventArgs.Expenditure;

            }, new RedBird.X64.Hooks.Context.ContextHookOptions() { Registers = RedBird.X64.Assembly.X64SmartCPUContextRegs.Volatile | RedBird.X64.Assembly.X64SmartCPUContextRegs.RDI });

        // void __fastcall c_game_update_player_popularity(__int64 pPlayerManager)
        // Since a lot of popularity values were hardcoded and the compiler did a number on these individual numbers, I had to get creative.
        // So theres now a big context hook chunk sitting in the middle of the function, replicating what the game does to a certain extent.
        // With the added bonus of un-hardcoding the values and making them configurable via the API (yay)
        // May god have mercy on your soul if they ever change something and the compiler decides to do some restructuring on this function.

        tx.AddContextHook(c_game_update_player_popularity,
            "41 8B 94 2C ? ? ? ? 45 8B 8C 2C",
            static ctx =>
            {
                // rbp: playerOffset
                // r15: playerId
                int playerId = (int)ctx.Pointer->R15;

                if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(playerId, out GamePlayerResources* res))
                {
                    LogHelper.Error($"Could not find player by id: {playerId}");
                    return;
                }
                LogHelper.Verbose($"c_game_update_player_popularity, playerId={playerId}, resAddr={new IntPtr(res).ToString("X16")}");

                GameGlobalsManager globals = GameGlobalsManager.Instance;

                int r_CurrentPopularity = (int)res->r_CurrentPopularity;
                int r_TotalPopulation = (int)res->r_TotalPopulation;
                int r_PreferredFoodType = (int)res->r_PreferredFoodType;
                res->r_PreviousPopularity = (uint)r_CurrentPopularity;
                int rationsPopModifier = 0;

                if (r_PreferredFoodType > 0)
                    res->r_UnknownFoodRelated = 0;
                else
                    ++res->r_UnknownFoodRelated;
                if (r_TotalPopulation > 0)
                {
                    if (r_PreferredFoodType > 0)
                    {
                        RationsMode r_RationsMode = res->r_RationMode;
                        switch (r_RationsMode)
                        {
                            case RationsMode.None:
                                rationsPopModifier = globals.RationsPopModifierNone.GetValue();
                                break;
                            case RationsMode.Half:
                                rationsPopModifier = globals.RationsPopModifierHalf.GetValue();
                                break;
                            case RationsMode.Full:
                                rationsPopModifier = globals.RationsPopModifierFull.GetValue();
                                break;
                            case RationsMode.Double:
                                rationsPopModifier = globals.RationsPopModifierDouble.GetValue();
                                break;
                            case RationsMode.Extra:
                                rationsPopModifier = globals.RationsPopModifierExtra.GetValue();
                                break;
                        }
                    }
                    else
                    {
                        rationsPopModifier = globals.RationsPopModifierNoPreferredFood.GetValue();
                    }
                }
                else
                {
                    rationsPopModifier = globals.RationsPopModifierNoPopulation.GetValue();
                }

                int r_FoodVarietyAmount = (int)res->r_FoodVarietyAmount;
                switch (r_FoodVarietyAmount)
                {
                    case 2:
                        rationsPopModifier += globals.FoodVarietyPopModifierTwoTypes.GetValue();
                        break;
                    case 3:
                        rationsPopModifier += globals.FoodVarietyPopModifierThreeTypes.GetValue();
                        break;
                    case 4:
                        rationsPopModifier += globals.FoodVarietyPopModifierFourTypes.GetValue();
                        break;
                }

                int r_OverpopulationRatioPercent = (int)res->r_OverpopulationRatioPercent;
                int v14 = r_CurrentPopularity + rationsPopModifier;
                res->r_RationsPopularityModifier = rationsPopModifier;
                int overpopulationPopModifier = 0;
                int v15 = (int)((uint)rationsPopModifier >> 31);
                if (rationsPopModifier >= 0)
                    rationsPopModifier = 0;
                if (r_OverpopulationRatioPercent > globals.OverpopulationRatioThresholdTierOne.GetValue())
                {
                    if (r_OverpopulationRatioPercent > globals.OverpopulationRatioThresholdTierTwo.GetValue())
                    {
                        if (r_OverpopulationRatioPercent > globals.OverpopulationRatioThresholdTierThree.GetValue())
                        {
                            if (r_OverpopulationRatioPercent > globals.OverpopulationRatioThresholdTierFour.GetValue())
                            {
                                overpopulationPopModifier = globals.OverpopulationPopModifierTierFive.GetValue();
                                if (r_OverpopulationRatioPercent <= globals.OverpopulationRatioThresholdTierFive.GetValue())
                                    overpopulationPopModifier = globals.OverpopulationPopModifierTierFour.GetValue();
                            }
                            else
                            {
                                overpopulationPopModifier = globals.OverpopulationPopModifierTierThree.GetValue();
                            }
                        }
                        else
                        {
                            overpopulationPopModifier = globals.OverpopulationPopModifierTierTwo.GetValue();
                        }
                    }
                    else
                    {
                        overpopulationPopModifier = globals.OverpopulationPopModifierTierOne.GetValue();
                    }
                }
                else
                {
                    overpopulationPopModifier = 0;
                }
                res->r_OvercrowdingPopularityModifier = overpopulationPopModifier;
                res->r_CurrentPopularity = (uint)(v14 + overpopulationPopModifier);
                if (overpopulationPopModifier < rationsPopModifier)
                {
                    rationsPopModifier = overpopulationPopModifier;
                    v15 = 2;
                }
                TaxesMode r_TaxesMode = res->r_TaxesMode;
                int r_TotalGoodsGold = (int)res->r_TotalGoodsGold;
                int taxesPopModifier = 0;
                if ((int)r_TaxesMode >= 3 || r_TotalGoodsGold > 0)
                {
                    switch (r_TaxesMode)
                    {
                        case TaxesMode.HugeDonation:
                            taxesPopModifier = globals.TaxPopModifierHugeDonation.GetValue();
                            break;
                        case TaxesMode.BigDonation:
                            taxesPopModifier = globals.TaxPopModifierBigDonation.GetValue();
                            break;
                        case TaxesMode.SmallDonation:
                            taxesPopModifier = globals.TaxPopModifierSmallDonation.GetValue();
                            break;
                        case TaxesMode.None:
                            taxesPopModifier = globals.TaxPopModifierNone.GetValue();
                            break;
                        case TaxesMode.Low:
                            taxesPopModifier = globals.TaxPopModifierLow.GetValue();
                            break;
                        case TaxesMode.Moderate:
                            taxesPopModifier = globals.TaxPopModifierModerate.GetValue();
                            break;
                        case TaxesMode.High:
                            taxesPopModifier = globals.TaxPopModifierHigh.GetValue();
                            break;
                        case TaxesMode.Mean:
                            taxesPopModifier = globals.TaxPopModifierMean.GetValue();
                            break;
                        case TaxesMode.Usurious:
                            taxesPopModifier = globals.TaxPopModifierUsurious.GetValue();
                            break;
                        case TaxesMode.Cruel:
                            taxesPopModifier = globals.TaxPopModifierCruel.GetValue();
                            break;
                        case TaxesMode.MostCruel:
                            taxesPopModifier = globals.TaxPopModifierMostCruel.GetValue();
                            break;
                        case TaxesMode.Cruellest:
                            taxesPopModifier = globals.TaxPopModifierCruellest.GetValue();
                            break;
                        default:
                            taxesPopModifier = globals.TaxPopModifierUnknownMode.GetValue();
                            break;
                    }
                }
                else
                {
                    taxesPopModifier = globals.TaxPopModifierUnaffordableDonation.GetValue();
                }
                int v19 = taxesPopModifier + (int)res->r_CurrentPopularity;
                res->r_TaxPopularityModifier = taxesPopModifier;
                if (taxesPopModifier < rationsPopModifier)
                {
                    rationsPopModifier = taxesPopModifier;
                    v15 = 4;
                }
                res->r_UnknownPopularityRelated = 0;
                int v20 = 0;
                if (rationsPopModifier <= 0)
                    v20 = rationsPopModifier;
                else
                    v15 = 3;
                int v21 = (int)res->r_IsPopularityMaxEvent != 0 ? globals.MaxPopularityEventPopModifier.GetValue() : 0;
                int v22 = v21 + v19;
                res->r_EventPopularityBoostModifier = unchecked((uint)v21);
                res->r_CurrentPopularity = unchecked((uint)v22);
                if (v21 < v20)
                {
                    v20 = v21;
                    v15 = 5;
                }
                int r_BlessedCiviliansPercent2 = (int)res->r_BlessedCiviliansPercent2;
                int religionPopModifier = 0;
                if (r_BlessedCiviliansPercent2 > globals.ReligionPercentThresholdTierOne.GetValue())
                {
                    if (r_BlessedCiviliansPercent2 > globals.ReligionPercentThresholdTierTwo.GetValue())
                    {
                        if (r_BlessedCiviliansPercent2 > globals.ReligionPercentThresholdTierThree.GetValue())
                        {
                            religionPopModifier = globals.ReligionPopModifierTierFour.GetValue();
                            if (r_BlessedCiviliansPercent2 <= globals.ReligionPercentThresholdTierFour.GetValue())
                                religionPopModifier = globals.ReligionPopModifierTierThree.GetValue();
                        }
                        else
                        {
                            religionPopModifier = globals.ReligionPopModifierTierTwo.GetValue();
                        }
                    }
                    else
                    {
                        religionPopModifier = globals.ReligionPopModifierTierOne.GetValue();
                    }
                }
                else
                {
                    religionPopModifier = 0;
                }
                int v25 = religionPopModifier + globals.ChurchPopModifier.GetValue();
                if (res->r_Churches == 0)
                    v25 = religionPopModifier;
                int v26 = v25 + globals.CathedralPopModifier.GetValue();
                if (res->r_Cathedrals == 0)
                    v26 = v25;
                int v27 = v22 + v26;
                res->r_ChurchPopularityModifier = (uint)v26;
                res->r_UnknownPopularityAccumulationByCurrentDividedBy25 += (uint)v26 / 25;
                res->r_CurrentPopularity = (uint)(v22 + v26);
                if (v26 < v20)
                {
                    v20 = v26;
                    v15 = 6;
                }
                int r_DrunkCiviliansPercent = c_game_player_calculate_drunk_percentage_hook_impl(GamePlayerManagerAPI.Instance.GetPlayerManager(), playerId);
                res->r_DrunkCiviliansPercent = (uint)r_DrunkCiviliansPercent;
                int v29 = r_DrunkCiviliansPercent;
                int drunkPopModifier = 0;
                if (r_DrunkCiviliansPercent >= globals.AlePercentThresholdTierOne.GetValue())
                {
                    if (r_DrunkCiviliansPercent >= globals.AlePercentThresholdTierTwo.GetValue())
                    {
                        if (r_DrunkCiviliansPercent >= globals.AlePercentThresholdTierThree.GetValue())
                        {
                            drunkPopModifier = globals.AlePopModifierTierFour.GetValue();
                            if (v29 < globals.AlePercentThresholdTierFour.GetValue())
                                drunkPopModifier = globals.AlePopModifierTierThree.GetValue();
                        }
                        else
                        {
                            drunkPopModifier = globals.AlePopModifierTierTwo.GetValue();
                        }
                    }
                    else
                    {
                        drunkPopModifier = globals.AlePopModifierTierOne.GetValue();
                    }
                }
                else
                {
                    drunkPopModifier = 0;
                }
                res->r_AlePopularityModifier = (uint)drunkPopModifier;
                int v31 = v27 + drunkPopModifier;
                if (drunkPopModifier < v20)
                {
                    v20 = drunkPopModifier;
                    v15 = 7;
                }
                int r_GoodBadThingBoost = res->r_GoodBadThingBoost;
                if (r_GoodBadThingBoost < 1)
                {
                    if (r_GoodBadThingBoost > -1)
                        rationsPopModifier = 0;
                    else
                        rationsPopModifier = globals.GoodBadThingPopModifierMultiplier.GetValue() * r_GoodBadThingBoost;
                }
                else
                {
                    rationsPopModifier = globals.GoodBadThingPopModifierMultiplier.GetValue() * r_GoodBadThingBoost;
                }
                res->r_RationsPopularityModifier2 = (uint)rationsPopModifier;
                int v33 = v31 + rationsPopModifier;
                if (rationsPopModifier < v20)
                    v15 = 8;

                PlayerCalculatePopularityEventArgs eventArgs = new(EventHookPhase.Pre, playerId, v33);
                PlayerR3EventHooks.OnPlayerCalculatePopularity.Raise(eventArgs);
                v33 = eventArgs.Popularity;

                res->r_CurrentPopularity = (uint)v33;

                ctx.Pointer->RDX = unchecked((uint)v33); // EDX = current popularity
                ctx.Pointer->RSI = unchecked((uint)v15); // ESI = worst modifier source?

            }, new RedBird.X64.Hooks.Context.ContextHookOptions()
            {
                Registers = RedBird.X64.Assembly.X64SmartCPUContextRegs.Volatile | RedBird.X64.Assembly.X64SmartCPUContextRegs.R15 | RedBird.X64.Assembly.X64SmartCPUContextRegs.RSI,
                Placement = RedBird.Abstractions.Hooks.OverwrittenInstructionPlacement.Suppress,
                HookSize = 0x347
            });

        tx.Commit();

    }

    internal static HookHandle<X64InlineHook> c_game_add_tax_money_removegold = new();
    internal static HookHandle<X64InlineHook> c_game_add_tax_money_addgold = new();

    internal static HookHandle<X64InlineHook> c_game_update_player_popularity = new();

    internal static HookHandle<X64InlineHook> c_game_player_food_consumption_update_baseconsumptionrate = new();

    //
    // __int64 __fastcall c_game_calculate_donation_expense(__int64 pPlayerManager, int playerId, int taxesMode, int totalPopulation)
    // 48 63 C2 48 69 D0 ? ? ? ? B8 ? ? ? ? 41 2B C0
    //
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int c_game_calculate_donation_expense_delegate(IntPtr pPlayerManager, int playerId, TaxesMode taxesMode, int population);
    internal static DetourHandle<c_game_calculate_donation_expense_delegate> c_game_calculate_donation_expense_hook = new();
    public static int c_game_calculate_donation_expense_hook_impl(IntPtr pPlayerManager, int playerId, TaxesMode taxesMode, int population)
    {
        LogHelper.Verbose($"playerId={playerId}, taxesMode={taxesMode}, population={population}");

        PlayerCalculateExpenditureEventArgs eventArgs = new(EventHookPhase.Pre, playerId, taxesMode, population);
        PlayerR3EventHooks.OnPlayerCalculateExpenditure.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            int originalResult = c_game_calculate_donation_expense_hook.Original(
                pPlayerManager,
                eventArgs.PlayerId,
                eventArgs.TaxesMode,
                eventArgs.Population
            );
            eventArgs.ReturnValue = originalResult;
            PlayerCalculateExpenditureEventArgs postEventArgs = new(EventHookPhase.Post, playerId, taxesMode, population)
            {
                ReturnValue = originalResult
            };
            PlayerR3EventHooks.OnPlayerCalculateExpenditure.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }

    //
    // __int64 __fastcall c_game_calculate_tax_income(__int64 pPlayerManager, int playerId, int taxesMode, int taxablePopulation)
    // 33 C0 4C 63 D2
    //
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int c_game_calculate_tax_income_delegate(IntPtr pPlayerManager, int playerId, TaxesMode taxesMode, int population);
    internal static DetourHandle<c_game_calculate_tax_income_delegate> c_game_calculate_tax_income_hook = new();
    public static int c_game_calculate_tax_income_hook_impl(IntPtr pPlayerManager, int playerId, TaxesMode taxesMode, int population)
    {
        LogHelper.Verbose($"playerId={playerId}, taxesMode={taxesMode}, population={population}");

        PlayerCalculateTaxesEventArgs eventArgs = new(EventHookPhase.Pre, playerId, taxesMode, population);
        PlayerR3EventHooks.OnPlayerCalculateTaxes.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            int originalResult = c_game_calculate_tax_income_hook.Original(
                pPlayerManager,
                eventArgs.PlayerId,
                eventArgs.TaxesMode,
                eventArgs.Population
            );
            eventArgs.ReturnValue = originalResult;
            PlayerCalculateTaxesEventArgs postEventArgs = new(EventHookPhase.Post, playerId, taxesMode, population)
            {
                ReturnValue = originalResult
            };
            PlayerR3EventHooks.OnPlayerCalculateTaxes.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }

    //
    // int __fastcall c_game_player_ai_send_good(signed int targetPlayerId, unsigned int goodType, int amount, int sourcePlayerId)
    // 40 55 56 41 54 48 83 EC
    //
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int c_game_player_ai_send_good_delegate(int targetPlayerId, eGoods goodType, int amount, int sourcePlayerId);
    internal static DetourHandle<c_game_player_ai_send_good_delegate> c_game_player_ai_send_good_hook = new();
    public static int c_game_player_ai_send_good_hook_impl(int targetPlayerId, eGoods goodType, int amount, int sourcePlayerId)
    {
        LogHelper.Debug($"sourcePlayerId={sourcePlayerId}, targetPlayerId={targetPlayerId}, goodType={goodType} amount={amount}");

        PlayerAIRequestReceivedGoodsEventArg eventArgs = new(EventHookPhase.Pre, sourcePlayerId, targetPlayerId, goodType, amount);
        PlayerR3EventHooks.OnPlayerAIRequestReceivedGoods.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            int originalResult = c_game_player_ai_send_good_hook.Original!(
                eventArgs.TargetPlayerId,
                eventArgs.Good,
                eventArgs.Amount,
                eventArgs.SourcePlayerId
            );
            eventArgs.ReturnValue = originalResult == 1;
            PlayerAIRequestReceivedGoodsEventArg postEventArgs = new(EventHookPhase.Post, sourcePlayerId, targetPlayerId, goodType, amount)
            {
                ReturnValue = originalResult == 1
            };
            PlayerR3EventHooks.OnPlayerAIRequestReceivedGoods.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue == true ? 1 : 0;
    }


    //
    // void __fastcall c_game_player_ai_request_good(int sourcePlayerId, int goodType, int amount, int targetPlayerId)
    // 48 89 5C 24 ? 48 89 74 24 ? 57 41 54 41 55 41 56 41 57 48 83 EC ? 48 63 F1
    //
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void c_game_player_ai_request_good_delegate(int sourcePlayerId, eGoods goodType, int amount, int targetPlayerId);
    internal static DetourHandle<c_game_player_ai_request_good_delegate> c_game_player_ai_request_good_hook = new();
    public static void c_game_player_ai_request_good_hook_impl(int sourcePlayerId, eGoods goodType, int amount, int targetPlayerId)
    {
        LogHelper.Debug($"sourcePlayerId={sourcePlayerId}, targetPlayerId={targetPlayerId}, goodType={goodType} amount={amount}");

        PlayerAIRequestGoodsEventArgs eventArgs = new(EventHookPhase.Pre, sourcePlayerId, targetPlayerId, goodType, amount);
        PlayerR3EventHooks.OnPlayerAIRequestGoods.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            c_game_player_ai_request_good_hook.Original!(
                eventArgs.SourcePlayerId,
                eventArgs.Good,
                eventArgs.Amount,
                eventArgs.TargetPlayerId
            );
            PlayerAIRequestGoodsEventArgs postEventArgs = new(EventHookPhase.Post, sourcePlayerId, targetPlayerId, goodType, amount);
            PlayerR3EventHooks.OnPlayerAIRequestGoods.Raise(postEventArgs);
        }
    }

    //
    // _BOOL8 __fastcall c_game_ai_can_spare_resource_amount(__int64 pLordManager, int targetPlayerId, int goodType, int amount)
    // 48 89 5C 24 ? 48 89 7C 24 ? 4C 63 D2 48 8D 3D ? ? ? ? 49 69 C2 ? ? ? ? 48 8B D9
    //
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int c_game_ai_can_spare_resource_amount_delegate(IntPtr pLordManager, int targetPlayerId, eGoods goodType, int amount);
    internal static DetourHandle<c_game_ai_can_spare_resource_amount_delegate> c_game_ai_can_spare_resource_amount_hook = new();
    public static int c_game_ai_can_spare_resource_amount_hook_impl(IntPtr pLordManager, int targetPlayerId, eGoods goodType, int amount)
    {
        LogHelper.Debug($"targetPlayerId={targetPlayerId}, goodType={goodType} amount={amount}");

        PlayerAIEvaluateCanSpareGoods eventArgs = new(EventHookPhase.Pre, targetPlayerId, goodType, amount);
        PlayerR3EventHooks.OnPlayerAIEvaluateCanSpareGoods.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            int originalResult = c_game_ai_can_spare_resource_amount_hook.Original!(
                pLordManager,
                eventArgs.TargetPlayerId,
                eventArgs.Good,
                eventArgs.Amount
            );
            eventArgs.ReturnValue = originalResult == 1;
            PlayerAIEvaluateCanSpareGoods postEventArgs = new(EventHookPhase.Post, targetPlayerId, goodType, amount)
            {
                ReturnValue = originalResult == 1
            };
            PlayerR3EventHooks.OnPlayerAIEvaluateCanSpareGoods.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue == true ? 1 : 0;
    }

    //
    // __int64 __fastcall c_game_ai_request_eval_defend_order(__int64 pLordManager, __int64 targetPlayerId, __int64 sourcePlayerId)
    // 48 83 EC ? 48 63 C2 4C 8D 1D ? ? ? ? 4C 69 D0 ? ? ? ? 4C 8B C9
    //
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int c_game_ai_request_eval_defend_order_delegate(IntPtr pLordManager, int targetPlayerId, int sourcePlayerId);
    internal static DetourHandle<c_game_ai_request_eval_defend_order_delegate> c_game_ai_request_eval_defend_order_hook = new();
    public static int c_game_ai_request_eval_defend_order_hook_impl(IntPtr pLordManager, int targetPlayerId, int sourcePlayerId)
    {
        LogHelper.Debug($"targetPlayerId={targetPlayerId}, sourcePlayerId={sourcePlayerId}");

        PlayerAIEvaluateDefendOrder eventArgs = new(EventHookPhase.Pre, targetPlayerId, sourcePlayerId);
        PlayerR3EventHooks.OnPlayerAIEvaluateDefendOrder.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            int originalResult = c_game_ai_request_eval_defend_order_hook.Original!(
                pLordManager,
                eventArgs.TargetPlayerId,
                eventArgs.SourcePlayerId
            );
            eventArgs.ReturnValue = originalResult == 1;
            PlayerAIEvaluateDefendOrder postEventArgs = new(EventHookPhase.Post, targetPlayerId, sourcePlayerId)
            {
                ReturnValue = originalResult == 1
            };
            PlayerR3EventHooks.OnPlayerAIEvaluateDefendOrder.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue == true ? 1 : 0;
    }

    //
    // __int64 __fastcall c_game_ai_request_eval_attack_order(__int64 pLordManager, int targetPlayerId, __int64 contextPlayerId, int sourcePlayerId)
    // 48 83 EC ? 4C 63 DA 4C 8D 05
    //
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int c_game_ai_request_eval_attack_order_delegate(IntPtr pLordManager, int targetPlayerId, int contextPlayerId, int sourcePlayerId);
    internal static DetourHandle<c_game_ai_request_eval_attack_order_delegate> c_game_ai_request_eval_attack_order_hook = new();
    public static int c_game_ai_request_eval_attack_order_hook_impl(IntPtr pLordManager, int targetPlayerId, int contextPlayerId, int sourcePlayerId)
    {
        LogHelper.Debug($"targetPlayerId={targetPlayerId}, contextPlayerId={contextPlayerId}, sourcePlayerId={sourcePlayerId}");

        PlayerAIEvaluateAttackOrder eventArgs = new(EventHookPhase.Pre, targetPlayerId, sourcePlayerId, contextPlayerId);
        PlayerR3EventHooks.OnPlayerAIEvaluateAttackOrder.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            int originalResult = c_game_ai_request_eval_attack_order_hook.Original!(
                pLordManager,
                eventArgs.TargetPlayerId,
                eventArgs.ContextPlayerId,
                eventArgs.SourcePlayerId
            );
            eventArgs.ReturnValue = originalResult == 1;
            PlayerAIEvaluateAttackOrder postEventArgs = new(EventHookPhase.Post, targetPlayerId, sourcePlayerId, contextPlayerId)
            {
                ReturnValue = originalResult == 1
            };
            PlayerR3EventHooks.OnPlayerAIEvaluateAttackOrder.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue == true ? 1 : 0;
    }

    //
    // __int64 __fastcall c_game_player_calculate_drunk_percentage(__int64 playerResource, int playerid)
    // 48 63 C2 48 69 D0 ? ? ? ? 8B 84 0A ? ? ? ? 85 C0
    //
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int c_game_player_calculate_drunk_percentage_delegate(IntPtr pPlayerResource, int playerId);
    internal static DetourHandle<c_game_player_calculate_drunk_percentage_delegate> c_game_player_calculate_drunk_percentage_hook = new();
    public static int c_game_player_calculate_drunk_percentage_hook_impl(IntPtr pPlayerResource, int playerId)
    {
        //LogHelper.Debug($"playerId={playerId}, bSell={bSell}, good={good}, a4={a4}");

        PlayerCalculateDrunkPercentageEventArgs eventArgs = new(EventHookPhase.Pre, playerId);
        PlayerR3EventHooks.OnPlayerCalculateDrunkPercentage.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            int originalResult = c_game_player_calculate_drunk_percentage_hook.Original!(
                pPlayerResource,
                eventArgs.PlayerId
            );
            eventArgs.ReturnValue = originalResult;
            PlayerCalculateDrunkPercentageEventArgs postEventArgs = new(EventHookPhase.Post, playerId)
            {
                ReturnValue = originalResult
            };
            PlayerR3EventHooks.OnPlayerCalculateDrunkPercentage.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }

    //
    // void __fastcall c_game_player_market_buy(unsigned int playerId, int bSell, Goods good, int bShiftModifier)
    // 48 89 5C 24 ?? 48 89 6C 24 ?? 56 57 41 56 48 83 EC ?? 45 85 C9
    //
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void c_game_player_market_buy_delegate(int playerId, int bSell, eGoods good, int bShiftModifier);
    internal static DetourHandle<c_game_player_market_buy_delegate> c_game_player_market_buy_hook = new();
    public static void c_game_player_market_buy_hook_impl(int playerId, int bSell, eGoods good, int bShiftModifier)
    {
        //LogHelper.Debug($"playerId={playerId}, bSell={bSell}, good={good}, a4={a4}");

        PlayerMarketInteractionEventArgs eventArgs = new(EventHookPhase.Pre, playerId, bSell == 1, good, bShiftModifier);
        PlayerR3EventHooks.OnPlayerMarketInteraction.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            c_game_player_market_buy_hook.Original!(
                eventArgs.PlayerId,
                eventArgs.Selling ? 1 : 0,
                eventArgs.Good,
                eventArgs.ShiftModifier
            );

            PlayerMarketInteractionEventArgs postEventArgs = new(EventHookPhase.Post, playerId, bSell == 1, good, bShiftModifier);
            PlayerR3EventHooks.OnPlayerMarketInteraction.Raise(postEventArgs);
        }
    }

    //
    // void __fastcall c_game_player_subtract_resources(__int64 pBuildingManager, int playerId, Goods good_type, int good_amount, int bDontSubtractResources)
    // 45 85 C0 0F 84 ?? ?? ?? ?? 44 89 44 24
    //
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void c_game_player_subtract_resources_delegate(NativePointer<GameBuildingManager> pBuildingManager, int playerId, eGoods good, int goodAmount, int bDontSubtractResources);
    internal static DetourHandle<c_game_player_subtract_resources_delegate> c_game_player_subtract_resources_hook = new();
    public static void c_game_player_subtract_resources_hook_impl(NativePointer<GameBuildingManager> pBuildingManager, int playerId, eGoods good, int amount, int bDontSubtractResources)
    {
        //LogHelper.Debug($"manager={pBuildingManager}, playerId={playerId}, good={good}, amount={amount}, bDontSubtractResources={bDontSubtractResources}");

        PlayerSubtractResourceEventArgs eventArgs = new(EventHookPhase.Pre, playerId, good, amount, bDontSubtractResources == 1);
        PlayerR3EventHooks.OnPlayerSubtractResource.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            c_game_player_subtract_resources_hook.Original!(
                pBuildingManager,
                eventArgs.PlayerId,
                eventArgs.Good,
                eventArgs.Amount,
                eventArgs.DontSubtract ? 1 : 0
            );

            PlayerSubtractResourceEventArgs postEventArgs = new(EventHookPhase.Post, playerId, good, amount, bDontSubtractResources == 1);
            PlayerR3EventHooks.OnPlayerSubtractResource.Raise(postEventArgs);
        }
    }

    // __int64 __fastcall c_game_player_add_resources(__int64 pBuildingManager, int playerId, Goods good_to_add, int amount)
    // 89 54 24 ?? 48 89 4C 24 ?? 53 55 56 41 54
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_player_add_resources_delegate(NativePointer<GameBuildingManager> pBuildingManager, int playerId, eGoods good, int amount);
    internal static DetourHandle<c_game_player_add_resources_delegate> c_game_player_add_resources_hook = new();
    public static Int64 c_game_player_add_resources_hook_impl(NativePointer<GameBuildingManager> pBuildingManager, int playerId, eGoods good, int amount)
    {
        //LogHelper.Debug($"manager={pBuildingManager}, playerId={playerId}, good={good}, amount={amount}");

        PlayerAddResourceEventArgs eventArgs = new(EventHookPhase.Pre, playerId, good, amount);
        PlayerR3EventHooks.OnPlayerAddResource.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_player_add_resources_hook.Original!(
                pBuildingManager,
                eventArgs.PlayerId,
                eventArgs.Good,
                eventArgs.Amount
            );
            eventArgs.ReturnValue = originalResult == 1;
            PlayerAddResourceEventArgs postEventArgs = new(EventHookPhase.Post, playerId, good, amount)
            {
                ReturnValue = originalResult == 1
            };
            PlayerR3EventHooks.OnPlayerAddResource.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue ? 1 : 0;
    }
}
