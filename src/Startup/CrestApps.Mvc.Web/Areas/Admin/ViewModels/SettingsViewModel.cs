namespace CrestApps.Mvc.Web.Areas.Admin.ViewModels;

public sealed class SettingsViewModel
{
    public bool EnablePreemptiveMemoryRetrieval { get; set; } = true;

    public int MaximumIterationsPerRequest { get; set; } = 10;

    public bool EnableDistributedCaching { get; set; } = true;

    public bool EnableOpenTelemetry { get; set; }
}
