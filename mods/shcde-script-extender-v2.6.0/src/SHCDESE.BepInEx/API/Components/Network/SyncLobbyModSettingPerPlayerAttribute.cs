using System;

namespace SHCDESE.API.Components.Network;

/// <summary>
/// Per-Player Setting (Everyone has their own state)
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class SyncPerPlayerAttribute : Attribute { }