using UnityEngine;

namespace SHCDESE.EventAPI.Input;

public class UnityInputEventArgs : EventHookBase
{
    public KeyCode Key { get; set; }

    /// <summary>
    /// Set to FALSE to block the input from reaching the rest of the game.
    /// </summary>
    public bool Result { get; set; } = true;

    public UnityInputEventArgs(EventHookPhase phase, KeyCode key)
    {
        Phase = phase;
        Key = key;
    }
}