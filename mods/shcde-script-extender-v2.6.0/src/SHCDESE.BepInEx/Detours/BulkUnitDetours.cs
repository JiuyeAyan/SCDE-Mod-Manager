using Iced.Intel;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Assembly.InstructionWalker;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using RedBird.X64.Hooks.Transaction;
using RedBird.X64.Memory.Scanners;
using Serilog;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using static Iced.Intel.AssemblerRegisters;

namespace SHCDESE.Detours;

[SuppressUnmanagedCodeSecurity]
internal unsafe class BulkUnitDetours
{
    public BulkUnitDetours(ReadOnlySpan<byte> memory, ScanRegion region, HookTransaction tx, DataScanner scanner)
    {
        LogHelper.Information($"Applying");
        UInt64 currentImageBase = (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle;

        ContextHookOptions volatileRegs = new();

        tx.AddDetour(c_game_unit_takedamage_melee_hook,
            "48 89 5C 24 18 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 30 49",
            c_game_unit_takedamage_melee_hook_impl);

        tx.AddDetour(c_game_unit_takedamage_projectile_hook,
            "48 89 5C 24 ?? 48 89 4C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ?? 45 33 D2",
            c_game_unit_takedamage_projectile_hook_impl);

        tx.AddDetour(c_game_unit_spawn_ex_hook,
            "40 53 55 56 48 83 EC ?? 44 8B 1D",
            c_game_unit_spawn_ex_hook_impl);

        tx.AddDetour(c_game_unit_handle_movement_hook,
            "48 63 C2 4C 69 C0 ?? ?? ?? ?? 41 83 BC 08",
            c_game_unit_handle_movement_hook_impl);

        tx.AddDetour(c_game_unit_delete_hook,
            "48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC ?? 48 63 FA 48 8D 2D",
            c_game_unit_delete_hook_impl);

        tx.AddDetour(c_game_unit_issueorder_movehere_hook,
            "48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ?? 48 63 F2",
            c_game_unit_issueorder_movehere_hook_impl);

        tx.AddDetour(c_game_unit_calculate_worker_good_yield_hook,
            "48 63 C1 4C 8D 1D ?? ?? ?? ?? 4C 69 C8",
            c_game_unit_calculate_worker_good_yield_hook_impl);

        // Unit selection logic hook
        tx.AddDetour(c_game_dll_troopselection_hook, HookTarget.FromExport("DLL_TroopSelection"),
            c_game_dll_troopselection_hook_impl);

        tx.AddContextHook(c_game_unit_killed_by_projectile_hook,
            "42 8B 84 0E ? ? ? ? 46 89 94 0E",
            static ctx =>
            {
                int* pProjectileId = ctx.Pointer->GetStackPtr<int>(0x50);
                int* pAttackedUnitId = ctx.Pointer->GetStackPtr<int>(0x30);
                Log.Verbose($"c_game_unit_killed_by_projectile_hook, attackedUnitId={*pAttackedUnitId}, projectileId={*pProjectileId}");

                UnitKilledByProjectileEventArgs eventArgs = new(EventHookPhase.Pre, *pAttackedUnitId, *pProjectileId);
                UnitR3EventHooks.OnUnitKilledByProjectile.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_killed_by_melee_hook,
            "66 41 89 84 34 ?? ?? ?? ?? 66 45 89 B4 34",
            static ctx =>
            {
                int* pAttackedUnitId = ctx.Pointer->GetStackPtr<int>(0x50);
                int* pAttackingUnitId = ctx.Pointer->GetStackPtr<int>(0x30);
                Log.Verbose($"c_game_unit_killed_by_melee_hook, attackingUnitId={*pAttackingUnitId}, damagedUnitId={*pAttackedUnitId}");

                UnitKilledByMeleeEventArgs eventArgs = new(EventHookPhase.Pre, *pAttackingUnitId, *pAttackedUnitId);
                UnitR3EventHooks.OnUnitKilledByMelee.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_damaged_by_projectile_hook,
            "42 0F B7 8C 0E ? ? ? ? 66 85 C9",
            static ctx =>
            {
                UInt16* pAttackingUnitId = ctx.Pointer->GetStackPtr<UInt16>(0x80);
                int damage = (int)ctx.Pointer->RBX;
                int* pProjectileId = ctx.Pointer->GetStackPtr<int>(0x50);
                int* pAttackedUnitId = ctx.Pointer->GetStackPtr<int>(0x30);
                Log.Verbose($"c_game_unit_damaged_by_projectile_hook: attackingUnitId={*pAttackingUnitId}, damage={damage}, attackedUnitId={*pAttackedUnitId}, projectileId={*pProjectileId}");

                UnitTakeDamageByProjectileExEventArgs eventArgs = new(EventHookPhase.Pre, *pAttackedUnitId, *pAttackingUnitId, *pProjectileId, damage);
                UnitR3EventHooks.OnUnitTakeProjectileDamageEx.Raise(eventArgs);

                ctx.Pointer->RBX = (UInt64)eventArgs.Damage;
            }, new ContextHookOptions() { Registers = X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RBX });

        tx.AddContextHook(c_game_player_buy_mercenary_hook,
            "4D 69 FE ? ? ? ? 41 39 9C 0F",
            static ctx =>
            {
                eChimps chimpType = (eChimps)ctx.Pointer->RDX;
                int goldCost = (int)ctx.Pointer->RBX;
                int newGoldCost = GameUnitManagerAPI.Instance.GetUnitGoldCost(chimpType);
                Log.Verbose($"c_game_player_buy_mercenary_hook: {chimpType} -> original goldCost: {goldCost}, new: {newGoldCost}");

                ctx.Pointer->RBX = (UInt64)newGoldCost;
            }, new ContextHookOptions() { Registers = X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RBX });

        tx.AddContextHook(c_game_unit_take_fire_damage,
            "41 8B C8 D1 E9",
            static ctx =>
            {
                eChimps chimpType = (eChimps)ctx.Pointer->R9;
                int fireDamage = GameUnitManagerAPI.GetFireDamageInternal(chimpType);
                Log.Verbose($"c_game_unit_take_fire_damage: {chimpType} -> fire damage: {fireDamage}");

                ctx.Pointer->R8 = (UInt64)fireDamage;
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_bedouin_healer_heal_hook,
            "83 C0 ? C6 01",
            static ctx =>
            {
                // rax = unit health
                UInt64 healedUnitOffset = ctx.Pointer->R9;
                int healedUnitId = ((int)healedUnitOffset / sizeof(GameUnit));
                int healerUnitId = *(int*)GameGlobalsManager.Instance.CurrentContextUnitValueVA;
                int healAmount = GameUnitManagerAPI.GetBedouinHealInternal(GameUnitManagerAPI.Instance.GetType(healedUnitId));

                Log.Verbose($"c_game_unit_bedouin_healer_heal_hook: healedUnitId={healedUnitId}, healerUnitId={healerUnitId}, heal={healAmount}");
                UnitHealByBedouinHealerEventArgs eventArgs = new(EventHookPhase.Pre, healedUnitId, healerUnitId, healAmount);
                UnitR3EventHooks.OnUnitHealByBedouinHealer.Raise(eventArgs);

                ctx.Pointer->RAX = ctx.Pointer->RAX + (UInt64)eventArgs.Heal;
            }, new ContextHookOptions() {
                Registers = X64SmartCPUContextRegs.Volatile,
                InstructionSelector = x => x.Skip(1)
            });

        //
        // Worker Event Hooks (asm-signal-style)
        //
        tx.AddContextHook(c_game_unit_woodcutter_update_pickup_planks_hook,
            "48 69 D9 ?? ?? ?? ?? 66 46 89 B4 23",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RCX;
                Log.Debug($"c_game_unit_woodcutter_update_pickup_planks_hook: unitId={unitId}");
                UnitWoodcutterPickUpPlanksEventArgs eventArgs = new(EventHookPhase.Pre, (int)unitId);
                UnitR3EventHooks.OnWoodcutterPickUpPlanks.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_woodcutter_update_dropoff_planks_hook,
            "46 0F BF 8C 21 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? FF 84 82 ?? ?? ?? ?? 8B D7",
            static ctx =>
            {
                int unitId = (int)(ctx.Pointer->RCX / (UInt64)sizeof(GameUnit));
                Log.Debug($"c_game_unit_woodcutter_update_dropoff_planks_hook: unitId={unitId}");
                UnitWoodcutterDropOffPlanksEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnWoodcutterDropOffPlanks.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_farmer_cattle_update_pickup_cheese_hook,
            "48 69 D9 ?? ?? ?? ?? 66 42 89 AC 2B",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RCX;
                Log.Debug($"c_game_unit_farmer_cattle_update_pickup_cheese_hook: unitId={unitId}");
                UnitCattleFarmerPickUpCheeseEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnCattleFarmerPickUpCheese.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_farmer_cattle_update_dropoff_cheese_hook,
            "46 0F BF 8C 29 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? FF 84 82 ?? ?? ?? ?? 41 8B D4",
            static ctx =>
            {
                int unitId = (int)(ctx.Pointer->RCX / (UInt64)sizeof(GameUnit));
                Log.Debug($"c_game_unit_farmer_cattle_update_dropoff_cheese_hook: unitId={unitId}");
                UnitCattleFarmerDropOffCheeseEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnCattleFarmerDropOffCheese.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_farmer_apple_update_pickup_apple_hook,
             "B8 ?? ?? ?? ?? 48 69 D9 ?? ?? ?? ?? 44 8B C5",
             static ctx =>
             {
                 int unitId = (int)ctx.Pointer->RCX;
                 Log.Debug($"c_game_unit_farmer_apple_update_pickup_apple_hook: unitId={unitId}");
                 UnitAppleFarmerPickUpAppleEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                 UnitR3EventHooks.OnAppleFarmerPickUpApple.Raise(eventArgs);
             }, volatileRegs);

        tx.AddContextHook(c_game_unit_farmer_apple_update_dropoff_apple_hook,
             "48 8D 15 ?? ?? ?? ?? 48 69 C8 ?? ?? ?? ?? 44 8B C5",
             static ctx =>
             {
                 int unitId = (int)ctx.Pointer->RAX;
                 Log.Debug($"c_game_unit_farmer_apple_update_dropoff_apple_hook: unitId={unitId}");
                 UnitAppleFarmerDropOffAppleEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                 UnitR3EventHooks.OnAppleFarmerDropOffApple.Raise(eventArgs);
             }, volatileRegs);

        tx.AddContextHook(c_game_unit_farmer_hemp_update_pickup_hemp_hook,
             "48 69 D1 ?? ?? ?? ?? 39 3D",
             static ctx =>
             {
                 int unitId = (int)ctx.Pointer->RCX;
                 Log.Debug($"c_game_unit_farmer_hemp_update_pickup_hemp_hook: unitId={unitId}");
                 UnitHempFarmerPickUpHempEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                 UnitR3EventHooks.OnHempFarmerPickUpHemp.Raise(eventArgs);
             }, volatileRegs);

        tx.AddContextHook(c_game_unit_farmer_hemp_update_dropoff_hemp_hook,
           "48 69 CA ?? ?? ?? ?? 66 42 FF 8C 21",
           static ctx =>
           {
               int unitId = (int)ctx.Pointer->RAX;
               Log.Debug($"c_game_unit_farmer_hemp_update_dropoff_hemp_hook: unitId={unitId}");
               UnitHempFarmerDropOffHempEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
               UnitR3EventHooks.OnHempFarmerDropOffHemp.Raise(eventArgs);
           }, volatileRegs);

        tx.AddContextHook(c_game_unit_baker_update_pickup_flour_hook,
           "E8 ?? ?? ?? ?? 45 8B C7 66 42 89 84 36",
           static ctx =>
           {
               int unitId = (int)ctx.Pointer->RCX;
               Log.Debug($"c_game_unit_baker_update_pickup_flour_hook: unitId={unitId}");
               UnitBakerPickUpFlourEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
               UnitR3EventHooks.OnBakerPickUpFlour.Raise(eventArgs);
           }, volatileRegs);

        tx.AddContextHook(c_game_unit_baker_update_pickup_bread_hook,
           "45 33 C0 48 69 DF",
           static ctx =>
           {
               int unitId = (int)ctx.Pointer->RDI;
               Log.Debug($"c_game_unit_baker_update_pickup_bread_hook: unitId={unitId}");
               UnitBakerPickUpBreadEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
               UnitR3EventHooks.OnBakerPickUpBread.Raise(eventArgs);
           }, new ContextHookOptions() { Registers = X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RDI });


        tx.AddContextHook(c_game_unit_baker_update_dropoff_bread_hook,
             "48 8D 15 ?? ?? ?? ?? 48 69 C8 ?? ?? ?? ?? 45 8B C7",
             static ctx =>
             {
                 int unitId = (int)ctx.Pointer->RAX;
                 Log.Debug($"c_game_unit_baker_update_dropoff_bread_hook: unitId={unitId}");
                 UnitBakerDropOffBreadEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                 UnitR3EventHooks.OnBakerDropOffBread.Raise(eventArgs);
             }, volatileRegs);

        tx.AddContextHook(c_game_unit_farmer_wheat_update_pickup_wheat_hook,
            "49 8B CD 49 69 F6",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RDX;
                Log.Debug($"c_game_unit_farmer_wheat_update_pickup_wheat_hook: unitId={unitId}");
                UnitWheatFarmerPickUpWheatEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnWheatFarmerPickUpWheat.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_farmer_wheat_update_dropoff_wheat_hook,
            "48 69 CA ?? ?? ?? ?? 66 42 FF 8C 29",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RDX;
                Log.Debug($"c_game_unit_farmer_wheat_update_dropoff_wheat_hook: unitId={unitId}");
                UnitWheatFarmerDropOffWheatEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnWheatFarmerDropOffWheat.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_miller_update_pickup_wheat_hook,
            "E8 ?? ?? ?? ?? 66 42 89 84 3B ?? ?? ?? ?? 8B D7 46 0F BF 84 3B",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RCX;
                Log.Debug($"c_game_unit_miller_update_pickup_wheat_hook: unitId={unitId}");
                UnitMillerPickUpWheatEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnMillerPickUpWheat.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_miller_update_dropoff_flour_hook,
            "45 8D 4E ?? 48 69 C8",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RAX;
                Log.Debug($"c_game_unit_miller_update_dropoff_flour_hook: unitId={unitId}");
                UnitMillerDropOffFlourEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnMillerDropOffFlour.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_miller_update_pickup_flour_hook,
            "48 69 C8 ?? ?? ?? ?? 42 8B 84 27 ?? ?? ?? ?? 66 42 89 9C 39",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RAX;
                Log.Debug($"c_game_unit_miller_update_pickup_flour_hook: unitId={unitId}");
                UnitMillerPickUpFlourEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnMillerPickUpFlour.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_brewer_update_pickup_hemp_hook,
             "4C 69 C0 ?? ?? ?? ?? B8 ?? ?? ?? ?? 44 89 64 24",
             static ctx =>
             {
                 int unitId = (int)ctx.Pointer->RAX;
                 Log.Debug($"c_game_unit_brewer_update_pickup_hemp_hook: unitId={unitId}");
                 UnitBrewerPickUpHempEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                 UnitR3EventHooks.OnBrewerPickUpHemp.Raise(eventArgs);
             }, volatileRegs);

        tx.AddContextHook(c_game_unit_brewer_update_dropoff_hemp_hook,
              "48 69 C8 ?? ?? ?? ?? 66 42 89 B4 39 ?? ?? ?? ?? 42 89 B4 39",
              static ctx =>
              {
                  int unitId = (int)ctx.Pointer->RAX;
                  Log.Debug($"c_game_unit_brewer_update_dropoff_hemp_hook: unitId={unitId}");
                  UnitBrewerDropOffHempEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                  UnitR3EventHooks.OnBrewerDropOffHemp.Raise(eventArgs);
              }, volatileRegs);

        tx.AddContextHook(c_game_unit_brewer_update_pickup_ale_hook,
           "33 F6 48 69 DF ?? ?? ?? ?? 45 8B C4",
           static ctx =>
           {
               int unitId = (int)ctx.Pointer->RDI;
               Log.Debug($"c_game_unit_brewer_update_pickup_ale_hook: unitId={unitId}");
               UnitBrewerPickUpAleEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
               UnitR3EventHooks.OnBrewerPickUpAle.Raise(eventArgs);
           }, new ContextHookOptions() { Registers = X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RDI });

        tx.AddContextHook(c_game_unit_brewer_update_dropoff_ale_hook,
           "48 69 CB ?? ?? ?? ?? 66 42 FF 8C 39",
           static ctx =>
           {
               int unitId = (int)ctx.Pointer->RBX;
               Log.Debug($"c_game_unit_brewer_update_dropoff_ale_hook: unitId={unitId}");
               UnitBrewerDropOffAleEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
               UnitR3EventHooks.OnBrewerDropOffAle.Raise(eventArgs);
           }, new ContextHookOptions() { Registers = X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RBX });

        tx.AddContextHook(c_game_unit_brewer_update_produced_ale_hook,
           "41 B9 ?? ?? ?? ?? 48 69 C8 ?? ?? ?? ?? 44 89 64 24",
           static ctx =>
           {
               int unitId = (int)ctx.Pointer->RAX;
               Log.Debug($"c_game_unit_brewer_update_produced_ale_hook: unitId={unitId}");
               UnitBrewerProducedAleEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
               UnitR3EventHooks.OnBrewerProduceAle.Raise(eventArgs);
           }, volatileRegs);

        tx.AddContextHook(c_game_unit_innkeeper_update_pickup_ale_hook,
           "4C 69 C0 ?? ?? ?? ?? B8 ?? ?? ?? ?? 89 7C 24",
           static ctx =>
           {
               int unitId = (int)ctx.Pointer->RAX;
               Log.Debug($"c_game_unit_innkeeper_update_pickup_ale_hook: unitId={unitId}");
               UnitInnkeeperPickUpAleEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
               UnitR3EventHooks.OnInnkeperPickUpAle.Raise(eventArgs);
           }, volatileRegs);

        tx.AddContextHook(c_game_unit_innkeeper_update_dropoff_ale_hook,
            "66 46 89 A4 3B ?? ?? ?? ?? 66 46 89 A4 3B ?? ?? ?? ?? 66 01 84 31",
            static ctx =>
            {
                int unitId = (int)(ctx.Pointer->RDX / (UInt64)sizeof(GameUnit));
                Log.Debug($"c_game_unit_innkeeper_update_dropoff_ale_hook: unitId={unitId}");
                UnitInnkeeperDropOffAleEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnInnkeeperDropOffAle.Raise(eventArgs);
            }, volatileRegs);

        // 44 8B C6 48 69 CA: fletcher->pickup: wood, has taken it, rdx
        // 49 69 D2 ?? ?? ?? ?? 66 42 89 BC 22: fletcher->dropoff: wood, has stored it, r10
        // 48 8D 15 ?? ?? ?? ?? 48 69 C8 ?? ?? ?? ?? 44 8B C6 4A 0F BF 84 21: fletcher->dropoff: bow/xbow. has stored it, rax

        tx.AddContextHook(c_game_unit_fletcher_update_pickup_wood_hook,
            "44 8B C6 48 69 CA",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RDX;
                Log.Debug($"c_game_unit_fletcher_update_pickup_wood_hook: unitId={unitId}");
                UnitFletcherPickUpPlanksEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnFletcherPickUpPlanks.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_fletcher_update_dropoff_wood_hook,
            "49 69 D2 ?? ?? ?? ?? 66 42 89 BC 22",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->R10;
                Log.Debug($"c_game_unit_fletcher_update_dropoff_wood_hook: unitId={unitId}");
                UnitFletcherDropOffPlanksEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnFletcherDropOffPlanks.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_fletcher_update_dropoff_produce_hook,
            "48 8D 15 ?? ?? ?? ?? 48 69 C8 ?? ?? ?? ?? 44 8B C6 4A 0F BF 84 21",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RAX;
                Log.Debug($"c_game_unit_fletcher_update_dropoff_produce_hook: unitId={unitId}");
                UnitFletcherDropOffProduceEventArg eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnFletcherDropOffProduce.Raise(eventArgs);
            }, volatileRegs);

        // 4C 69 C0 ?? ?? ?? ?? B8 ?? ?? ?? ?? 44 89 6C 24: poleturner->pickup: wood, has not taken it, rax
        // 48 69 D0 ?? ?? ?? ?? 66 42 89 B4 22: poleturner->dropoff: wood, has stored it, rax
        // 4C 69 C0 ?? ?? ?? ?? 44 89 6C 24: poleturner->dropoff: pike/spear, has not stored it, rax

        tx.AddContextHook(c_game_unit_poleturner_update_pickup_wood_hook,
            "4C 69 C0 ?? ?? ?? ?? B8 ?? ?? ?? ?? 44 89 6C 24",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RAX;
                Log.Debug($"c_game_unit_poleturner_update_pickup_wood_hook: unitId={unitId}");
                UnitPoleturnerPickUpPlanksEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnPoleturnerPickUpPlanks.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_poleturner_update_dropoff_wood_hook,
              "48 69 D0 ?? ?? ?? ?? 66 42 89 B4 22",
              static ctx =>
              {
                  int unitId = (int)ctx.Pointer->RAX;
                  Log.Debug($"c_game_unit_poleturner_update_dropoff_wood_hook: unitId={unitId}");
                  UnitPoleturnerDropOffPlanksEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                  UnitR3EventHooks.OnPoleturnerDropOffPlanks.Raise(eventArgs);
              }, volatileRegs);

        tx.AddContextHook(c_game_unit_poleturner_update_dropoff_produce_hook,
              "4C 69 C0 ?? ?? ?? ?? 44 89 6C 24",
              static ctx =>
              {
                  int unitId = (int)ctx.Pointer->RAX;
                  Log.Debug($"c_game_unit_poleturner_update_dropoff_produce_hook: unitId={unitId}");
                  UnitPoleturnerDropOffProduceEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                  UnitR3EventHooks.OnPoleturnerDropOffProduce.Raise(eventArgs);
              }, volatileRegs);

        // 48 69 C8 ?? ?? ?? ?? B8 ?? ?? ?? ?? 89 7C 24 ?? C7 44 24: blacksmiht->pikcup: iron, has not taken it, rax
        // 48 69 CA ?? ?? ?? ?? 66 46 89 AC 21: blacksmiht->dropoff: iron, has stored it, rdx
        // 48 69 C8 ?? ?? ?? ?? B8 ?? ?? ?? ?? 66 46 89 AC 21: blacksmiht->process: iron (removes it), has processed it, rax
        // 4C 69 C0 ?? ?? ?? ?? 89 7C 24 ?? C7 44 24: blacksmith->dropoff: sword/mace, has not stored it yet, rax

        tx.AddContextHook(c_game_unit_blacksmith_update_pickup_iron_hook,
            "48 69 C8 ?? ?? ?? ?? B8 ?? ?? ?? ?? 89 7C 24 ?? C7 44 24",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RAX;
                Log.Debug($"c_game_unit_blacksmith_update_pickup_iron_hook: unitId={unitId}");
                UnitBlacksmithPickUpIronEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnBlacksmithPickUpIron.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_blacksmith_update_dropoff_iron_hook,
            "48 69 CA ?? ?? ?? ?? 66 46 89 AC 21",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RDX;
                Log.Debug($"c_game_unit_blacksmith_update_dropoff_iron_hook: unitId={unitId}");
                UnitBlacksmithDropOffIronEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnBlacksmithDropOffIron.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_blacksmith_update_produce_hook,
            "48 69 C8 ?? ?? ?? ?? B8 ?? ?? ?? ?? 66 46 89 AC 21",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RAX;
                Log.Debug($"c_game_unit_blacksmith_update_produce_hook: unitId={unitId}");
                UnitBlacksmithProduceEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnBlacksmithProduce.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_blacksmith_update_dropoff_produce_hook,
            "4C 69 C0 ?? ?? ?? ?? 89 7C 24 ?? C7 44 24",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RAX;
                Log.Debug($"c_game_unit_blacksmith_update_dropoff_produce_hook: unitId={unitId}");
                UnitBlacksmithDropOffProduceEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnBlacksmithDropOffProduce.Raise(eventArgs);
            }, volatileRegs);


        // 48 69 C8 ?? ?? ?? ?? 42 C7 84 21 ?? ?? ?? ?? ?? ?? ?? ?? 66 42 C7 84 21: tanner->store at prod, has stroed it, rax
        // 48 69 C8 ?? ?? ?? ?? 33 FF 66 42 89 BC 21 ?? ?? ?? ?? 66 42 89 9C 21: tanner->process, has processed it, rax
        // 48 69 CB ?? ?? ?? ?? 66 42 FF 8C 21: tanner->dropoff: cowhide at goodsyard, has stored it, rbx

        tx.AddContextHook(c_game_unit_tanner_update_store_cowhides_hook,
            "48 69 C8 ?? ?? ?? ?? 42 C7 84 21 ?? ?? ?? ?? ?? ?? ?? ?? 66 42 C7 84 21",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RAX;
                Log.Debug($"c_game_unit_tanner_update_store_cowhides_hook: unitId={unitId}");
                UnitTannerStoreCowHidesEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnTannerStoreCowHides.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_tanner_update_produce_hook,
            "48 69 C8 ?? ?? ?? ?? 33 FF 66 42 89 BC 21 ?? ?? ?? ?? 66 42 89 9C 21",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RAX;
                Log.Debug($"c_game_unit_tanner_update_produce_hook: unitId={unitId}");
                UnitTannerProduceEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnTannerProduce.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_tanner_update_dropoff_cowhides_hook,
            "48 69 CB ?? ?? ?? ?? 66 42 FF 8C 21",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RBX;
                Log.Debug($"c_game_unit_tanner_update_dropoff_cowhides_hook: unitId={unitId}");
                UnitTannerDropOffCowHidesEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnTannerDropOffCowHides.Raise(eventArgs);
            }, new ContextHookOptions() { Registers = X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RBX });

        // 48 69 C8 ?? ?? ?? ?? B8 ?? ?? ?? ?? 89 74 24 ?? C7 44 24: armourer->pickup: iron, rax, doesnt have it yet
        // 48 69 C8 ?? ?? ?? ?? 66 42 89 AC 29 ?? ?? ?? ?? 66 42 89 AC 29: armourer->dropoff: iron, rax, has stored it
        // 41 BA ?? ?? ?? ?? 48 69 C8 ?? ?? ?? ?? 66 46 89 94 29: armourer->store: produce, rax, has stored it
        // 45 33 C0 48 69 C8 ?? ?? ?? ?? 42 0F BF 94 29 ?? ?? ?? ?? 48 8B CB: armourer->pickup: produce, rax, has taken it
        // 45 8B C7 48 69 C8 ?? ?? ?? ?? 42 0F BF 94 29 ?? ?? ?? ?? 48 8B CB: armourer->process, rax, has processed it
        // 44 8B C6 48 69 C8 ?? ?? ?? ?? 4A 0F BF 94 29: armourer->dropoff: produce, rax, has stored it

        tx.AddContextHook(c_game_unit_armourer_update_pickup_iron_hook,
            "48 69 C8 ?? ?? ?? ?? B8 ?? ?? ?? ?? 89 74 24 ?? C7 44 24",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RAX;
                Log.Debug($"c_game_unit_armourer_update_pickup_iron_hook: unitId={unitId}");
                UnitArmourerPickUpIronEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnArmourerPickUpIron.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_armourer_update_dropoff_iron_hook,
            "48 69 C8 ?? ?? ?? ?? 66 42 89 AC 29 ?? ?? ?? ?? 66 42 89 AC 29",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RAX;
                Log.Debug($"c_game_unit_armourer_update_dropoff_iron_hook: unitId={unitId}");
                UnitArmourerDropOffIronEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnArmourerDropOffIron.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_armourer_update_store_produce_hook,
            "41 BA ?? ?? ?? ?? 48 69 C8 ?? ?? ?? ?? 66 46 89 94 29",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RAX;
                Log.Debug($"c_game_unit_armourer_update_store_produce_hook: unitId={unitId}");
                UnitArmourerStoreProduceEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnArmourerStoreProduce.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_armourer_update_produce_hook,
            "45 8B C7 48 69 C8 ?? ?? ?? ?? 42 0F BF 94 29 ?? ?? ?? ?? 48 8B CB",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RAX;
                Log.Debug($"c_game_unit_armourer_update_produce_hook: unitId={unitId}");
                UnitArmourerProduceEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnArmourerProduce.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_armourer_update_dropoff_produce_hook,
            "44 8B C6 48 69 C8 ?? ?? ?? ?? 4A 0F BF 94 29",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RAX;
                Log.Debug($"c_game_unit_armourer_update_dropoff_produce_hook: unitId={unitId}");
                UnitArmourerDropOffProduceEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnArmourerDropOffProduce.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_armourer_update_pickup_produce_hook,
            "45 33 C0 48 69 C8 ?? ?? ?? ?? 42 0F BF 94 29 ?? ?? ?? ?? 48 8B CB",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RAX;
                Log.Debug($"c_game_unit_armourer_update_pickup_produce_hook: unitId={unitId}");
                UnitArmourerPickUpProduceEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnArmourerPickUpProduce.Raise(eventArgs);
            }, volatileRegs);

        // 48 69 CB ?? ?? ?? ?? 42 38 AC 29: hunter->dropoff: meat, rbx, has stored it
        // 45 8B 84 3E: hunter->pickup, r9, has not stored it yet

        tx.AddContextHook(c_game_unit_hunter_update_pickup_meat_hook,
            "45 8B 84 3E",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->R9;
                Log.Debug($"c_game_unit_hunter_update_pickup_meat_hook: unitId={unitId}");
                UnitHunterPickUpMeatEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnHunterPickUpMeat.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_hunter_update_dropoff_meat_hook,
            "48 69 CB ?? ?? ?? ?? 42 38 AC 29",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RBX;
                Log.Debug($"c_game_unit_hunter_update_dropoff_meat_hook: unitId={unitId}");
                UnitHunterDropOffMeatEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnHunterDropOffMeat.Raise(eventArgs);
            }, new ContextHookOptions() { Registers = X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RBX });


        // 41 BA ?? ?? ?? ?? 48 69 CE: quarry_grunt->pickup: stone, rsi, has taken it
        // 44 8D 4E ?? 48 69 C8 ?? ?? ?? ?? 66 42 89 BC 39: quarry_grunt->dropoff: rax, has stored it

        tx.AddContextHook(c_game_unit_quarry_grunt_update_pickup_stone_hook,
            "41 BA ?? ?? ?? ?? 48 69 CE",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RSI;
                Log.Debug($"c_game_unit_quarry_grunt_update_pickup_stone_hook: unitId={unitId}");
                UnitQuarryGruntPickUpStoneEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnQuarryGruntPickUpStone.Raise(eventArgs);
            }, new ContextHookOptions() { Registers = X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RSI });

        tx.AddContextHook(c_game_unit_quarry_grunt_update_dropoff_stone_hook,
            "44 8D 4E ?? 48 69 C8 ?? ?? ?? ?? 66 42 89 BC 39",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RAX;
                Log.Debug($"c_game_unit_quarry_grunt_update_dropoff_stone_hook: unitId={unitId}");
                UnitQuarryGruntDropOffStoneEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnQuarryGruntDropOffStone.Raise(eventArgs);
            }, volatileRegs);


        // 49 8B CE 48 69 F3: quarry_ox->depart: edx, starts departing, command not yet issued
        // 48 8D 15 ?? ?? ?? ?? 48 69 C8 ?? ?? ?? ?? 4A 0F BF 84 31: quarry_ox->dropoff: rax, has stored it

        tx.AddContextHook(c_game_unit_quarry_ox_update_depart_hook,
            "49 8B CE 48 69 F3",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RDX;
                Log.Debug($"c_game_unit_quarry_ox_update_depart_hook: unitId={unitId}");
                UnitQuarryOxDepartEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnQuarryOxDepart.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_quarry_ox_update_dropoff_stone_hook,
            "48 8D 15 ?? ?? ?? ?? 48 69 C8 ?? ?? ?? ?? 4A 0F BF 84 31",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RAX;
                Log.Debug($"c_game_unit_quarry_ox_update_dropoff_hook: unitId={unitId}");
                UnitQuarryOxDropOffStoneEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnQuarryOxDropOffStone.Raise(eventArgs);
            }, volatileRegs);

        // 41 8B D4 49 69 D9: miner2->pickup iron, r9, has not taken it yet
        // 48 8D 15 ?? ?? ?? ?? 48 69 CB ?? ?? ?? ?? 4A 0F BF 84 39: miner2->dropoff iron, rbx, has stored it [miner2=the guy who transports]
        tx.AddContextHook(c_game_unit_miner2_update_pickup_iron_hook,
            "48 8D 15 ?? ?? ?? ?? 48 69 CB ?? ?? ?? ?? 4A 0F BF 84 39",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RBX;
                Log.Debug($"c_game_unit_miner2_update_pickup_iron_hook: unitId={unitId}");
                UnitMiner2PickUpIronEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnMiner2PickUpIron.Raise(eventArgs);
            }, new ContextHookOptions() { Registers = X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RBX });

        tx.AddContextHook(c_game_unit_miner2_update_dropoff_iron_hook,
            "41 8B D4 49 69 D9",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->R9;
                Log.Debug($"c_game_unit_miner2_update_dropoff_iron_hook: unitId={unitId}");
                UnitMiner2DropOffIronEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnMiner2DropOffIron.Raise(eventArgs);
            }, volatileRegs);

        // 48 8D 15 ?? ?? ?? ?? 48 69 CB ?? ?? ?? ?? 4A 0F BF 84 21: pitcher->drop rawpitch, rbx, has stored it
        // 8B D7 48 69 D9: pitcher->pickup rawpitch, rcx, has not gained it yet
        tx.AddContextHook(c_game_unit_pitcher_update_pickup_rawpitch_hook,
            "8B D7 48 69 D9",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RCX;
                Log.Debug($"c_game_unit_pitcher_update_pickup_rawpitch_hook: unitId={unitId}");
                UnitPitcherPickUpRawPitchEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnPitcherPickUpRawPitch.Raise(eventArgs);
            }, volatileRegs);

        tx.AddContextHook(c_game_unit_pitcher_update_dropoff_rawpitch_hook,
            "48 8D 15 ?? ?? ?? ?? 48 69 CB ?? ?? ?? ?? 4A 0F BF 84 21",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RBX;
                Log.Debug($"c_game_unit_pitcher_update_dropoff_rawpitch_hook: unitId={unitId}");
                UnitPitcherDropOffRawPitchEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
                UnitR3EventHooks.OnPitcherDropOffRawPitch.Raise(eventArgs);
            }, new ContextHookOptions() { Registers = X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RBX });

        // c_game_unit_is_over_killingpit_update_hook
        tx.AddInline(c_game_unit_is_over_killingpit_update_hook,
            "41 BA ? ? ? ? 05",
            static (asm, overwritten, returnAddress) =>
            {
                // rbx: unitId
                // replacing:
                // 0: mov     r10d, 1
                // 1: add     eax, 0FFFFB9B0h
                // 2: mov     [r8+r11+67E5DE0h], eax
                asm.AddInstruction(overwritten[0]);
                asm.X64FastcallSafe((UInt64)Marshal.GetFunctionPointerForDelegate(static (UInt64 currentHealth, UInt64 unitId, UInt64 buildingOffset) =>
                {
                    int buildingId = ((int)buildingOffset / sizeof(GameBuilding));
                    Log.Debug($"c_game_unit_is_over_killingpit_update_hook: unitId={unitId}, buildingId={buildingId}, currentHealth={currentHealth}");
                    int damage = GameBuildingManagerAPI.Instance.KillingPitDamage;

                    UnitEnterKillingPitEventArgs eventArgs = new(EventHookPhase.Pre, (int)unitId, buildingId, (int)currentHealth, damage);
                    UnitR3EventHooks.OnUnitEnterKillingPit.Raise(eventArgs);
                    damage = eventArgs.Damage;

                    return currentHealth - (UInt64)damage;
                }), totalArgumentCount: 3, preserveRAX: false, prepareArgumentsAction: static (a) =>
                {
                    a.mov(rcx, rax);
                    a.mov(rdx, rbx);
                });
                asm.AddInstruction(overwritten[2]);
            });

        // unit transition hooks
        tx.AddContextHook(c_game_unit_transition_hook1_c_game_player_buy_mercenary,
            "48 63 C5 48 8D 15 ? ? ? ? 48 69 F0",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RBP;
                eChimps nextUnitType = (eChimps)ctx.Pointer->RDI;
                int playerId = (int)ctx.Pointer->R14;
                Log.Verbose($"c_game_unit_transition_hook1_c_game_player_buy_mercenary: unitId={unitId}, nextUnitType={nextUnitType}, playerId={playerId}");

                UnitTransitionEventArgs eventArgs = new(EventHookPhase.Pre, unitId, playerId, nextUnitType, UnitTransitionSource.MercenaryOutpost);
                UnitR3EventHooks.OnUnitTransition.Raise(eventArgs);

                ctx.Pointer->RDI = (UInt64)eventArgs.NextUnitType;

            }, new ContextHookOptions() { Registers = X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RBP | X64SmartCPUContextRegs.RDI | X64SmartCPUContextRegs.R14 });

        tx.AddContextHook(c_game_unit_transition_hook2_c_game_player_buy_eu_mercenary,
            "48 63 C7 48 69 C8 ? ? ? ? 48 03 D9",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RDI;
                eChimps nextUnitType = (eChimps)ctx.Pointer->R9;
                int playerId = (int)ctx.Pointer->RBP;
                Log.Verbose($"c_game_unit_transition_hook2_c_game_player_buy_eu_mercenary: unitId={unitId}, nextUnitType={nextUnitType}, playerId={playerId}");

                UnitTransitionEventArgs eventArgs = new(EventHookPhase.Pre, unitId, playerId, nextUnitType, UnitTransitionSource.EuropeanBarracks);
                UnitR3EventHooks.OnUnitTransition.Raise(eventArgs);

                ctx.Pointer->R9 = (UInt64)eventArgs.NextUnitType;

            }, new ContextHookOptions() { Registers = X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RDI | X64SmartCPUContextRegs.RBP });

        tx.AddContextHook(c_game_unit_transition_hook3_c_game_building_assign_worker,
            "48 63 C7 45 33 C9",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RDI;
                eChimps nextUnitType = (eChimps)ctx.Pointer->RBP;
                UInt64 playerId = *ctx.Pointer->GetStackPtr<UInt64>(0x58);

                //for (int i = 0; i < 28; i++)
                //{
                //    Log.Information($"Stack: {i*8}=[{(UInt64)(*ctx.Pointer->GetStackPtr<UInt64>(i * IntPtr.Size))}]");
                //}

                Log.Verbose($"c_game_unit_transition_hook3_c_game_building_assign_worker: unitId={unitId}, nextUnitType={nextUnitType}, playerId={playerId}");

                UnitTransitionEventArgs eventArgs = new(EventHookPhase.Pre, unitId, (int)playerId, nextUnitType, UnitTransitionSource.Worker);
                UnitR3EventHooks.OnUnitTransition.Raise(eventArgs);

                ctx.Pointer->RBP = (UInt64)eventArgs.NextUnitType;

            }, new ContextHookOptions() { Registers = X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RDI | X64SmartCPUContextRegs.RBP });

        tx.AddContextHook(c_game_unit_transition_hook4_c_game_unit_disband,
            "44 89 AB ? ? ? ? C7 83 ? ? ? ? ? ? ? ? 66 C7 83",
            static ctx =>
            {
                int unitId = (int)ctx.Pointer->RDI;
                eChimps nextUnitType = (eChimps)ctx.Pointer->R13;
                int playerId = GameUnitManagerAPI.Instance.GetOwner(unitId);

                Log.Verbose($"c_game_unit_transition_hook4_c_game_unit_disband: unitId={unitId}, nextUnitType={nextUnitType}, playerId={playerId}");

                UnitTransitionEventArgs eventArgs = new(EventHookPhase.Pre, unitId, playerId, nextUnitType, UnitTransitionSource.Disband);
                UnitR3EventHooks.OnUnitTransition.Raise(eventArgs);

                ctx.Pointer->R13 = (UInt64)eventArgs.NextUnitType;

            }, new ContextHookOptions() { Registers = X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RDI | X64SmartCPUContextRegs.R13 });

        // c_game_unit_hunter_query_for_target has no stable aob anchor point, so we have to resort to this
        if (scanner.Scan("E8 ? ? ? ? 49 0F BF 8C 3E").TryReadFlowControlTarget(out UInt64 c_game_unit_hunter_query_for_target_rva))
        {
            c_game_unit_hunter_query_for_target = Marshal.GetDelegateForFunctionPointer<c_game_unit_hunter_query_for_target_delegate>((IntPtr)(c_game_unit_hunter_query_for_target_rva));
            LogHelper.Information($"c_game_unit_hunter_query_for_target found at {(c_game_unit_hunter_query_for_target_rva).ToString("X16")}");

            InstructionWalker walker = new(c_game_unit_hunter_query_for_target_rva, new()
            {
                WalkRules =
                [
                    new InstructionWalkRule
                    {
                        InstructionSkip = 0,
                        StartFrom = InstructionWalkerStartMode.Begin,
                        Predicate = static (instr) =>
                        {
                            if (instr.Mnemonic != Iced.Intel.Mnemonic.Movzx) return false;

                            return true;
                        },
                        Extractor = InstructionWalkerHelpers.ExtractInstructionAddress
                    }
                ]
            }, Plugin.Instance.LoggerFactory.CreateLogger("InstructionWalker"));
            if (walker.TryWalk<UInt64>(out UInt64 movzxAddressVA))
            {
                LogHelper.Information($"Anchor point found: {movzxAddressVA.ToString("X16")}");
                tx.AddInline(c_game_unit_hunter_query_for_target_hook, movzxAddressVA, static (asm, overwritten, returnAddress) =>
                {
                    // movzx   eax, word ptr [rbx-212h]
                    // cmp     ax, 2Ch ; ','
                    // jz      short loc_1801890AD
                    // cmp     ax, 56h ; 'V'

                    // CONTEXT
                    // 18: [rsp+90] 000000000000002F 000000000000002F: HunterId
                    // RBX = 00007FFADB8E6148 + 0x29C: Querying Chimp Address
                    asm.X64FastcallSafe((UInt64)Marshal.GetFunctionPointerForDelegate(static (UInt64 queryChimpAddressVA_p0x29C, UInt64 hunterId) =>
                    {
                        bool result = false;
                        GameUnitManagerAPI unitAPI = GameUnitManagerAPI.Instance;
                        UInt64 queryChimpAddressVA = queryChimpAddressVA_p0x29C - 0x29C;

                        int queryUnitId = unitAPI.GetUnitArray().GetIndexByAddress(queryChimpAddressVA) + 1;
                        //Log.Verbose($"c_game_unit_hunter_query_for_target_hook: queryChimpAddress={queryChimpAddressVA.ToString("X16")}/querChimporig={queryChimpAddressVA_p0x29C.ToString("X16")}, queryUnitId={queryUnitId}, hunterId={hunterId}");

                        // base game logic emulation
                        if (queryChimpAddressVA != 0)
                        {
                            GameUnit* gameUnit = (GameUnit*)queryChimpAddressVA;
                            if ((gameUnit->r_UnitChimp == eChimps.CHIMP_TYPE_DEER) || (gameUnit->r_UnitChimp == eChimps.CHIMP_TYPE_GOAT))
                            {
                                result = true;
                            }
                        }

                        UnitHunterQueryTargetEventArgs eventArgs = new(EventHookPhase.Pre, queryUnitId, (int)hunterId);
                        UnitR3EventHooks.OnUnitHunterQueryTarget.Raise(eventArgs);
                        if (eventArgs.IsValidTarget.HasValue)
                        {
                            result = eventArgs.IsValidTarget.Value;
                        }

                        return result;
                    }), totalArgumentCount: 2, preserveRAX: false, prepareArgumentsAction: static (a) =>
                    {
                        a.mov(rcx, rbx);
                        a.mov(rdx, __qword_ptr[rsp + 0xF0]); // base is 0x90, but post-shift it resides at 0xF0
                    });
                    // eax doesnt seem to be relevant
                    asm.cmp(ax, 1);
                    // jnz     loc_18018918D ; if triggered, jump to exit route
                });
            }
            else LogHelper.Error($"Could not find anchor point");
        }
        else LogHelper.Error($"Could not retrieve function ptr to c_game_unit_hunter_query_for_target");

        // Experimental callback placer for r_AIState tracking.
        Microsoft.Extensions.Logging.ILogger stateTrackerLogger = Plugin.Instance.LoggerFactory.CreateLogger("AIStateTracker");
        Array eChimpsArray = Enum.GetValues(typeof(eChimps));
        for (int i = 0; i < eChimpsArray.Length; i++)
        {
            eChimps chimpValue = (eChimps)i;
            if (!GameUnitManagerAPI.Instance.IsAIStateTracked(chimpValue))
                continue;

            HookHandle<X64FunctionCloneHook> newCloneHook = new HookHandle<X64FunctionCloneHook>();
            UInt64 fnAddress = ((UInt64*)(GameGlobalsManager.Instance.GameUnitFunctionsVTable.Pointer))[(int)chimpValue];

            LogHelper.Information($"Installing AIStateTracker hook for [{chimpValue}] at [{fnAddress.ToString("X16")}]");
            tx.AddCloneHook(newCloneHook, ((UInt64*)(GameGlobalsManager.Instance.GameUnitFunctionsVTable.Pointer))[(int)chimpValue],
                [new FunctionClonePatch
            {
                Predicate = static instr =>
                    instr.Mnemonic == Mnemonic.Mov &&
                    instr.HasOpKind(OpKind.Memory) &&
                    instr.MemoryDisplacement32 == 0x918,

                Generator = (Assembler asm, Instruction original, ref bool suppress) =>
                {
                    Register sourceReg = original.GetOpRegister(1); // Register.None if immediate
                    bool isImmediate = sourceReg == Register.None;

                    Register memBase  = original.MemoryBase;
                    Register memIndex = original.MemoryIndex;
                    AssemblerRegister64 asmBase  = new AssemblerRegister64(memBase.GetFullRegister());
                    AssemblerRegister64 asmIndex = memIndex != Register.None ? new AssemblerRegister64(memIndex.GetFullRegister()) : default;

                    asm.push(r10);
                    asm.push(r11);
                    asm.push(rcx);

                    // Snapshot the base and index pointers
                    asm.mov(r10, asmBase);
                    if (memIndex != Register.None)
                        asm.mov(r11, asmIndex);

                    // Load the value-to-be-written into rcx (arg1)
                    if (!isImmediate)
                        asm.movzx(rcx, new AssemblerRegister16(sourceReg));
                    else
                        asm.mov(rcx, original.GetImmediate(1)); // imm is already zero-extended into rcx

                    System.Func<UInt64, UInt16> callback = (UInt64 value) => {
                        int unitId = GameUnitManagerAPI.Instance.GetCurrentContextUnitId();
                        //LogHelper.Debug($"[{chimpValue}] WRITTEN AI STATE: {value}, unitId: {unitId}");

                        UnitAIStateEventArgs eventArgs = new(EventHookPhase.Pre, unitId, chimpValue, (int)value);
                        UnitR3EventHooks.OnUnitAIStateChange.Raise(eventArgs);

                        return (UInt16)eventArgs.State;
                    };
                    UInt64 cbPtr = (UInt64)newCloneHook?.Hook?.PinDelegate(callback); // keep alive for hook lifetime

                    // Call modder callback: (UInt64 proposedState) -> UInt16 newState
                    asm.X64FastcallSafe(cbPtr, 1, preserveRAX: false);
                    //asm.int3();

                    if (memIndex != Register.None)
                        asm.lea(rcx, __[r10 + r11 + 0x918]);
                    else
                        asm.lea(rcx, __[r10 + 0x918]);

                    asm.mov(__word_ptr[rcx], ax);

                    asm.pop(rcx);
                    asm.pop(r11);
                    asm.pop(r10);

                    suppress = true;
                }
            }], name: $"unitStateTracker_{chimpValue}");
        }

        // wall targetability control (part 1)
        tx.AddContextHook(c_game_unit_control_capability_unknown,
            "49 63 C1 48 8D 2D ? ? ? ? 48 0F BF 94 45 ? ? ? ? 42 0F BF 84 26",
            static ctx =>
            {
                //eChimps.CHIMP_TYPE_ARAB_SLAVE = 71
                //eChimps.CHIMP_TYPE_ARAB_SWORDSMAN = 75
                GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
                int unitId = unitApi.GetUnitArray().GetIndexByOffset(ctx.Pointer->RSI);

                // RDX = unit type
                // RDI = can-attack value
                eChimps unit = unitApi.GetType(unitId); //ctx.Pointer->RDX;
                int attackWallBehaviour = unitApi.GetCanAttackWalls(unit);

                //LogHelper.Information($"unit type: {unit}, beh: {attackWallBehaviour}, unit id: {unitId}");

                if (attackWallBehaviour != -2)
                {
                    ctx.Pointer->RDI = (UInt64)attackWallBehaviour; //0x5F5E100;
                }
                //ctx.Pointer->RDI = 0x5F5E100;
            }, new ContextHookOptions() { Registers = X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RDI | X64SmartCPUContextRegs.RSI });

        // wall targetability control (part 2)
        // Unit_SetCanAttackWalls(eChimps.CHIMP_TYPE_ARAB_SLAVE, 0x5F5E100)
        // Unit_SetCanAttackWalls(eChimps.CHIMP_TYPE_ARAB_SWORDSMAN, -1)
        tx.AddContextHook(c_game_unit_fsm_arabslave_attack_capability_wall,
            "4A 63 84 21 ? ? ? ? 4C 0F BF B4 45", // movsxd  rax, dword ptr [rcx+r12+0A00h]
            static ctx =>
            {
                int buildingTile = (int)ctx.Pointer->R14;
                int tileId = (int)ctx.Pointer->RAX;
                if (tileId == 0)
                    return;

                GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;

                // buildingtile is zero, try wall tile
                if ((buildingTile == 0) && (tileApi.HasTilePropertyFlag(tileId, Interop.Enums.TilePropertyFlag.IsWall)))
                {
                    ctx.Pointer->R14 = (UInt64)tileId;
                }
            }, new ContextHookOptions()
            {
                Registers = X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.R14 | X64SmartCPUContextRegs.RAX,
                Placement = OverwrittenInstructionPlacement.BeforeCallback
            }
        );

        tx.AddInline(c_game_unit_fsm_arabslave_attack_capability_wall2,
            "4D 69 FE ? ? ? ? 48 8D 1D", // imul    r15, r14, 32Ch
            static (asm, overwritten, returnAddress) =>
            {
                Label lblSkip = asm.CreateLabel("lblSkip");

                asm.push(rax);
                asm.push(rcx);
                asm.mov(rcx, r14);
                asm.X64FastcallSafe((UInt64)Marshal.GetFunctionPointerForDelegate(static (UInt64 rcx) =>
                {
                    int buildingId = (int)rcx;
                    //LogHelper.Verbose($"buildingId={buildingId}");
                    if (!GameBuildingManagerAPI.Instance.IsValid(buildingId))
                    {
                        return false;
                    }
                    return true;
                }), 1, preserveRAX: false);
                asm.test(rax, rax);
                asm.pop(rcx);
                asm.pop(rax);

                asm.je(lblSkip);
                asm.AddInstructions(overwritten);
                /*
                imul r15,r14,32C
                lea rbx,qword ptr ds:[7FFFA9D39BA0]
                cmp word ptr ds:[r15+rbx+31A],di
                */
                asm.Label(ref lblSkip);
            }, hookSize: 22
        );

        tx.AddInline(c_game_unit_fsm_arabslave_attack_capability_wall3,
            "41 8B D6 48 8B CB E8 ? ? ? ? 85 C0 48 63 05", // mov     edx, r14d
            static (asm, overwritten, returnAddress) =>
            {
                Label lblSkip = asm.CreateLabel("lblSkip");

                asm.push(rax);
                asm.push(rcx);
                asm.mov(rcx, r14);
                asm.X64FastcallSafe((UInt64)Marshal.GetFunctionPointerForDelegate(static (UInt64 rcx) =>
                {
                    int buildingId = (int)rcx;
                    //LogHelper.Verbose($"buildingId={buildingId}");
                    if (!GameBuildingManagerAPI.Instance.IsValid(buildingId))
                    {
                        return false;
                    }
                    return true;
                }), 1, preserveRAX: false);
                asm.test(rax, rax);
                asm.pop(rcx);
                asm.pop(rax);

                asm.je(lblSkip);
                asm.AddInstructions(overwritten[..^1]);
                /*
                mov     edx, r14d
                mov     rcx, rbx
                call    c_game_get_unknown_building_attribute
                test    eax, eax
                */
                asm.Label(ref lblSkip);
                asm.AddInstruction(overwritten[^1]); // movsxd  rax, cs:gCurrentContextUnitId
            }
        );

    }

    internal static HookHandle<X64InlineHook> c_game_unit_fsm_arabslave_attack_capability_wall3 = new();
    internal static HookHandle<X64InlineHook> c_game_unit_fsm_arabslave_attack_capability_wall2 = new();
    internal static HookHandle<X64InlineHook> c_game_unit_fsm_arabslave_attack_capability_wall = new();

    internal static HookHandle<X64InlineHook> c_game_unit_transition_hook1_c_game_player_buy_mercenary = new();
    internal static HookHandle<X64InlineHook> c_game_unit_transition_hook2_c_game_player_buy_eu_mercenary = new();
    internal static HookHandle<X64InlineHook> c_game_unit_transition_hook3_c_game_building_assign_worker = new();
    internal static HookHandle<X64InlineHook> c_game_unit_transition_hook4_c_game_unit_disband = new();

    internal static HookHandle<X64InlineHook> c_game_unit_control_capability_unknown = new();

    internal static List<HookHandle<X64FunctionCloneHook>> c_game_unit_aistatetracker_list = new();

    internal delegate void c_game_unit_hunter_query_for_target_delegate(IntPtr pGameUnitManager, int unitId);
    internal static c_game_unit_hunter_query_for_target_delegate c_game_unit_hunter_query_for_target;

    internal static HookHandle<X64InlineHook> c_game_unit_hunter_query_for_target_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_is_over_killingpit_update_hook = new();
    internal static HookHandle<X64InlineHook> c_game_player_buy_mercenary_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_damaged_by_projectile_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_killed_by_projectile_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_killed_by_melee_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_take_fire_damage = new();
    internal static HookHandle<X64InlineHook> c_game_unit_bedouin_healer_heal_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_woodcutter_update_pickup_planks_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_woodcutter_update_dropoff_planks_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_farmer_cattle_update_pickup_cheese_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_farmer_cattle_update_dropoff_cheese_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_farmer_apple_update_pickup_apple_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_farmer_apple_update_dropoff_apple_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_farmer_hemp_update_pickup_hemp_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_farmer_hemp_update_dropoff_hemp_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_baker_update_pickup_flour_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_baker_update_pickup_bread_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_baker_update_dropoff_bread_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_farmer_wheat_update_pickup_wheat_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_farmer_wheat_update_dropoff_wheat_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_miller_update_pickup_wheat_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_miller_update_dropoff_flour_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_miller_update_pickup_flour_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_brewer_update_pickup_hemp_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_brewer_update_dropoff_hemp_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_brewer_update_pickup_ale_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_brewer_update_dropoff_ale_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_brewer_update_produced_ale_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_innkeeper_update_pickup_ale_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_innkeeper_update_dropoff_ale_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_fletcher_update_pickup_wood_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_fletcher_update_dropoff_wood_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_fletcher_update_dropoff_produce_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_poleturner_update_pickup_wood_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_poleturner_update_dropoff_wood_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_poleturner_update_dropoff_produce_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_blacksmith_update_pickup_iron_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_blacksmith_update_dropoff_iron_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_blacksmith_update_produce_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_blacksmith_update_dropoff_produce_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_tanner_update_store_cowhides_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_tanner_update_produce_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_tanner_update_dropoff_cowhides_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_armourer_update_pickup_iron_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_armourer_update_dropoff_iron_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_armourer_update_store_produce_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_armourer_update_produce_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_armourer_update_dropoff_produce_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_armourer_update_pickup_produce_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_hunter_update_pickup_meat_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_hunter_update_dropoff_meat_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_quarry_grunt_update_pickup_stone_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_quarry_grunt_update_dropoff_stone_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_quarry_ox_update_depart_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_quarry_ox_update_dropoff_stone_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_miner2_update_pickup_iron_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_miner2_update_dropoff_iron_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_pitcher_update_pickup_rawpitch_hook = new();
    internal static HookHandle<X64InlineHook> c_game_unit_pitcher_update_dropoff_rawpitch_hook = new();


    // __int64 __fastcall DLL_TroopSelection(
    // int mouseState, char rightDown, char rightUp, int count, __int64 selectedChimps,
    // char selection_on, char selection_established, int underCursorCount, unsigned int *underCursorChimps,
    // int mousePosX, int mousePosY, char overTopHalf, unsigned int onScreenCount, __int64 onScreenChimps)
    // DLL_TroopSelection
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_dll_troopselection_delegate(int mouseState, byte rightDown, byte rightUp, UInt32 count, UInt64 pSelectedChimps,
        byte selectionOn, byte selectionEstablished, UInt32 underCursorCount, UInt64 pUnderCursorChimps,
        int mousePosX, int mousePosY, byte overTopHalf, UInt32 onScreenCount, UInt64 pOnScreenChimps);
    internal static DetourHandle<c_game_dll_troopselection_delegate> c_game_dll_troopselection_hook = new();
    public static Int64 c_game_dll_troopselection_hook_impl(int mouseState, byte rightDown, byte rightUp, UInt32 selectedChimpsCount, UInt64 pSelectedChimps,
        byte selectionOn, byte selectionEstablished, UInt32 underCursorCount, UInt64 pUnderCursorChimps,
        int mousePosX, int mousePosY, byte overTopHalf, UInt32 onScreenCount, UInt64 pOnScreenChimps)
    {
        //Log.Debug($"c_game_dll_troopselection_hook_impl: selectedChimpsCount={selectedChimpsCount}, underCursorCount={underCursorCount}, onScreenCount={onScreenCount}");

        try
        {
            GameUnitManagerAPI.FilterUnselectableUnits(pSelectedChimps, selectedChimpsCount);
            GameUnitManagerAPI.FilterUnselectableUnits(pUnderCursorChimps, underCursorCount);
            GameUnitManagerAPI.FilterUnselectableUnits(pOnScreenChimps, onScreenCount);
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error while filtering unselectable units");
        }

        return c_game_dll_troopselection_hook.Original(mouseState, rightDown, rightUp, selectedChimpsCount, pSelectedChimps, selectionOn, selectionEstablished,
            underCursorCount, pUnderCursorChimps, mousePosX, mousePosY, overTopHalf, onScreenCount, pOnScreenChimps);
    }

    // 48 63 C1 4C 8D 1D ?? ?? ?? ?? 4C 69 C8
    // __int64 __fastcall c_game_unit_calculate_worker_good_yield(int unitId, int good_amount, int b50PercentBonus)
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_unit_calculate_worker_good_yield_delegate(int unitId, int goodAmount, int b50PercentBonus);
    internal static DetourHandle<c_game_unit_calculate_worker_good_yield_delegate> c_game_unit_calculate_worker_good_yield_hook = new();
    public static Int64 c_game_unit_calculate_worker_good_yield_hook_impl(int unitId, int goodAmount, int b50PercentBonus)
    {
        LogHelper.Debug($"unitId={unitId}, goodAmount={goodAmount}, b50PercentBonus={b50PercentBonus}");

        UnitCalculateBonusYieldEventArgs eventArgs = new(EventHookPhase.Pre, unitId, goodAmount, b50PercentBonus);
        UnitR3EventHooks.OnCalculateBonusYield.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_unit_calculate_worker_good_yield_hook.Original(
                eventArgs.UnitId,
                eventArgs.GoodAmount,
                eventArgs.b50PercentBonus
            );
            eventArgs.ReturnValue = originalResult;
            UnitCalculateBonusYieldEventArgs postEventArgs = new(EventHookPhase.Post, unitId, goodAmount, b50PercentBonus)
            {
                ReturnValue = originalResult
            };
            UnitR3EventHooks.OnCalculateBonusYield.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }

    // __int64 __fastcall c_game_unit_issueorder_movehere(__int64 pUnitManager, unsigned int unit_id, unsigned int tileX, unsigned int tileY, int a5)
    // 48 89 5C 24 ? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ? 48 63 F2
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_unit_issueorder_movehere_delegate(NativePointer<GameUnitManager> pGameUnitManager, int unitId, int tileX, int tileY, int unknown);
    internal static DetourHandle<c_game_unit_issueorder_movehere_delegate> c_game_unit_issueorder_movehere_hook = new();
    public static Int64 c_game_unit_issueorder_movehere_hook_impl(NativePointer<GameUnitManager> pGameUnitManager, int unitId, int tileX, int tileY, int unknown)
    {
        LogHelper.Verbose($"pGameUnitManager={new IntPtr(pGameUnitManager).ToString("X16")}, unitId={unitId}, tileX={tileX}, tileY={tileY}, unknown={unknown}");

        UnitMoveHereEventArgs eventArgs = new(EventHookPhase.Pre, unitId, tileX, tileY, unknown);
        UnitR3EventHooks.OnUnitMoveHere.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_unit_issueorder_movehere_hook.Original(
                pGameUnitManager,
                eventArgs.UnitId,
                eventArgs.TileX,
                eventArgs.TileY,
                eventArgs.Unknown
            );
            eventArgs.ReturnValue = originalResult;
            UnitMoveHereEventArgs postEventArgs = new(EventHookPhase.Post, unitId, tileX, tileY, unknown)
            {
                ReturnValue = originalResult
            };
            UnitR3EventHooks.OnUnitMoveHere.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }

    // __int64 __fastcall c_game_unit_delete(__int64 pGameUnitManager, unsigned int unitId)
    // 48 89 5C 24 ? 48 89 6C 24 ? 48 89 74 24 ? 48 89 7C 24 ? 41 56 48 83 EC ? 48 63 FA 48 8D 2D
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_unit_delete_hook_delegate(NativePointer<GameUnitManager> pGameUnitManager, UInt32 unitId);
    internal static DetourHandle<c_game_unit_delete_hook_delegate> c_game_unit_delete_hook = new();
    public static Int64 c_game_unit_delete_hook_impl(NativePointer<GameUnitManager> pGameUnitManager, UInt32 unitId)
    {
        LogHelper.Verbose($"pGameUnitManager={new IntPtr(pGameUnitManager).ToString("X16")}, unitId={unitId}");

        UnitDeleteEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
        UnitR3EventHooks.OnUnitDelete.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_unit_delete_hook.Original(
                pGameUnitManager,
                eventArgs.UnitId
            );
            eventArgs.ReturnValue = originalResult;
            UnitDeleteEventArgs postEventArgs = new(EventHookPhase.Post, unitId)
            {
                ReturnValue = originalResult
            };
            UnitR3EventHooks.OnUnitDelete.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }

    // void __fastcall c_game_unit_handle_movement_update(__int64 pUnitManager, __int64 unitId)
    // 48 63 C2 4C 69 C0 ?? ?? ?? ?? 41 83 BC 08
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void c_game_unit_handle_movement_delegate(NativePointer<GameUnitManager> pGameUnitManager, UInt32 unitId);
    internal static DetourHandle<c_game_unit_handle_movement_delegate> c_game_unit_handle_movement_hook = new();
    public static void c_game_unit_handle_movement_hook_impl(NativePointer<GameUnitManager> pGameUnitManager, UInt32 unitId)
    {
        //LogHelper.Verbose($"pGameUnitManager={new IntPtr(pGameUnitManager).ToString("X16")}, unitId={unitId}");

        UnitMovementEventArgs eventArgs = new(EventHookPhase.Pre, unitId);
        UnitR3EventHooks.OnUnitMovement.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            c_game_unit_handle_movement_hook.Original(
                pGameUnitManager,
                eventArgs.UnitId
            );
            UnitMovementEventArgs postEventArgs = new(EventHookPhase.Post, unitId);
            UnitR3EventHooks.OnUnitMovement.Raise(postEventArgs);
        }
    }

    // __int64 __fastcall c_game_unit_takedamage_projectile(__int64 pGameUnitManager, int attackedUnitId, int projectileId, int value)
    // 48 89 5C 24 ?? 48 89 4C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ?? 45 33 D2
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_unit_takedamage_projectile_Delegate(NativePointer<GameUnitManager> pGameUnitManager, int attackedUnitId, int projectileId, int value);
    internal static DetourHandle<c_game_unit_takedamage_projectile_Delegate> c_game_unit_takedamage_projectile_hook = new();
    public static Int64 c_game_unit_takedamage_projectile_hook_impl(NativePointer<GameUnitManager> pGameUnitManager, int attackedUnitId, int projectileId, int value)
    {
        LogHelper.Verbose($"pGameUnitManager={new IntPtr(pGameUnitManager).ToString("X16")}, attackedUnitId={attackedUnitId}, projectileId={projectileId}, value={value}");

        UnitTakeDamageByProjectileEventArgs eventArgs = new(EventHookPhase.Pre, attackedUnitId, projectileId, value);
        UnitR3EventHooks.OnUnitTakeProjectileDamage.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_unit_takedamage_projectile_hook.Original(
                pGameUnitManager,
                eventArgs.AttackedUnitId,
                eventArgs.ProjectileId,
                eventArgs.UnknownBool
            );
            eventArgs.ReturnValue = originalResult;
            UnitTakeDamageByProjectileEventArgs postEventArgs = new(EventHookPhase.Post, attackedUnitId, projectileId, value)
            {
                ReturnValue = originalResult
            };
            UnitR3EventHooks.OnUnitTakeProjectileDamage.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }

    // __int64 __fastcall c_game_unit_takedamage(__int64 a1, __int64 a2, int a3, int a4)
    // 48 89 5C 24 18 55 56 57 41 54 41 55 41 56 41 57 48 83 EC ?? 49
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate long c_game_unit_takedamage_melee_Delegate(NativePointer<GameUnitManager> pGameUnitManager, int attackingUnitId, int damagedUnitId, int value);
    internal static DetourHandle<c_game_unit_takedamage_melee_Delegate> c_game_unit_takedamage_melee_hook = new();
    public static long c_game_unit_takedamage_melee_hook_impl(NativePointer<GameUnitManager> pGameUnitManager, int attackingUnitId, int damagedUnitId, int value)
    {
        LogHelper.Verbose($"pGameUnitManager={new IntPtr(pGameUnitManager).ToString("X16")}, attackingUnitId={attackingUnitId}, damagedUnitId={damagedUnitId}, value={value}");

        UnitTakeDamageByMeleeEventArgs eventArgs = new(EventHookPhase.Pre, attackingUnitId, damagedUnitId, value);
        UnitR3EventHooks.OnUnitTakeMeleeDamage.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_unit_takedamage_melee_hook.Original(
                pGameUnitManager,
                eventArgs.AttackingUnitId,
                eventArgs.DamagedUnitId,
                eventArgs.Damage
            );
            eventArgs.ReturnValue = originalResult;
            UnitTakeDamageByMeleeEventArgs postEventArgs = new(EventHookPhase.Post, attackingUnitId, damagedUnitId, value)
            {
                ReturnValue = originalResult
            };
            UnitR3EventHooks.OnUnitTakeMeleeDamage.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }

    // __int64 __fastcall c_game_unit_spawn_wip(__int64 pGameUnitManager, int unitId)
    // 48 89 5C 24 ?? 57 48 83 EC ?? B8 ?? ?? ?? ?? 48 63 DA
    /*[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_unit_spawn_delegate(GameUnitManager* pGameUnitManager, UInt32 unitId);
    public static X64ManagedFunctionDetourAOB<c_game_unit_spawn_delegate>? c_game_unit_spawn_hook;

    public static Int64 c_game_unit_spawn_hook_impl(GameUnitManager* pGameUnitManager, UInt32 unitId)
    {
        Log.Information($"c_game_unit_spawn_hook_impl: a1={new IntPtr(pGameUnitManager).ToString("X16")}, unitId={unitId}");
        return c_game_unit_spawn_hook!.Hook!.Trampoline!(pGameUnitManager, unitId);
    }*/

    // __int64 __fastcall c_game_unit_spawn_ex(int *pGameUnitManager, int a2, __int16 a3, __int16 tile_x, __int16 tile_y, __int16 a6, int unitChimpType)
    // 40 53 55 56 48 83 EC ?? 44 8B 1D
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate Int64 c_game_unit_spawn_ex_delegate(NativePointer<GameUnitManager> pGameUnitManager, int playerOwnerId, int playerColorId, int worldTileX, int worldTileY, int heightElevation, eChimps unitType);
    internal static DetourHandle<c_game_unit_spawn_ex_delegate> c_game_unit_spawn_ex_hook = new();
    public static Int64 c_game_unit_spawn_ex_hook_impl(NativePointer<GameUnitManager> pGameUnitManager, int playerOwnerId, int playerColorId, int worldTileX, int worldTileY, int heightElevation, eChimps unitType)
    {
        LogHelper.Debug($"pGameUnitManager={new IntPtr(pGameUnitManager).ToString("X16")}, playerOwnerId={playerOwnerId}, playerColorId={playerColorId}, worldTileX={worldTileX}, worldTileY={worldTileY}, heightElevation={heightElevation}, unitType={unitType}");

        UnitCreateEventArgs eventArgs = new(EventHookPhase.Pre, playerColorId, playerOwnerId, worldTileX, worldTileY, heightElevation, unitType);
        UnitR3EventHooks.OnUnitCreate.Raise(eventArgs);
        if (!eventArgs.SkipOriginalFunction)
        {
            Int64 originalResult = c_game_unit_spawn_ex_hook.Original(
                pGameUnitManager,
                eventArgs.PlayerOwnerId,
                eventArgs.PlayerColorId,
                eventArgs.WorldTileX,
                eventArgs.WorldTileY,
                eventArgs.HeightElevation,
                eventArgs.UnitType
            );
            eventArgs.ReturnValue = originalResult;
            UnitCreateEventArgs postEventArgs = new(EventHookPhase.Post, playerColorId, playerOwnerId, worldTileX, worldTileY, heightElevation, unitType)
            {
                ReturnValue = originalResult
            };
            UnitR3EventHooks.OnUnitCreate.Raise(postEventArgs);
            eventArgs.ReturnValue = postEventArgs.ReturnValue;
        }
        return eventArgs.ReturnValue;
    }
}
