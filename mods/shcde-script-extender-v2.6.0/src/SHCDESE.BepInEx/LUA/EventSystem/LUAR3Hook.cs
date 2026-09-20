using NLua;
using SHCDESE.EventAPI;
using System;
using R3;
using Serilog;
namespace SHCDESE.Lua.EventSystem;

public class LUAR3Hook<T> : ILUAR3Hook where T : EventHookBase
{
    private readonly R3EventHook<T> _csharpHook;

    internal LUAR3Hook(R3EventHook<T> csharpHook)
    {
        _csharpHook = csharpHook;
    }

    public LUAR3Subscription Subscribe(LuaFunction function)
    {
        if (function == null)
        {
            Log.Warning($"R3EventHook<{typeof(T).Name}> - Subscribe: LuaFunction is null!");
            return null;
        }

        // - Subscribe to the C# R3 stream.
        // - The action for the subscription calls the Lua function, passing the event args.
        // - Wrap the returned IDisposable in our LuaSubscription class.
        IDisposable disposable = _csharpHook.Observable.Subscribe(eventArgs =>
        {
            try
            {
                // The C# EventArgs object is passed directly to Lua.
                // NLua will automatically allow Lua to access its public properties.
                function.Call(eventArgs);
            }
            catch (Exception ex)
            {
                if (ex is NLua.Exceptions.LuaException luaEx)
                {
                    // LuaException.Message usually includes the file and line number.
                    Log.Error($"R3EventHook<{typeof(T).Name}> - Lua error: {luaEx.ToString()}");
                }
                else
                {
                    // Fallback for other .NET exceptions
                    Log.Error($"R3EventHook<{typeof(T).Name}> - C# error in Lua hook: {ex.ToString()}");
                }
            }
        });

        return new LUAR3Subscription(disposable);
    }
}
