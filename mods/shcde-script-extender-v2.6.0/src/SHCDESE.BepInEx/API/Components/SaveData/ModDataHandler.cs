using System;

namespace SHCDESE.API.Components.SaveData;

/// <summary>
/// Represents a registered mod data handler.
/// </summary>
internal class ModDataHandler
{
    public required string ModIdentifier { get; init; }
    public required Func<SaveContext, byte[]?> SaveCallback { get; init; }
    public required Action<byte[], LoadContext> LoadCallback { get; init; }
    public Action? OnUnloadCallback { get; init; }
}