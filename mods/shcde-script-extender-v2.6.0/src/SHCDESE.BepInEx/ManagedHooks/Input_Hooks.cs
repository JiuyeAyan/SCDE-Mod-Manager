using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Input;
using SHCDESE.Logging;
using System;
using UnityEngine;

namespace SHCDESE.ManagedHooks;

public partial class ManagedHookManager
{
    internal delegate void KeyManager_Update_Delegate(KeyManager self);
    internal static ManagedDetour<KeyManager_Update_Delegate> keyManager_Update_Hook;

    internal void KeyManager_Update_Hook_Impl(KeyManager self)
    {
        // Let the game update its internal state first
        keyManager_Update_Hook.Trampoline(self);

        try
        {
            int[] values = KeyManager.instance.values;
            int[] keys = KeyManager.instance.keys;
            if (values == null || keys == null) return;

            // Iterate over the keys
            for (int i = 0; i < values.Length; i++)
            {
                int state = keys[i];
                // States defined in KeyManager: 0=Off, 1=Down, 2=Held, 3=Up

                if (state == 0) 
                    continue; // Optimization: skip idle keys

                KeyCode code = (KeyCode)values[i];
                bool shouldBlock = false;

                if (state == 1) // Down
                {
                    UnityInputEventArgs args = new(EventHookPhase.Pre, code);
                    InputR3EventHooks.OnKeyDown.Raise(args);

                    if (!args.Result) shouldBlock = true;

                    // Post event
                    InputR3EventHooks.OnKeyDown.Raise(new UnityInputEventArgs(EventHookPhase.Post, code) { Result = args.Result });
                }
                else if (state == 3) // Up
                {
                    UnityInputEventArgs args = new(EventHookPhase.Pre, code);
                    InputR3EventHooks.OnKeyUp.Raise(args);

                    if (!args.Result) shouldBlock = true;

                    InputR3EventHooks.OnKeyUp.Raise(new UnityInputEventArgs(EventHookPhase.Post, code) { Result = args.Result });
                }
                else if (state == 2) // Held
                {
                    UnityInputEventArgs args = new(EventHookPhase.Pre, code);
                    InputR3EventHooks.OnKey.Raise(args);

                    if (!args.Result) shouldBlock = true;

                    InputR3EventHooks.OnKey.Raise(new UnityInputEventArgs(EventHookPhase.Post, code) { Result = args.Result });
                }

                // Apply Blocking
                if (shouldBlock)
                {
                    // Resetting state to 0 effectively hides it from the rest of the game
                    // which reads this array via IsKeyPressed etc.
                    keys[i] = 0;
                }
            }
        }
        catch (Exception ex)
        {
            if (!_hasLoggedKeyManagerError)
            {
                LogHelper.Error(ex, "Error inside KeyManager.Update hook");
                _hasLoggedKeyManagerError = true;
            }
        }
    }

    private bool _hasLoggedKeyManagerError = false;
}
