namespace CrestApps.OrchardCore.AI.Core.Models;

public sealed class DefaultAIOptions
{
    public float Temperature = 0;

    public int MaxOutputTokens = 800;

    public float TopP = 1;

    public float FrequencyPenalty = 0;

    public float PresencePenalty = 0;

    public int PastMessagesCount = 10;

    public int? MaximumIterationsPerRequest { get; set; } = 1;

    public bool EnableOpenTelemetry { get; set; }

    public bool EnableDistributedCaching { get; set; } = true;
}
