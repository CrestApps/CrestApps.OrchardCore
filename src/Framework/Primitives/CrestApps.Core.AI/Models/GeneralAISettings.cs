namespace CrestApps.Core.AI.Models;

public sealed class GeneralAISettings
{
    public bool EnableAIUsageTracking { get; set; }

    [Obsolete("Use EnableAIUsageTracking instead.")]
    public bool EnableAnalytics
    {
        get => EnableAIUsageTracking;
        set => EnableAIUsageTracking = value;
    }

    public bool EnablePreemptiveMemoryRetrieval { get; set; } = true;

    public bool OverrideMaximumIterationsPerRequest { get; set; }

    public int MaximumIterationsPerRequest { get; set; } = 10;

    public bool OverrideEnableDistributedCaching { get; set; }

    public bool EnableDistributedCaching { get; set; } = true;

    public bool OverrideEnableOpenTelemetry { get; set; }

    public bool EnableOpenTelemetry { get; set; }
}
