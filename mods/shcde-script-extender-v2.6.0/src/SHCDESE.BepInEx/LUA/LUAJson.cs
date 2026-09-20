using NLua;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SHCDESE.Lua;

/// <summary>
/// Provides static utility methods for converting between NLua.LuaTable objects and JSON strings.
/// This class acts as the serialization engine for the persistent data API.
/// </summary>
/// <remarks>
/// Originally adapted from: https://gitlab.com/Rawra/sw3se-re-project/-/blob/main/src/sw3se-netcore/Core/LUA/LUAJsonAPI.cs?ref_type=heads
/// </remarks>
public sealed class LuaJson
{
    /// <summary>
    /// Converts a JSON string into a new Lua table.
    /// </summary>
    /// <param name="lua">The active Lua state, which is required to create the new table.</param>
    /// <param name="json">The JSON string to parse.</param>
    /// <returns>A new LuaTable representing the JSON structure.</returns>
    public static LuaTable JsonToLuaTable(NLua.Lua lua, string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        return (LuaTable)ConvertJsonElement(lua, doc.RootElement);
    }

    /// <summary>
    /// Converts a Lua table into a formatted (indented) JSON string.
    /// </summary>
    /// <param name="table">The Lua table to serialize.</param>
    /// <returns>A string containing the JSON representation of the table.</returns>
    public static string LuaTableToJson(LuaTable table)
    {
        object obj = ConvertLuaValue(table);
        return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
    }

    private static object ConvertJsonElement(NLua.Lua lua, JsonElement elem)
    {
        switch (elem.ValueKind)
        {
            case JsonValueKind.Object:
                {
                    LuaTable tbl = CreateTempTable(lua);
                    foreach (JsonProperty kv in elem.EnumerateObject())
                        tbl[kv.Name] = ConvertJsonElement(lua, kv.Value);
                    return tbl;
                }

            case JsonValueKind.Array:
                {
                    LuaTable arrTbl = CreateTempTable(lua);
                    int index = 1;
                    foreach (JsonElement v in elem.EnumerateArray())
                        arrTbl[index++] = ConvertJsonElement(lua, v);
                    return arrTbl;
                }

            case JsonValueKind.String:
                return elem.GetString();
            case JsonValueKind.Number:
                return elem.TryGetInt64(out long i) ? i : elem.GetDouble();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
                return null;
            default:
                return null;
        }
    }

    private static object ConvertLuaValue(object val)
    {
        if (val is LuaTable table)
        {
            // Get all keys from the table. The keys are of type object.
            List<object> keys = table.Keys.Cast<object>().ToList();

            // Attempt to treat the table as a Lua-style array (1-based, sequential integer keys).
            // We do this by checking if all keys are of type double.
            List<double> numericKeys = keys.OfType<double>().OrderBy(k => k).ToList();

            // An array must have the same number of numeric keys as total keys,
            // and the keys must be a sequence from 1 to N.
            bool isArray = numericKeys.Count > 0 && numericKeys.Count == keys.Count &&
                           numericKeys.Select((k, i) => k == i + 1).All(b => b);

            if (isArray)
            {
                List<object> list = new List<object>();
                // Iterate through the sorted numeric keys to preserve array order.
                foreach (double key in numericKeys)
                {
                    list.Add(ConvertLuaValue(table[key]));
                }
                return list;
            }
            else
            {
                // If it not an array, treat it as a dictionary (JSON object).
                Dictionary<string, object> dict = new Dictionary<string, object>();

                // Iterate through the raw keys.
                foreach (object key in keys)
                {
                    // For each key, use the indexer to get the value and recursively convert it.
                    dict[key.ToString()] = ConvertLuaValue(table[key]);
                }
                return dict;
            }
        }

        return val;
    }


    /// <summary>
    /// Creates an anonymous temporary Lua table and returns it.
    /// </summary>
    private static LuaTable CreateTempTable(NLua.Lua lua)
    {
        string tmpName = "__tmp_" + Guid.NewGuid().ToString("N");
        lua.NewTable(tmpName);
        LuaTable table = (LuaTable)lua[tmpName]!;
        lua[tmpName] = null; // cleanup global reference
        return table;
    }
}
