using System;

namespace SHCDESE.API.Components.ModManager;

/// <summary>
/// Transient setting. Opts a synchronised property back out of storage, so it is routed over the
/// network exactly as its sync attribute declares but is never written to disk. Use it for values
/// that only make sense for the lifetime of one session, and for the companion arrays of
/// per-player properties, which hold other players' values.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class DoNotPersistAttribute : Attribute { }