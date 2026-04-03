namespace CrestApps.OrchardCore.AI.ViewModels;

public class GeneralAISettingsViewModel
{
    public bool EnablePreemptiveMemoryRetrieval { get; set; }

    public bool OverrideMaximumIterationsPerRequest { get; set; }

    public int MaximumIterationsPerRequest { get; set; }

    public int AppSettingsMaximumIterationsPerRequest { get; set; }

    public int AbsoluteMaximumIterationsPerRequest { get; set; }

    public bool OverrideEnableDistributedCaching { get; set; }

    public bool EnableDistributedCaching { get; set; }

    public bool AppSettingsEnableDistributedCaching { get; set; }

    public bool OverrideEnableOpenTelemetry { get; set; }

    public bool EnableOpenTelemetry { get; set; }

    public bool AppSettingsEnableOpenTelemetry { get; set; }
}
