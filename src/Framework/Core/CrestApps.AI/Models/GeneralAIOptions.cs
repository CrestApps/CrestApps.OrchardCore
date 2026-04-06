namespace CrestApps.AI.Models;

public sealed class GeneralAIOptions
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

    public GeneralAIOptions Clone()
        => new()
        {
            EnableAIUsageTracking = EnableAIUsageTracking,
            EnablePreemptiveMemoryRetrieval = EnablePreemptiveMemoryRetrieval,
            OverrideMaximumIterationsPerRequest = OverrideMaximumIterationsPerRequest,
            MaximumIterationsPerRequest = MaximumIterationsPerRequest,
            OverrideEnableDistributedCaching = OverrideEnableDistributedCaching,
            EnableDistributedCaching = EnableDistributedCaching,
            OverrideEnableOpenTelemetry = OverrideEnableOpenTelemetry,
            EnableOpenTelemetry = EnableOpenTelemetry,
        };

    public static GeneralAIOptions FromSettings(GeneralAISettings settings)
        => settings == null
            ? new GeneralAIOptions()
            : new GeneralAIOptions
            {
                EnableAIUsageTracking = settings.EnableAIUsageTracking,
                EnablePreemptiveMemoryRetrieval = settings.EnablePreemptiveMemoryRetrieval,
                OverrideMaximumIterationsPerRequest = settings.OverrideMaximumIterationsPerRequest,
                MaximumIterationsPerRequest = settings.MaximumIterationsPerRequest,
                OverrideEnableDistributedCaching = settings.OverrideEnableDistributedCaching,
                EnableDistributedCaching = settings.EnableDistributedCaching,
                OverrideEnableOpenTelemetry = settings.OverrideEnableOpenTelemetry,
                EnableOpenTelemetry = settings.EnableOpenTelemetry,
            };
}
