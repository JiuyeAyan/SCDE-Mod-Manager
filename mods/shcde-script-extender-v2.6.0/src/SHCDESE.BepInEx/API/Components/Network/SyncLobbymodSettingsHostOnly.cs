using System;

namespace SHCDESE.API.Components.Network;

/// <summary>
/// Host-only setting. Only the host can change it; all clients receive the current value.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class SyncHostOnlyAttribute : Attribute { }