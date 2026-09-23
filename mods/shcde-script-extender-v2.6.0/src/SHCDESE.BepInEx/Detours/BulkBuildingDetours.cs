using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.Core.Memory.Scanners;
using RedBird.X64.Assembly;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using RedBird.X64.Memory.Scanners;
using Serilog;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.DebugMenu;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.GameGlobals;
using SHCDESE.ImMenu.MapEditor;
using SHCDESE.Interop;
using SHCDESE.Logging;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using static Iced.Intel.AssemblerRegisters;
namespace SHCDESE.Detours;

/// <summary>
/// Manages the application of all native detours (hooks) related to the game's building systems.
/// </summary>
/// <remarks>
/// This static class is responsible for finding specific functions within the game's memory using
/// Array-of-Bytes (AOB) scanning and redirecting their execution to custom C# implementations.
/// This allows the script extender to intercept core game logic such as building placement, deletion,
/// damage, and unit spawning from buildings, in order to expose these events to the modding API.
/// </remarks>
[SuppressUnmanagedCodeSecurity]
public unsafe class BulkBuildingDetours
{
    /// <summary>
    /// Scans the game's memory for all building-related function signatures and applies the corresponding detours.
    /// </summary>
    /// <param name="memory">A <see cref="ReadOnlySpan{T}"/> of bytes representing the memory block of the loaded `CrusaderDE.dll`.</param>
    /// <param name="region">The memory region to scan for function signatures.</param>
    /// <param name="tx">Shared transaction.</param>
    /// <param name="scanner">Shared scanner.</param>
    /// <remarks>
    /// This method should only be called once during the script extender's initialization phase, after the
    /// `CrusaderDE.dll` has been loaded and its memory is accessible. It initializes and enables all the hooks
    /// defined within this class.
    /// </remarks>
    public BulkBuildingDetours(ReadOnlySpan<byte> memory, ScanRegion region, HookTransaction tx, DataScanner scanner)
    {
        LogHelper.Information($"Applying");
        UInt64 currentImageBase = (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle;

        // These hooks need to be done within the CrusaderDE.dll module!
        tx.AddDetour(c_game_build_wall_hook,
            "44 89 4C 24 ?? 44 89 44 24 ?? 89 54 24 ?? 48 89 4C 24 ?? 53 56 57",
            c_game_build_wall_hook_impl);

        tx.AddDetour(c_game_buildingtile_take_melee_damage_hook,
            "48 89 5C 24 ?? 44 89 4C 24 ?? 44 89 44 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ?? 8B BC 24",
            c_game_buildingtile_take_damage_hook_impl);

        tx.AddDetour(c_game_building_spawn_hook,
            "48 89 5C 24 ? 48 89 6C 24 ? 48 89 74 24 ? 57 41 54 41 55 41 56 41 57 48 83 EC ? 48 63 9C 24",
            c_game_building_spawn_hook_impl);

        tx.AddDetour(c_game_is_player_building_within_range_to_same_type_hook,
            "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 44 89 44 24 ?? 57 41 54 41 55 41 56 41 57 48 83 EC ?? 4C 63 A4 24 ?? ?? ?? ?? 48 8B E9 48 8D 0D ?? ?? ?? ?? 33 DB 33 FF 45 8B E9 44 8B FA",
            c_game_is_player_building_within_range_to_same_type_hook_impl);

        tx.AddDetour(c_game_player_build_structure_hook,
             "89 54 24 ?? 53 55 56 57 41 55 41 56 41 57 48 83 EC ?? 44 8B AC 24",
             c_game_player_build_structure_hook_impl);

        tx.AddDetour(c_game_building_bulldoze_hook,
             "48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 83 3D ?? ?? ?? ?? ?? 48 8B F1",
             c_game_building_bulldoze_hook_impl);

        tx.AddDetour(c_game_building_delete_hook,
            "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC ?? 41 BE",
            c_game_building_delete_hook_impl);

        tx.AddDetour(c_game_add_good_to_goodsyard_hook,
            "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC ?? 44 8B 74 24",
            c_game_add_good_to_goodsyard_hook_impl);

        tx.AddDetour(c_game_building_set_production_good_hook,
            "45 8B D1 45 8B C8",
            c_game_building_set_production_good_hook_impl);

        tx.AddDetour(c_game_player_build_placement_validator_hook,
             "48 89 5C 24 ? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ? 44 8B BC 24",
             c_game_player_build_placement_validator_hook_impl);

        tx.AddDetour(c_game_bulldoze_wall_hook,
             "48 89 5C 24 ? 48 89 6C 24 ? 48 89 74 24 ? 57 41 56 41 57 48 83 EC ? 49 63 F8",
             c_game_bulldoze_wall_hook_impl);

        tx.AddDetour(c_game_building_repair_hook,
            "48 89 5C 24 ? 48 89 6C 24 ? 48 89 74 24 ? 48 89 7C 24 ? 41 56 48 83 EC ? 48 63 C2 4C 8D 35 ? ? ? ? 48 69 F0",
            c_game_building_repair_hook_impl);

        tx.AddDetour(c_game_building_refund_hook,
             "45 85 C9 0F 84 ? ? ? ? 53",
             c_game_building_refund_hook_impl);

        tx.AddDetour(c_game_allow_repair_for_building_proximity_hook,
            "48 89 5C 24 ? 48 89 6C 24 ? 48 89 74 24 ? 48 89 7C 24 ? 41 54 41 55 41 56 41 57 4D 63 F8",
            c_game_allow_repair_for_building_proximity_hook_impl);

        tx.AddDetour(c_game_player_build_pitch_ditch_hook,
            "48 89 5C 24 ? 48 89 6C 24 ? 48 89 74 24 ? 57 41 56 41 57 48 83 EC ? 49 63 D8",
            c_game_player_build_pitch_ditch_hook_impl);

        tx.AddDetour(c_game_player_remove_pitch_ditch_hook,
            "40 53 48 83 EC ? 4C 63 C2",
            c_game_player_remove_pitch_ditch_hook_impl);

        tx.AddDetour(c_game_get_buildable_range_for_skirmish,
            "8B 91 ?? ?? ?? ?? 81 FA",
            c_game_get_buildable_range_for_skirmish_hook_impl);

        DataScanner c_game_update_visual_goodsyard_goods_scan = scanner.Scan(CompiledPattern.Parse("48 63 C2 45 33 DB"));
        if (c_game_update_visual_goodsyard_goods_scan.Found)
        {
            c_game_update_visual_goodsyard_goods = Marshal.GetDelegateForFunctionPointer<c_game_update_visual_goodsyard_goods_delegate>((IntPtr)c_game_update_visual_goodsyard_goods_scan.CurrentAddress);
        } else LogHelper.Warning($"Failed to find c_game_update_visual_goodsyard_goods");

        tx.AddInline(c_game_building_keep_spawn_peasant_hook,
            "E8 ?? ?? ?? ?? 85 C0 74 ?? 48 98 48 69 C8 ?? ?? ?? ?? 42 8B 84 21",
            static (asm, overwritten, returnAddress) =>
            {
            Instruction[] instrs = overwritten.CloneInstructionsWithoutIP().Skip(1).ToArray();
            asm.call((UInt64)Marshal.GetFunctionPointerForDelegate(static (NativePointer<GameUnitManager> pUnitManager, int playerColorId, UInt16 playerOwnerId, UInt16 tileX, UInt16 tileY, Int16 heightElevation, eChimps chimp) =>
            {
                LogHelper.Verbose($"peasant spawned for player: {playerOwnerId}, tileX: {tileX}, tileY: {tileY} chimp={chimp}");

                KeepSpawnPeasantEventArgs eventArgs = new(EventHookPhase.Pre, playerColorId, playerOwnerId, tileX, tileY, heightElevation, chimp);
                BuildingR3EventHooks.OnKeepSpawnPeasant.Raise(eventArgs);
                if (!eventArgs.SkipOriginalFunction)
                {
                    Int64 originalResult = BulkUnitDetours.c_game_unit_spawn_ex_hook_impl(
                        pUnitManager,
                        eventArgs.PlayerColorId,
                        eventArgs.PlayerOwnerId,
                        eventArgs.TileX,
                        eventArgs.TileY,
                        eventArgs.HeightElevation,
                        eventArgs.Chimp
                    );
                    eventArgs.ReturnValue = originalResult;
                    KeepSpawnPeasantEventArgs postEventArgs = new(EventHookPhase.Post, playerColorId, playerOwnerId, tileX, tileY, heightElevation, chimp)
                    {
                        ReturnValue = originalResult
                    };
                    BuildingR3EventHooks.OnKeepSpawnPeasant.Raise(postEventArgs);
                    eventArgs.ReturnValue = postEventArgs.ReturnValue;
                }
                return eventArgs.ReturnValue;
            }));
            asm.AddInstructions(instrs);
        });

        tx.AddContextHook(c_game_building_granary_spawn_chicken_hook,
             "89 4C 24 ? 8B D3 C1 E0",
             static ctx =>
             {
                 // rcx = heightElevation
                 // rdx = TileX
                 // r8 = playerOwnerId
                 // r9 = worldTileY
                 // stack1 = IGNORE
                 // stack2 = IGNORE
                 // stack3 = unitType
                int heightElevation = (int)ctx.Pointer->RCX;
                int tileX = (int)ctx.Pointer->RDX;
                int playerId = (int)ctx.Pointer->R8;
                int tileY = (int)ctx.Pointer->R9 >> 3; // aka / 8
                eChimps* pUnitType = (eChimps*)ctx.Pointer->GetStackPtr<int>(0x30);
                Log.Verbose($"c_game_building_granary_spawn_chicken_hook: tileX={tileX}, tileY={tileY}, heightElevation={heightElevation}, playerId={playerId}, unitType={*pUnitType}");

                GranarySpawnChickenEventArgs eventArgs = new(EventHookPhase.Pre, tileX, tileY, *pUnitType, playerId, heightElevation);
                BuildingR3EventHooks.OnGranarySpawnChicken.Raise(eventArgs);

                ctx.Pointer->RCX = (UInt64)eventArgs.HeightElevation;
                ctx.Pointer->RDX = (UInt64)eventArgs.TileX;
                ctx.Pointer->R8 = (UInt64)eventArgs.PlayerId;
                ctx.Pointer->R9 = (UInt64)eventArgs.TileY << 3;
                *pUnitType = eventArgs.Chimp;
            }, new RedBird.X64.Hooks.Context.ContextHookOptions() {  Registers = X64SmartCPUContextRegs.Volatile });

        tx.AddInline(c_game_building_calculate_wall_cost_hook,
            "48 03 D0 48 89 7C 24",
            static (asm, overwritten, returnAddress) =>
            {
                // reference:
                // r8 = wall_tiles_n
                // r9 = low_wall_tiles_n
                asm.AddInstructions(overwritten[..4]);
                // add     rdx, rax
                // mov     [rsp+38h+arg_8], rdi
                // mov     edi, [rsp+38h+arg_20]
                // mov     eax, [rdx]

                asm.push(rdx);
                asm.push(rax);

                asm.X64FastcallSafe((UInt64)Marshal.GetFunctionPointerForDelegate<GameBuildingManagerAPI.GetWallCostMultiplierDelegate>(GameBuildingManagerAPI.GetHighWallCostMultiplierInternal), 0, preserveRAX: false);
                asm.imul(eax, r8d);
                asm.mov(edx, eax);

                asm.X64FastcallSafe((UInt64)Marshal.GetFunctionPointerForDelegate<GameBuildingManagerAPI.GetWallCostMultiplierDelegate>(GameBuildingManagerAPI.GetLowWallCostMultiplierInternal), 0, preserveRAX: false);
                asm.imul(eax, r9d);

                asm.add(edx, eax);
                asm.pop(rax);
                asm.add(eax, edx);

                asm.pop(rdx);
                asm.xor(r8, r8);
                asm.xor(r9, r9);

                // asm below
                // add eax, r9d
                // test edi, edi
            });

        // vanilla: 5 stone = 10 walls
        // vanilla: 5 stone = 20 low walls
        tx.AddInline(c_game_building_calculate_wall_cost_placement_hook,
               "E8 ? ? ? ? 44 8B 0D ? ? ? ? 48 8D 0D ? ? ? ? 44 8B 05 ? ? ? ? 8B 15 ? ? ? ? 89 05",
               static (asm, overwritten, returnAddress) =>
               {
                   // Call the original function to get the actual stone count
                   asm.AddInstruction(overwritten[0]);

                   asm.push(rdx);
                   asm.push(rcx);
                   asm.pushfq();
                   asm.push(rax); // Stack Top = Original Stone Count

                   // Get the current Mapper Value (Wall Type)
                   asm.mov(rdx, GameGlobalsManager.Instance.CurrentContextMapperValueVA);
                   // Read the WORD value (mv)
                   asm.movzx(ecx, __word_ptr[rdx]);

                   // Branch based on Wall Type
                   Label lowWallLabel = asm.CreateLabel();
                   Label calcLabel = asm.CreateLabel();

                   // Check if Low Wall
                   asm.cmp(ecx, (int)eMappers.MAPPER_WOODWALL);
                   asm.je(lowWallLabel);

                   // --- High Wall Path ---
                   asm.X64FastcallSafe((UInt64)Marshal.GetFunctionPointerForDelegate<GameBuildingManagerAPI.GetWallCostMultiplierDelegate>(GameBuildingManagerAPI.GetHighWallCostMultiplierInternal), 0, preserveRAX: false);
                   asm.mov(r8d, 2);
                   asm.jmp(calcLabel);

                   // --- Low Wall Path ---
                   asm.Label(ref lowWallLabel);
                   asm.X64FastcallSafe((UInt64)Marshal.GetFunctionPointerForDelegate<GameBuildingManagerAPI.GetWallCostMultiplierDelegate>(GameBuildingManagerAPI.GetLowWallCostMultiplierInternal), 0, preserveRAX: false);
                   asm.mov(r8d, 4);

                   // -------------------------------------------------------------
                   // CALCULATION
                   // -------------------------------------------------------------
                   asm.Label(ref calcLabel);
                   // State: EAX = Unit Cost. Stack Top = Original Stone Count.

                   asm.mov(ecx, eax); // Move Cost to ECX (Divisor)
                   asm.pop(rax);      // Pop Stone Count to EAX (Dividend). Stack: [Flags, RCX, RDX]

                   // Safety: Prevent divide by zero
                   asm.test(ecx, ecx);
                   Label skipMathLabel = asm.CreateLabel();
                   asm.jz(skipMathLabel);

                   // Step A: Calculate True Tile Limit
                   // Formula: (Stone * 4) / CustomCost
                   asm.shl(eax, 2);   // EAX = Total Units
                   asm.xor(edx, edx);
                   asm.div(ecx);      // EAX = Tile Limit

                   // Step B: Normalize for Game Logic
                   asm.xor(edx, edx);
                   asm.div(r8d);      // EAX = Adjusted Resource Count

                   asm.Label(ref skipMathLabel);

                   asm.popfq();
                   asm.pop(rcx); // Restore Original RCX (The game uses it next)
                   asm.pop(rdx); // Restore Original RDX (The game uses it next)

                   // CONTINUE EXECUTION
                   // EAX contains our modified limit.
                   // We execute the rest of the overwritten instructions.
                   asm.AddInstructions(overwritten[1..]);
               });

        tx.AddInline(c_game_building_take_fire_damage_hook,
            "89 54 24 ? 41 8B 92",
            static (asm, overwritten, returnAddress) =>
            {
                // mov     [rsp+78h+damage], edx ; damage
                asm.push(rcx);
                asm.mov(rcx, __word_ptr[r10 + 0x12E]);

                // eax seems to be free to use at this point
                asm.X64FastcallSafe((UInt64)Marshal.GetFunctionPointerForDelegate<GameBuildingManagerAPI.GetBuildingFireDamageDelegate>(GameBuildingManagerAPI.GetBuildingFireDamageInternal), totalArgumentCount: 1, preserveRAX: false);
                asm.pop(rcx);
                asm.mov(rdx, rax);

                asm.AddInstructions(overwritten[0..3]); 
                // note: word ptr [r10+12Eh] = building type
                // mov edx, [r10 + 150h]; building_offset
                // edx = must be damage
                // call    c_game_buildingtile_take_melee_damage
            });

        tx.AddContextHook(c_game_building_toggle_pause_hook,
            "0F B6 8C 2A ?? ?? ?? ?? 41 88 8A",
            static ctx =>
            {
                GameBuilding* buildingPtr = (GameBuilding*)(ctx.Pointer->R10 - 6);
                int buildingId = GameBuildingManagerAPI.Instance.GetBuildingsArray().GetIndexByAddress(buildingPtr) + 1;
                LogHelper.Verbose($"buildingId: {buildingId}");

                BuildingTogglePauseEventArgs eventArgs = new(EventHookPhase.Pre, buildingId, buildingPtr->r_IsSleeping == 1);
                BuildingR3EventHooks.OnTogglePause.Raise(eventArgs);
            }, new RedBird.X64.Hooks.Context.ContextHookOptions() { Registers = RedBird.X64.Assembly.X64SmartCPUContextRegs.Volatile });

        // 44 8B F2 41 C1 FE ? 41 8B C6
        // edi = wood
        // ebp = stone blocks
        // r15d = iron ingots
        // r12d = pitch raw
        // r14d = gold
        //.text:00000001800C119A                 mov     r14d, edx
        //.text:00000001800C119D                 sar     r14d, 5
        //.text:00000001800C11A1                 mov     eax, r14d
        //.text:00000001800C11A4                 shr     eax, 1Fh
        //.text:00000001800C11A7                 add     r14d, eax
        //(hook jmp here)
        //.text:00000001800C11AA                 mov     [rsp+58h+var_38], r14d
        tx.AddContextHook(c_game_building_refund_intercept_hook,
            "44 8B F2 41 C1 FE ? 41 8B C6",
            static ctx =>
            {
                int buildingId = *ctx.Pointer->GetStackPtr<int>(IntPtr.Size * 29);
                if (buildingId == 0)
                {
                    buildingId = RefundContextBuildingId;
                }
                GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
                float percentageFactor = RefundContextPercentage / 100f;

                eStructs buildingType = buildingApi.GetType(buildingId);
                int woodCost = buildingApi.GetWoodCost(buildingType);
                int stoneCost = buildingApi.GetStoneCost(buildingType);
                int ironIngotCost = buildingApi.GetIronIngotCost(buildingType);
                int rawPitchCost = buildingApi.GetRawPitchCost(buildingType);
                int goldCost = buildingApi.GetGoldCost(buildingType);

                LogHelper.Debug($"Refunding buildingId={buildingId}, type={buildingType}, costs={woodCost}, {stoneCost}, {ironIngotCost}, {rawPitchCost}, {goldCost}");
                ctx.Pointer->RDI = (UInt64)(woodCost * buildingApi.WoodRefundMultiplier * percentageFactor);
                ctx.Pointer->RBP = (UInt64)(stoneCost * buildingApi.StoneRefundMultiplier * percentageFactor);
                ctx.Pointer->R15 = (UInt64)(ironIngotCost * buildingApi.IronRefundMultiplier * percentageFactor);
                ctx.Pointer->R12 = (UInt64)(rawPitchCost * buildingApi.PitchRefundMultiplier * percentageFactor);
                ctx.Pointer->R14 = (UInt64)(goldCost * buildingApi.GoldRefundMultiplier * percentageFactor);
            }, new RedBird.X64.Hooks.Context.ContextHookOptions() { 
                Registers = X64SmartCPUContextRegs.Volatile | 
                X64SmartCPUContextRegs.RDI | 
                X64SmartCPUContextRegs.RBP | 
                X64SmartCPUContextRegs.R12 | 
                X64SmartCPUContextRegs.R14 |
                X64SmartCPUContextRegs.R15,
                Placement = OverwrittenInstructionPlacement.Suppress
            });

        tx.AddInline(c_game_building_set_repair_costs_stone_hook,
             "45 89 8A ? ? ? ? 8B C5",
             static (asm, overwritten, returnAddress) =>
             {
                    asm.push(rcx);
                    asm.push(rdx);
                    asm.push(rax);
                    asm.mov(rcx, r11);
                    asm.mov(rdx, r9);
                    asm.X64FastcallSafe((UInt64)Marshal.GetFunctionPointerForDelegate(static (UInt64 buildingAddr, int stoneCostForRepair) =>
                    {
                        // 300 = offset to re-align the addr to be representative of a normal building element.
                        int buildingId = GameBuildingManagerAPI.Instance.GetBuildingsArray().GetIndexByAddress(buildingAddr + 300) + 1;
                        Log.Debug($"c_game_building_set_repair_costs_stone_hook: buildingId={buildingId}, cost={stoneCostForRepair}");

                        BuildingCalculateStoneRepairCostEventArgs eventArgs = new(EventHookPhase.Pre, buildingId, stoneCostForRepair);
                        BuildingR3EventHooks.OnBuildingCalculateStoneRepairCost.Raise(eventArgs);

                        return eventArgs.RepairCost;
                    }), totalArgumentCount: 2, preserveRAX: false);
                    asm.mov(r9, rax);
                    asm.pop(rax);
                    asm.pop(rdx);
                    asm.pop(rcx);

                    asm.AddInstructions(overwritten);
                    //.text:00000001800B39A1                 mov     [r10+31B828h], r9d
                    //.text:00000001800B39A8                 mov     eax, ebp
                    //.text:00000001800B39AA                 mov     rbp, [rsp+8+arg_8]
                });

        tx.AddInline(c_game_building_set_repair_costs_wood_hook,
            "41 89 82 ? ? ? ? 48 8B 34 24",
            static (asm, overwritten, returnAddress) =>
            {
                asm.push(rcx);
                asm.mov(rcx, r11);
                asm.mov(rdx, rax);
                asm.X64FastcallSafe((UInt64)Marshal.GetFunctionPointerForDelegate(static (UInt64 buildingAddr, int woodCostForRepair) =>
                {
                    int buildingId = GameBuildingManagerAPI.Instance.GetBuildingsArray().GetIndexByAddress(buildingAddr + 300) + 1;
                    Log.Debug($"c_game_building_set_repair_costs_wood_hook: buildingId={buildingId}, cost={woodCostForRepair}");

                    BuildingCalculateWoodRepairCostEventArgs eventArgs = new(EventHookPhase.Pre, buildingId, woodCostForRepair);
                    BuildingR3EventHooks.OnBuildingCalculateWoodRepairCost.Raise(eventArgs);

                    return eventArgs.RepairCost;
                }), totalArgumentCount: 2, preserveRAX: false);
                asm.pop(rcx);

                asm.AddInstructions(overwritten);

                //.text:00000001800B396D                 mov     [r10+31B824h], eax
                //.text:00000001800B3974                 mov     rsi, [rsp+8+var_8]
                //.text:00000001800B3978                 test    ebx, ebx
                //.text:00000001800B397A                 jz      short loc_1800B39A1
            });


        // Hook for the query-result of "should we close the gatehouse"? 
        // REPLACES:
        // cmp     word ptr [rdx+rbp+67E5AA4h], 2 
        // jnz     loc_1800B5EE0
        // cmp     word ptr [rdx+rbp+67E5AA6h], 2Dh ; '-'
        // jz      short loc_1800B5EE0
        // cmp     word ptr [rdx+rbp+67E5CBCh], 0
        // REPLACE END
        // jz      short loc_1800B5EE0
        tx.AddInline(c_game_building_gatehouse_query_hook,
            "66 83 BC 2A ? ? ? ? ? 0F 85",
            static (asm, overwritten, returnAddress) =>
            {
                asm.push(rcx);
                asm.push(rax);
                asm.mov(rcx, rdx);
                asm.X64FastcallSafe((UInt64)Marshal.GetFunctionPointerForDelegate(static (UInt64 unitOffset) =>
                {
                    bool result = false;
                    GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;

                    int unitId = unitApi.GetUnitArray().GetIndexByOffset(unitOffset);
                    int buildingId = GameBuildingManagerAPI.Instance.GetCurrentContextBuildingId();
                    //LogHelper.Verbose($"unitId={unitId}, buildingId={buildingId}");

                    // base game logic emulation
                    if (unitApi.TryGetUnitById(unitId, out GameUnit* unit))
                    {
                        if (
                        (unit->r_AliveState == Interop.Enums.AliveState.IsAlive) && 
                        (unit->r_UnitChimp != eChimps.CHIMP_TYPE_LION) && 
                        (unit->r_ControllableForPlayerId != 0))
                        {
                            result = true;
                        }
                    }

                    GatehouseQueryEventArgs eventArgs = new(EventHookPhase.Pre, unitId, buildingId);
                    BuildingR3EventHooks.OnGatehouseQuery.Raise(eventArgs);
                    if (eventArgs.ShouldClose.HasValue)
                    {
                        result = eventArgs.ShouldClose.Value;
                    }

                    return result;
                }), totalArgumentCount: 1, preserveRAX: false);
                asm.test(rax, rax);
                asm.pop(rax);
                asm.pop(rcx);
            }, hookSize: 34);

    }

    //
    // __int64 __fastcall c_game_get_buildable_range_for_skirmish(__int64 pTileManager)
    // 8B 91 ? ? ? ? 81 FA
    //
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_get_buildable_range_for_skirmish_delegate(IntPtr pTileManager);
    public static DetourHandle<c_game_get_buildable_range_for_skirmish_delegate> c_game_get_buildable_range_for_skirmish = new();
    public static Int64 c_game_get_buildable_range_for_skirmish_hook_impl(IntPtr pTileManager)
    {
        return GameBuildingManagerAPI.Instance.GetKeepProximityRangeInternal(GameTileManagerAPI.Instance.TileManager.CurrentMapSize);
    }

    //
    // __int64 __fastcall c_game_update_visual_goodsyard_goods(__int64 pBuildingManager, int goodsyard_tile)
    // 48 63 C2 45 33 DB
    // Call-only
    //
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_update_visual_goodsyard_goods_delegate(NativePointer<GameBuildingManager> pBuildingManager, int buildingId);
    public static c_game_update_visual_goodsyard_goods_delegate? c_game_update_visual_goodsyard_goods;

    //
    // __int64 __fastcall c_game_allow_repair_for_building_proximity(__int64 pUnknown, int playerId, unsigned int tileX, unsigned int tileY, int proximity, char a6)
    //
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_allow_repair_for_building_proximity_delegate(IntPtr pUnknown, int playerId, int tileX, int tileY, int proximity, byte a6);
    internal static DetourHandle<c_game_allow_repair_for_building_proximity_delegate> c_game_allow_repair_for_building_proximity_hook = new();
    public static Int64 c_game_allow_repair_for_building_proximity_hook_impl(IntPtr pUnknown, int playerId, int tileX, int tileY, int proximity, byte a6)
    {
        LogHelper.Verbose($"pUnknown={pUnknown.ToString("X16")}, playerId={playerId}, tileX={tileX}, tileY={tileY}, proximity={proximity}, a6={a6}");
        BuildingAllowRepairInProximityEventArgs eventArgs = new(EventHookPhase.Pre, playerId, tileX, tileY, proximity, a6);
        BuildingR3EventHooks.OnBuildingAllowRepairInProximity.Raise(eventArgs);

        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_allow_repair_for_building_proximity_hook.Original!(
                pUnknown,
                eventArgs.PlayerId,
                eventArgs.TileX,
                eventArgs.TileY,
                eventArgs.Proximity,
                eventArgs.Unknown
            );
            eventArgs.ReturnValue = originalResult;
            BuildingAllowRepairInProximityEventArgs postEventArgs = new(EventHookPhase.Post, playerId, tileX, tileY, proximity, a6)
            {
                ReturnValue = originalResult
            };
            BuildingR3EventHooks.OnBuildingAllowRepairInProximity.Raise(postEventArgs);
        }
        return eventArgs.ReturnValue;
    }

    //
    // void __fastcall c_game_refund_building(__int64 pBuildingManager, int a2, signed int playerResourceId, int percentage)
    //
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void c_game_building_refund_delegate(NativePointer<GameBuildingManager> pBuildingManager, int buildingId, int playerId, int percentage);
    internal static DetourHandle<c_game_building_refund_delegate> c_game_building_refund_hook = new();

    /// <summary>
    /// There is an edge case where the buildingId retrieval of c_game_building_refund_intercept_hook
    /// fails, hence this workaround.
    /// </summary>
    internal static int RefundContextBuildingId = 0;
    internal static int RefundContextPercentage = 0;
    public static void c_game_building_refund_hook_impl(NativePointer<GameBuildingManager> pBuildingManager, int buildingId, int playerId, int percentage)
    {
        LogHelper.Verbose($"pBuildingManager={pBuildingManager}, buildingId={buildingId}, playerId={playerId}, percentage={percentage}");
        BuildingRefundEventArgs eventArgs = new(EventHookPhase.Pre, buildingId, playerId, percentage);
        BuildingR3EventHooks.OnBuildingRefund.Raise(eventArgs);

        RefundContextBuildingId = eventArgs.BuildingId;
        RefundContextPercentage = eventArgs.Percentage;
        if (!eventArgs.SkipOriginalFunction)
        {
            c_game_building_refund_hook.Original!(
                pBuildingManager,
                eventArgs.BuildingId,
                eventArgs.PlayerId,
                eventArgs.Percentage
            );
        }

        BuildingRefundEventArgs postEventArgs = new(EventHookPhase.Post, buildingId, playerId, percentage);
        BuildingR3EventHooks.OnBuildingRefund.Raise(postEventArgs);
    }

    //
    // __int16 __fastcall c_game_player_build_placement_validator(__int64 pTileManager, int playerId, int tile_x, int tile_y, eMappers mv, int a6, unsigned __int8 a7)
    //
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_player_build_placement_validator_delegate(IntPtr pTileManager, int playerId, int tileX, int tileY, eMappers mv, int a6, byte a7);
    internal static DetourHandle<c_game_player_build_placement_validator_delegate> c_game_player_build_placement_validator_hook = new();
    public static Int64 c_game_player_build_placement_validator_hook_impl(IntPtr pTileManager, int playerId, int tileX, int tileY, eMappers mv, int a6, byte a7)
    {
        LogHelper.Verbose($"pTileManager={pTileManager.ToString("X16")}, playerId={playerId}, tileX={tileX}, tileY={tileY}, mv={mv}, a6={a6}, a7={a7}");

        BuildingPlacementValidationEventArgs eventArgs = new(EventHookPhase.Pre, playerId, tileX, tileY, mv, a6, a7);
        BuildingR3EventHooks.OnPlacementValidation.Raise(eventArgs);
        Int64 originalResult = c_game_player_build_placement_validator_hook.Original(
            pTileManager,
            eventArgs.PlayerId,
            eventArgs.TileX,
            eventArgs.TileY,
            eventArgs.Mappers,
            eventArgs.Unknown1,
            eventArgs.Unknown2
        );

        if (eventArgs.CustomValidationRules)
        {
            GameTileManagerAPI.Instance.TileManager.IsPlacementBlocked = eventArgs.ForceBlockPlacementState;
        }

        if (GameTileManagerAPI.Instance.TileManager.UsePlacementBlockedOverride)
        {
            GameTileManagerAPI.Instance.TileManager.IsPlacementBlocked = GameTileManagerAPI.Instance.TileManager.PlacementBlockedOverrideValue;
        }

        BuildingPlacementValidationEventArgs postEventArgs = new(EventHookPhase.Post, playerId, tileX, tileY, mv, a6, a7);
        BuildingR3EventHooks.OnPlacementValidation.Raise(postEventArgs);
        return originalResult;
    }

    //
    // __int16 *__fastcall c_game_building_set_production_good(int pUnknown, int buildingId, Goods good, int buildingGlobalId)
    //
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_building_set_production_good_delegate(IntPtr pUnknown, int buildingId, eGoods good, int buildingGlobalId);
    internal static DetourHandle<c_game_building_set_production_good_delegate> c_game_building_set_production_good_hook = new();
    public static Int64 c_game_building_set_production_good_hook_impl(IntPtr pUnknown, int buildingId, eGoods good, int buildingGlobalId)
    {
        LogHelper.Information($"pUnknown={pUnknown.ToString("X16")}, buildingId={buildingId}, good={good}, buildingGlobalId={buildingGlobalId}");

        BuildingSwitchProductionGoodEventArgs eventArgs = new(EventHookPhase.Pre, buildingId, good, buildingGlobalId);
        BuildingR3EventHooks.OnSwitchProductionGood.Raise(eventArgs);
        Int64 originalResult = c_game_building_set_production_good_hook.Original!(
            pUnknown,
            eventArgs.BuildingId,
            eventArgs.Good,
            eventArgs.BuildingGlobalId
        );

        BuildingSwitchProductionGoodEventArgs postEventArgs = new(EventHookPhase.Post, buildingId, good, buildingGlobalId);
        BuildingR3EventHooks.OnSwitchProductionGood.Raise(postEventArgs);
        return originalResult;
    }

    //
    // __int64 __fastcall c_game_add_good_to_goodsyard(__int64 pBuildingManager, int goodsyardId, int goodsyard_globalId, Goods eGoods, int add_amount, int max_tile_good_capacity, int bAdd)
    // 48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC ?? 44 8B 74 24
    //
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_add_good_to_goodsyard_delegate(NativePointer<GameBuildingManager> pBuildingManager, int buildingId, int goodsyardGlobalId, eGoods good, int addAmount, int capacity, int bAdd);
    internal static DetourHandle<c_game_add_good_to_goodsyard_delegate> c_game_add_good_to_goodsyard_hook = new();
    public static Int64 c_game_add_good_to_goodsyard_hook_impl(NativePointer<GameBuildingManager> pBuildingManager, int buildingId, int goodsyardGlobalId, eGoods good, int addAmount, int capacity, int bAdd)
    {
        LogHelper.Debug($"manager={((UInt64)pBuildingManager.Pointer).ToString("X16")}, buildingId={buildingId}, goodsyardGlobalid={goodsyardGlobalId}, good={good}, addAmount={addAmount}, capacity={capacity}, bAdd={bAdd}");
        
        AddGoodToGoodsyardEventArgs eventArgs = new(EventHookPhase.Pre, buildingId, goodsyardGlobalId, good, addAmount, capacity, bAdd == 1);
        BuildingR3EventHooks.OnGoodsyardAddGood.Raise(eventArgs);
        Int64 originalResult = c_game_add_good_to_goodsyard_hook.Original!(
            pBuildingManager,
            eventArgs.BuildingId,
            eventArgs.BuildingGlobalId,
            eventArgs.Good,
            eventArgs.AddAmount,
            eventArgs.Capacity,
            eventArgs.Add ? 1 : 0
        );

        AddGoodToGoodsyardEventArgs postEventArgs = new(EventHookPhase.Post, buildingId, goodsyardGlobalId, good, addAmount, capacity, bAdd == 1);
        BuildingR3EventHooks.OnGoodsyardAddGood.Raise(postEventArgs);
        return originalResult;
    }

    //
    // c_game_is_player_building_within_range_to_same_type_hook
    //

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate UInt64 c_game_is_player_building_within_range_to_same_type_delegate(NativePointer<GameBuildingManager> pBuildingManager, int playerId, int tileX, int tileY, int potRange, eStructs building);
    internal static DetourHandle<c_game_is_player_building_within_range_to_same_type_delegate> c_game_is_player_building_within_range_to_same_type_hook = new();
    public static UInt64 c_game_is_player_building_within_range_to_same_type_hook_impl(NativePointer<GameBuildingManager> pBuildingManager, int playerId, int tileX, int tileY, int potRange, eStructs building)
    {
        //Log.Information($"c_game_is_player_building_within_range_to_same_type_hook_impl: playerId={playerId}");
        // Create the PRE event args
        IsGameBuildingAdjacentToOtherEventArgs eventArgs = new(EventHookPhase.Pre, playerId, tileX, tileY, potRange, building);

        // Push the PRE event into the stream
        BuildingR3EventHooks.OnIsBuildingAdjacentToOther.Raise(eventArgs);

        // Check for skip (same logic)
        if (!eventArgs.SkipOriginalFunction)
        {
            //Log.Information($"c_game_is_player_building_within_range_to_same_type_hook_impl: eventArgs.playerId={eventArgs.PlayerId}");
            // 4a. Call original function with potentially modified args
            UInt64 originalResult = c_game_is_player_building_within_range_to_same_type_hook.Original!(
                pBuildingManager,
                eventArgs.PlayerId,
                eventArgs.TileX,
                eventArgs.TileY,
                eventArgs.PotentialRange,
                eventArgs.Building
            );
            eventArgs.ReturnValue = originalResult;

            // Create the POST event args (we can reuse and modify, but creating new is cleaner)
            IsGameBuildingAdjacentToOtherEventArgs postEventArgs = new(EventHookPhase.Post, playerId, tileX, tileY, potRange, building)
            {
                ReturnValue = originalResult
            };

            // Push the POST event into the stream
            BuildingR3EventHooks.OnIsBuildingAdjacentToOther.Raise(postEventArgs);

            // The final return value might have been changed by a POST subscriber
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }

        // Return the final result
        return eventArgs.ReturnValue;

    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void c_game_building_repair_hook_delegate(int playerId, int buildingId, int woodAmount, int stoneAmount, int buildingGlobalId);
    internal static DetourHandle<c_game_building_repair_hook_delegate> c_game_building_repair_hook = new();
    public static void c_game_building_repair_hook_impl(int playerId, int buildingId, int woodAmount, int stoneAmount, int buildingGlobalId)
    {
        Log.Debug("c_game_building_repair_hook_impl");
        BuildingRepairEventArgs eventArgs = new(EventHookPhase.Pre, playerId, buildingId, woodAmount, stoneAmount, buildingGlobalId);
        BuildingR3EventHooks.OnBuildingRepair.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            c_game_building_repair_hook.Original(
                eventArgs.PlayerId, 
                eventArgs.BuildingId,
                eventArgs.WoodCost,
                eventArgs.StoneCost,
                eventArgs.BuildingGlobalId
            );
            BuildingRepairEventArgs postEventArgs = new(EventHookPhase.Post, playerId, buildingId, woodAmount, stoneAmount, buildingGlobalId);
            BuildingR3EventHooks.OnBuildingRepair.Raise(postEventArgs);
        }
    }

    // __int64 __fastcall c_game_building_spawn(_DWORD *pBuildingManager, int playerId, unsigned int tile_x, int tile_y, __int16 height_elevation, int building_struct_type, int building_scale, int visualPlayerId, int sprite_variation_index)
    // 48 89 5C 24 ? 55 56 41 54 41 56 41 57 48 83 EC ? 48 63 9C 24
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_building_spawn_delegate(NativePointer<GameBuildingManager> pBuildingManager, int playerId, int tileX, int tileY, Int16 heightElevation, eStructs building, int buildingScale, int visualPlayerId, int spriteVariationIndex);
    internal static DetourHandle<c_game_building_spawn_delegate> c_game_building_spawn_hook = new();
    public static Int64 c_game_building_spawn_hook_impl(NativePointer<GameBuildingManager> pBuildingManager, int playerId, int tileX, int tileY, Int16 heightElevation, eStructs building, int buildingScale, int visualPlayerId, int spriteVariationIndex)
    {
        LogHelper.Debug($"manager={pBuildingManager}, playerId={playerId}, tileX={tileX}, tileY={tileY}, heightElevation={heightElevation}, building={building}, buildingScale={buildingScale}, visualPlayerId={visualPlayerId}, spriteVariationIndex={spriteVariationIndex}");

        BuildingSpawnEventArgs eventArgs = new(EventHookPhase.Pre, pBuildingManager, playerId, tileX, tileY, heightElevation, building, buildingScale, visualPlayerId, spriteVariationIndex);
        BuildingR3EventHooks.OnBuildingSpawn.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_building_spawn_hook.Original(
                eventArgs.BuildingManager,
                eventArgs.PlayerId,
                eventArgs.TileX,
                eventArgs.TileY,
                eventArgs.HeightElevation,
                eventArgs.Building,
                eventArgs.BuildingScale,
                eventArgs.VisualPlayerId,
                eventArgs.SpriteVariationIndex
            );
            eventArgs.ReturnValue = originalResult;
            BuildingSpawnEventArgs postEventArgs = new(EventHookPhase.Post, pBuildingManager, playerId, tileX, tileY, heightElevation, building, buildingScale, visualPlayerId, spriteVariationIndex)
            {
                ReturnValue = originalResult
            };
            BuildingR3EventHooks.OnBuildingSpawn.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }

    // __int64 __fastcall c_game_build_wall(_DWORD *pTileManager, unsigned int playerId, int tile_x_begin, int tile_y_begin, int tile_x_end, int tile_y_end, __int16 wallType, int buildWallsMaximum)
    // 44 89 4C 24 ? 44 89 44 24 ? 89 54 24 ? 48 89 4C 24 ? 53 56 57
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_build_wall_delegate(IntPtr pTileManager, int playerId, int tileXbegin, int tileYbegin, int tileXend, int tileYend, eMappers wallType, int buildWallsMaximum);
    internal static DetourHandle<c_game_build_wall_delegate> c_game_build_wall_hook = new();
    public static Int64 c_game_build_wall_hook_impl(IntPtr pTileManager, int playerId, int tileXbegin, int tileYbegin, int tileXend, int tileYend, eMappers wallType, int buildWallsMaximum)
    {
        LogHelper.Verbose($"pTileManager={pTileManager.ToString("X16")}, playerId={playerId}, tileXbegin={tileXbegin}, tileYbegin={tileYbegin}, tileXend={tileXend}, tileYend={tileYend}, wallType={wallType}, buildWallsMaximum={buildWallsMaximum}");

        if (DebugMenuManager.Instance._mirrorTool.WallMirrorEnabled)
        {
            MirrorTool.ExecuteMirrorActionForLine(tileXbegin, tileYbegin, tileXend, tileYend,
                (mirroredXBegin, mirroredYBegin, mirroredXEnd, mirroredYEnd) =>
                {
                    c_game_build_wall_hook.Original(
                        pTileManager,
                        playerId,
                        mirroredXBegin,
                        mirroredYBegin,
                        mirroredXEnd,
                        mirroredYEnd,
                        wallType,
                        buildWallsMaximum);
                });
        }

        BuildWallEventArgs eventArgs = new(EventHookPhase.Pre, playerId, tileXbegin, tileYbegin, tileXend, tileYend, wallType, buildWallsMaximum);
        BuildingR3EventHooks.OnBuildWall.Raise(eventArgs);

        Int64 originalResult = c_game_build_wall_hook.Original(
            pTileManager,
            eventArgs.PlayerId,
            eventArgs.TileXBegin,
            eventArgs.TileYBegin,
            eventArgs.TileXEnd,
            eventArgs.TileYEnd,
            eventArgs.WallType,
            eventArgs.BuildWallsMaximum
        );
        BuildWallEventArgs postEventArgs = new(EventHookPhase.Post, playerId, tileXbegin, tileYbegin, tileXend, tileYend, wallType, buildWallsMaximum);
        BuildingR3EventHooks.OnBuildWall.Raise(postEventArgs);

        return originalResult;
    }

    // void __fastcall c_game_bulldoze_wall(__int64 pTileManager, __int64 playerId, int tileId)
    // 48 89 5C 24 ? 48 89 6C 24 ? 48 89 74 24 ? 57 41 56 41 57 48 83 EC ? 49 63 F8
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_bulldoze_wall_delegate(IntPtr pTileManager, int playerId, int tileId);
    internal static DetourHandle<c_game_bulldoze_wall_delegate> c_game_bulldoze_wall_hook = new();
    public static Int64 c_game_bulldoze_wall_hook_impl(IntPtr pTileManager, int playerId, int tileId)
    {
        LogHelper.Debug($"pTileManager={pTileManager.ToString("X16")}, playerId={playerId}, tileId={tileId.ToString("X8")}");

        if (DebugMenuManager.Instance._mirrorTool.DeleteMirrorEnabled)
        {
            GameTileManagerAPI gtm = GameTileManagerAPI.Instance;
            UnmanagedVector2<UInt16> vec2 = gtm.GetTileVectorFromId(tileId);
            MirrorTool.ExecuteMirrorAction(tileId, vec2.Y, (mirroredId, mirroredX, mirroredY) =>
            {
                c_game_bulldoze_wall_hook.Original(pTileManager, playerId, mirroredId);
            });
        }

        WallBulldozeEventArgs eventArgs = new(EventHookPhase.Pre, playerId, tileId);
        BuildingR3EventHooks.OnWallBulldoze.Raise(eventArgs);

        Int64 originalResult = c_game_bulldoze_wall_hook.Original(
            pTileManager,
            eventArgs.PlayerId,
            eventArgs.TileId
        );
        WallBulldozeEventArgs postEventArgs = new(EventHookPhase.Post, playerId, tileId);
        BuildingR3EventHooks.OnWallBulldoze.Raise(postEventArgs);

        return originalResult;
    }

    // __int64 __fastcall c_game_buildingtile_take_meele_damage(_DWORD *pTileManager, int tileId, unsigned int tile_x, __int64 tile_y, int damage, int attacking_unit_0x3EA, int playerIdSource, int a8, int a9)
    // 48 89 5C 24 ?? 44 89 4C 24 ?? 44 89 44 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ?? 8B BC 24
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_buildingtile_take_damage_delegate(IntPtr pTileManager, int tileId, int tileX, int tileY, int damage, int a6, int playerIdSource, int a8, int a9);
    internal static DetourHandle<c_game_buildingtile_take_damage_delegate> c_game_buildingtile_take_melee_damage_hook = new();
    public static Int64 c_game_buildingtile_take_damage_hook_impl(IntPtr pTileManager, int tileId, int tileX, int tileY, int damage, int a6, int playerIdSource, int a8, int a9)
    {
        LogHelper.Debug($"pTileManager={pTileManager.ToString("X16")}, tileId={tileId}, tileX={tileX}, tileY={tileY}, damage={damage}, a6={a6}, playerIdSource={playerIdSource}, a8={a8}, a9={a9}");

        BuildingTileTakeDamageEventArgs eventArgs = new(EventHookPhase.Pre, tileId, tileX, tileY, damage, a6, playerIdSource, a8, a9);
        BuildingR3EventHooks.OnBuildingTileTakeDamage.Raise(eventArgs);

        Int64 originalResult = c_game_buildingtile_take_melee_damage_hook.Original(
            pTileManager,
            eventArgs.TileId,
            eventArgs.TileX,
            eventArgs.TileY,
            eventArgs.Damage,
            eventArgs.Unknown1,
            eventArgs.PlayerIdSource,
            eventArgs.Unknown3,
            eventArgs.Unknown4
        );
        BuildingTileTakeDamageEventArgs postEventArgs = new(EventHookPhase.Post, tileId, tileX, tileY, damage, a6, playerIdSource, a8, a9);
        BuildingR3EventHooks.OnBuildingTileTakeDamage.Raise(postEventArgs);

        return originalResult;
    }

    // __int16 __fastcall c_game_player_build_structure(unsigned int *pTileManager, int playerId, unsigned int tile_x, unsigned int tile_y, eMappers mv, int building_scale_unk, int a7, char bIsFree)
    // 89 54 24 ?? 53 55 56 57 41 55 41 56 41 57 48 83 EC ?? 44 8B AC 24
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_player_build_structure_delegate(IntPtr pTileManager, int playerId, int tileX, int tileY, eMappers mv, int buildingScaleUnk, int a7, byte bIsFree);
    internal static DetourHandle<c_game_player_build_structure_delegate> c_game_player_build_structure_hook = new();
    public static Int64 c_game_player_build_structure_hook_impl(IntPtr pTileManager, int playerId, int tileX, int tileY, eMappers mv, int buildingScaleUnk, int a7, byte bIsFree)
    {
        Log.Debug($"c_game_player_build_structure_hook_impl: pTileManager={pTileManager.ToString("X16")}, playerId={playerId}, tileX={tileX}, tileY={tileY}, mv={mv}, buildingScaleUnk={buildingScaleUnk}, a7={a7}, bIsFree={bIsFree}");

        if (DebugMenuManager.Instance._mirrorTool.BuildingMirrorEnabled)
        {
            GameTileManagerAPI gtm = GameTileManagerAPI.Instance;
            int centerTileId = gtm.GetTileId(tileX, tileY);
            MirrorTool.ExecuteMirrorAction(centerTileId, tileY, (mirroredId, mirroredX, mirroredY) =>
            {
                c_game_player_build_structure_hook.Original(
                    pTileManager,
                    playerId,
                    mirroredX,
                    mirroredY,
                    mv,
                    buildingScaleUnk,
                    a7,
                    bIsFree);
            });
        }
        bool isFree = bIsFree != 0;

        BuildStructureEventArgs eventArgs = new(EventHookPhase.Pre, playerId, tileX, tileY, mv, buildingScaleUnk, a7, isFree);
        BuildingR3EventHooks.OnBuildStructure.Raise(eventArgs);

        Int64 originalResult = c_game_player_build_structure_hook.Original(
            pTileManager,
            eventArgs.PlayerId,
            eventArgs.TileX,
            eventArgs.TileY,
            eventArgs.Mappers,
            eventArgs.BuildingScaleUnknown,
            eventArgs.Unknown1,
            eventArgs.IsFree ? (byte)1 : (byte)0
        );
        BuildStructureEventArgs postEventArgs = new(EventHookPhase.Post, playerId, tileX, tileY, mv, buildingScaleUnk, a7, isFree);
        BuildingR3EventHooks.OnBuildStructure.Raise(postEventArgs);

        return originalResult;
    }

    // __int64 __fastcall c_game_building_delete(__int64 pBuildingManager, int buildingId)
    // 48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 83 3D ?? ?? ?? ?? ?? 48 8B F1
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_building_delete_delegate(NativePointer<GameBuildingManager> pBuildingManager, int buildingId);
    internal static DetourHandle<c_game_building_delete_delegate> c_game_building_delete_hook = new();
    public static Int64 c_game_building_delete_hook_impl(NativePointer<GameBuildingManager> pBuildingManager, int buildingId)
    {
        LogHelper.Debug($"pBuildingManager={pBuildingManager}, buildingId={buildingId}");

        BuildingDeleteEventArgs eventArgs = new(EventHookPhase.Pre, buildingId);
        BuildingR3EventHooks.OnBuildingDelete.Raise(eventArgs);

        Int64 originalResult = c_game_building_delete_hook.Original(
            pBuildingManager,
            eventArgs.BuildingId
        );
        BuildingDeleteEventArgs postEventArgs = new(EventHookPhase.Post, buildingId);
        BuildingR3EventHooks.OnBuildingDelete.Raise(postEventArgs);

        return originalResult;
    }

    // __int64 __fastcall c_game_building_bulldoze(__int64 pBuildingManager, unsigned int buildingId)
    // 48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC ?? 41 BE
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_building_bulldoze_delegate(NativePointer<GameBuildingManager> pBuildingManager, int buildingId);
    internal static DetourHandle<c_game_building_bulldoze_delegate> c_game_building_bulldoze_hook = new();
    public static Int64 c_game_building_bulldoze_hook_impl(NativePointer<GameBuildingManager> pBuildingManager, int buildingId)
    {
        LogHelper.Debug($"pBuildingManager={pBuildingManager}, buildingId={buildingId}");

        BuildingBulldozeEventArgs eventArgs = new(EventHookPhase.Pre, buildingId);
        BuildingR3EventHooks.OnBuildingBulldoze.Raise(eventArgs);

        Int64 originalResult = c_game_building_bulldoze_hook.Original(
            pBuildingManager,
            eventArgs.BuildingId
        );
        BuildingBulldozeEventArgs postEventArgs = new(EventHookPhase.Post, buildingId);
        BuildingR3EventHooks.OnBuildingBulldoze.Raise(postEventArgs);

        return originalResult;
    }

    // __int64 __fastcall c_game_player_build_pitch_ditch(__int64 pTileManager, __int16 playerId, unsigned int tileX, unsigned int tileY)
    // 48 89 5C 24 ? 48 89 6C 24 ? 48 89 74 24 ? 57 41 56 41 57 48 83 EC ? 49 63 D8
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_player_build_pitch_ditch_delegate(IntPtr pTileManager, Int16 playerId, int tileX, int tileY);
    internal static DetourHandle<c_game_player_build_pitch_ditch_delegate> c_game_player_build_pitch_ditch_hook = new();
    public static Int64 c_game_player_build_pitch_ditch_hook_impl(IntPtr pTileManager, Int16 playerId, int tileX, int tileY)
    {
        LogHelper.Debug($"pTileManager={pTileManager.ToString("X16")}, playerId={playerId}, tileX={tileX}, tileY={tileY}");

        BuildPitchDitchEventArgs eventArgs = new(EventHookPhase.Pre, playerId, tileX, tileY);
        BuildingR3EventHooks.OnBuildPitchDitch.Raise(eventArgs);

        Int64 originalResult = c_game_player_build_pitch_ditch_hook.Original(
            pTileManager,
            (Int16)eventArgs.PlayerId,
            eventArgs.TileX,
            eventArgs.TileY
        );
        BuildPitchDitchEventArgs postEventArgs = new(EventHookPhase.Post, playerId, tileX, tileY)
        {
            ReturnValue = originalResult
        };
        BuildingR3EventHooks.OnBuildPitchDitch.Raise(postEventArgs);

        return originalResult;
    }

    // __int64 __fastcall c_game_player_remove_killing_pit(__int64 pTileManager, int pitchId)
    // 40 53 48 83 EC ? 4C 63 C2
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_player_remove_pitch_ditch_delegate(IntPtr pTileManager, int pitchId);
    internal static DetourHandle<c_game_player_remove_pitch_ditch_delegate> c_game_player_remove_pitch_ditch_hook = new();
    public static Int64 c_game_player_remove_pitch_ditch_hook_impl(IntPtr pTileManager, int pitchId)
    {
        LogHelper.Debug($"pTileManager={pTileManager.ToString("X16")}, pitchId={pitchId}");

        RemovePitchDitchEventArgs eventArgs = new(EventHookPhase.Pre, pitchId);
        BuildingR3EventHooks.OnRemovePitchDitch.Raise(eventArgs);

        Int64 originalResult = c_game_player_remove_pitch_ditch_hook.Original(
            pTileManager,
            eventArgs.TileId
        );
        RemovePitchDitchEventArgs postEventArgs = new(EventHookPhase.Post, pitchId);
        BuildingR3EventHooks.OnRemovePitchDitch.Raise(postEventArgs);

        return originalResult;
    }
    internal static HookHandle<X64InlineHook> c_game_building_gatehouse_query_hook = new();
    internal static HookHandle<X64InlineHook> c_game_building_keep_spawn_peasant_hook = new();
    internal static HookHandle<X64InlineHook> c_game_building_granary_spawn_chicken_hook = new();
    internal static HookHandle<X64InlineHook> c_game_building_calculate_wall_cost_hook = new();
    internal static HookHandle<X64InlineHook> c_game_building_calculate_wall_cost_placement_hook = new();
    internal static HookHandle<X64InlineHook> c_game_building_take_fire_damage_hook = new();
    internal static HookHandle<X64InlineHook> c_game_building_toggle_pause_hook = new();
    internal static HookHandle<X64InlineHook> c_game_building_set_repair_costs_stone_hook = new();
    internal static HookHandle<X64InlineHook> c_game_building_set_repair_costs_wood_hook = new();
    internal static HookHandle<X64InlineHook> c_game_building_refund_intercept_hook = new();

}
