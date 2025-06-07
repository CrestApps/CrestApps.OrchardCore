using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.Models;

public sealed class DictionaryDocument<T> : Document
{
    public Dictionary<string, T> Records { get; init; } = [];
}

