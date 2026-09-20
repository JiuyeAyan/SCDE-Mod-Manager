using NLua;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace SHCDESE.LUA.Noesis;

/// <summary>
/// A generic ViewModel that holds a dictionary of values.
/// It implements INotifyPropertyChanged so the UI updates automatically when data changes.
/// </summary>
public class LuaViewModel : INotifyPropertyChanged
{
    // The storage for all data bound to the UI
    private readonly Dictionary<string, object> _data = new Dictionary<string, object>();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// This indexer allows Lua to do: vm["Key"] = Value
    /// </summary>
    public object? this[string key]
    {
        get
        {
            if (_data.TryGetValue(key, out object? value))
                return value;
            return null;
        }
        set
        {
            // Auto-wrap Lua functions into ICommands so Buttons work immediately
            if (value is LuaFunction func)
            {
                _data[key] = new LuaCommand(func);
            }
            else
            {
                _data[key] = value;
            }

            // Notify the UI that this specific key has changed
            // "Item[]" is the special name for Indexers in WPF/Noesis
            OnPropertyChanged("Item[]");

            // Also notify the specific key name, just in case specific property paths are used
            OnPropertyChanged(key);
        }
    }

    /// <summary>
    /// Helper for Lua to set multiple values at once if needed, or debug.
    /// </summary>
    public void Set(string key, object value)
    {
        this[key] = value;
    }

    /// <summary>
    /// Retrieves the internal dictionary count (for debug).
    /// </summary>
    public int Count => _data.Count;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}