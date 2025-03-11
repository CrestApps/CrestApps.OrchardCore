using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.Models;

public sealed class ModelDocument<T> : Document
{
    public Dictionary<string, T> Records { get; init; } = [];
}

