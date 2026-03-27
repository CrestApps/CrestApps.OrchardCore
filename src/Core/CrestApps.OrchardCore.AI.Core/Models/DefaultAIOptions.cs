namespace CrestApps.OrchardCore.AI.Core.Models;

public sealed class DefaultAIOptions
{
    public float? Temperature { get; set; } = 0;

    public int? MaxOutputTokens { get; set; }

    public float? TopP { get; set; } = 1;

    public float? FrequencyPenalty { get; set; } = 0;

    public float? PresencePenalty { get; set; } = 0;

    public int PastMessagesCount { get; set; } = 10;

    public int MaximumIterationsPerRequest { get; set; } = 10;

    public bool EnableOpenTelemetry { get; set; }

    public bool EnableDistributedCaching { get; set; } = true;
}
