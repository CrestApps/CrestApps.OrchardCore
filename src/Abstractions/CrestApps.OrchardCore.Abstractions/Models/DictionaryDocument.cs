using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.Models;

/// <summary>
/// Represents the dictionary document.
/// </summary>
public sealed class DictionaryDocument<T> : Document
{
    /// <summary>
    /// Gets the records.
    /// </summary>
    public Dictionary<string, T> Records { get; init; } = [];
}
