using SHCDESE.EventAPI.AI;
using SHCDESE.Lua.DocsGen;

namespace SHCDESE.EventAPI;

/// <summary>
/// Provides static R3EventHook fields for subscribing to ai-specific things.
/// </summary>
public static class AIR3EventHooks
{
    /// <summary>
    /// Fired when the game processes a custom lord.
    /// </summary>
    public static readonly R3EventHook<AIProcessCustomLordEventArgs> OnAIProcessCustomLord = new();

    /// <summary>
    /// Fired when the game ai attempts to seek a place to rally their troops to during a siege, before letting
    /// them storm the castle.
    /// </summary>
    [LuaApiExport("OnAISelectSiegeRallypoint")]
    public static readonly R3EventHook<AISelectSiegeRallypointEventArgs> OnAISelectSiegeRallypoint = new();

    /// <summary>
    /// Fired when the game ai attempts to build a wall.
    /// </summary>
    [LuaApiExport("OnAIBuildWall")]
    public static readonly R3EventHook<AIBuildWallEventArgs> OnAIBuildWall = new();

    /// <summary>
    /// Fired when the game ai checks if it should build a new hovel or not
    /// <para>Default behavior:</para>
    /// <para>
    /// The AI will block hovel construction when the player already has more than
    /// 12 civilian housing spaces and at least one of the following conditions is true:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>Total population is below available housing capacity.</description>
    /// </item>
    /// <item>
    /// <description>Current idle peasant count is 5 or greater.</description>
    /// </item>
    /// <item>
    /// <description>Average idle peasant count is 5 or greater.</description>
    /// </item>
    /// <item>
    /// <description>Current popularity is below 5000.</description>
    /// </item>
    /// </list>
    /// <para>
    /// Returns <c>true</c> only for <c>MAPPER_HOVEL</c>; all other building types
    /// return <c>false</c>.
    /// </para>
    /// </summary>
    [LuaApiExport("OnAIQueryBuildHovel")]
    public static readonly R3EventHook<AIQueryBuildHovelEventArgs> OnAIQueryBuildHovelEventArgs = new();
}