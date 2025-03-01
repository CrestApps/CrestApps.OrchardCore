using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.Models;

public sealed class ModelDocument<T> : Document
    where T : SourceModel
{
    public Dictionary<string, T> Records { get; init; } = [];
}

