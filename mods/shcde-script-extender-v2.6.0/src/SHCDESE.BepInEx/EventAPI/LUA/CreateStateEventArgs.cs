using System;

namespace SHCDESE.EventAPI.Lua;

public class CreateStateEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public NLua.Lua? Lua { get; }

    public CreateStateEventArgs(EventHookPhase phase, NLua.Lua? lua)
    {
        Phase = phase;
        Lua = lua;
    }
}
