using MonoMod.RuntimeDetour;
using SHCDESE.Logging;
using System;
using System.Reflection;

namespace SHCDESE.ManagedHooks;

public class ManagedDetour<T> where T : Delegate
{
    public Hook Hook { get; set; }
    public T Trampoline { get; set; }

    public ManagedDetour(MethodBase from, Delegate to)
    {
        Hook = new Hook(from, to);
        Trampoline = Hook.GenerateTrampoline<T>();

        LogHelper.Information($"Created detour [{from.Name}]->[{to.Method.Name ?? "N/A"}]");
    }
}
