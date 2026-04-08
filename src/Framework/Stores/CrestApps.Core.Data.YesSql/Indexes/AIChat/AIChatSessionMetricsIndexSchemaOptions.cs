namespace CrestApps.Core.Data.YesSql.Indexes.AIChat;

public sealed class AIChatSessionMetricsIndexSchemaOptions
{
    public string CollectionName { get; init; }

    public int SessionIdLength { get; init; } = 44;

    public int ProfileIdLength { get; init; } = 26;

    public int VisitorIdLength { get; init; } = 255;

    public int UserIdLength { get; init; } = 255;

    public bool CreateNamedIndexes { get; init; }
}
