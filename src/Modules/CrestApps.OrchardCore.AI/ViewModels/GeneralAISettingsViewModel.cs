namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents the view model for general AI settings.
/// </summary>
public class GeneralAISettingsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether enable AI usage tracking.
    /// </summary>
    public bool EnableAIUsageTracking { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether enable preemptive memory retrieval.
    /// </summary>
    public bool EnablePreemptiveMemoryRetrieval { get; set; }

    /// <summary>
    /// Gets or sets the maximum iterations per request.
    /// </summary>
    public int MaximumIterationsPerRequest { get; set; }

    /// <summary>
    /// Gets or sets the absolute maximum iterations per request.
    /// </summary>
    public int AbsoluteMaximumIterationsPerRequest { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether enable distributed caching.
    /// </summary>
    public bool EnableDistributedCaching { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether enable open telemetry.
    /// </summary>
    public bool EnableOpenTelemetry { get; set; }
}
