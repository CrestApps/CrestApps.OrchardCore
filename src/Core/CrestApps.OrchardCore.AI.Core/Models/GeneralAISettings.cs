namespace CrestApps.OrchardCore.AI.Core.Models;

public sealed class GeneralAISettings
{
    public bool EnablePreemptiveMemoryRetrieval { get; set; } = true;

    public bool OverrideMaximumIterationsPerRequest { get; set; }

    public int MaximumIterationsPerRequest { get; set; } = 10;

    public bool OverrideEnableDistributedCaching { get; set; }

    public bool EnableDistributedCaching { get; set; } = true;

    public bool OverrideEnableOpenTelemetry { get; set; }

    public bool EnableOpenTelemetry { get; set; }
}
