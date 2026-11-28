namespace CrestApps.OrchardCore.AI.Records;

/// <summary>
/// The record kinds the enabled features registered with
/// <see cref="AIRecordServiceCollectionExtensions.AddAIRecordKind{T}"/>, by name.
/// </summary>
public sealed class AIRecordKindOptions
{
    private readonly Dictionary<string, AIRecordKind> _kinds = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the registered kinds, by name. A name is matched without regard to case.
    /// </summary>
    public IReadOnlyDictionary<string, AIRecordKind> Kinds
        => _kinds;

    /// <summary>
    /// Adds a kind, replacing one registered earlier under the same name.
    /// </summary>
    /// <param name="kind">The kind.</param>
    internal void Add(AIRecordKind kind)
        => _kinds[kind.Name] = kind;
}
