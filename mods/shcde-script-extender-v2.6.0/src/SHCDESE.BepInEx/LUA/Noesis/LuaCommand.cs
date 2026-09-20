using NLua;
using SHCDESE.Logging;
using System;
using System.Windows.Input;

namespace SHCDESE.LUA.Noesis;

/// <summary>
/// Wraps a Lua function into an ICommand so XAML buttons can invoke it.
/// </summary>
public class LuaCommand : ICommand
{
    private readonly LuaFunction _luaFunc;

    public event EventHandler? CanExecuteChanged;

    public LuaCommand(LuaFunction luaFunc)
    {
        _luaFunc = luaFunc;
    }

    public bool CanExecute(object? parameter)
    {
        // For simplicity, we assume commands can always execute.
        // You could extend this to check a boolean in Lua if needed.
        return true;
    }

    public void Execute(object? parameter)
    {
        try
        {
            if (_luaFunc != null)
            {
                if (parameter != null)
                    _luaFunc.Call(new object[] { parameter });
                else
                    _luaFunc.Call();
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error executing LuaCommand");
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}