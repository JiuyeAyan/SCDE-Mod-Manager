using System;

namespace SHCDESE.API.Components.ModManager;

/// <summary>
/// Local setting. Saved to this player's settings file and restored on the next launch,
/// but never sent over the network: local enable switches, UI preferences, selected profile
/// indices, and anything else that is nobody else's business.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class PersistLocalAttribute : Attribute { }