namespace CrestApps.OrchardCore.AI.ViewModels;

public class GeneralAISettingsViewModel
{
    public bool EnablePreemptiveMemoryRetrieval { get; set; }

    public int MaximumIterationsPerRequest { get; set; }

    public int AbsoluteMaximumIterationsPerRequest { get; set; }

    public bool EnableDistributedCaching { get; set; }

    public bool EnableOpenTelemetry { get; set; }
}
