using NLua;
using SHCDESE.Interop;
using SHCDESE.Logging;
using System;
using System.Collections.Generic;

namespace SHCDESE.Lua;

public static class LuaTableExtensions
{
    /// <summary>
    /// Convert a lua table to a List of type T
    /// </summary>
    /// <typeparam name="T">Expected type</typeparam>
    /// <param name="self">Lua table</param>
    /// <returns>List of type T</returns>
    public static List<T> ToList<T>(this LuaTable self)
    {
        List<T> l = new List<T>();
        foreach (object x in self.Values)
        {
            try
            {
                // handles Int64 → int, double → float, etc.
                T value = (T)Convert.ChangeType(x, typeof(T));
                l.Add(value);
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, $"Error during conversion");
            }
        }
        return l;
    }

    /// <summary>
    /// Convert a lua table to a Dictionary of type K, V
    /// </summary>
    /// <typeparam name="K">Expected key type</typeparam>
    /// <typeparam name="V">Expected value type</typeparam>
    /// <param name="self">Lua table</param>
    /// <returns>Dictionary of type K, V</returns>
    public static Dictionary<K, V> ToDictionary<K, V>(this LuaTable self)
    {
        Dictionary<K, V> d = new Dictionary<K, V>();
        foreach (KeyValuePair<object, object> kv in self)
        {
            try
            {
                K key = (K)Convert.ChangeType(kv.Key, typeof(K));
                V value = (V)Convert.ChangeType(kv.Value, typeof(V));
                d.Add(key, value);
            } 
            catch (Exception ex)
            {
                LogHelper.Error(ex, $"Error during conversion");
            }
        }
        return d;
    }

    /// <summary>
    /// Convert a lua table to a UnmanagedVector2 of T
    /// </summary>
    /// <typeparam name="T">Expected type</typeparam>
    /// <param name="self">Lua table</param>
    /// <returns>List of UnmanagedVecotr2 of T</returns>
    public static UnmanagedVector2<T> ToUnmanagedVector2<T>(this LuaTable self) where T : struct
    {
        if (self["X"] is T x && self["Y"] is T y)
        {
            return new UnmanagedVector2<T>(x, y);
        }
        return default;
    }

}
