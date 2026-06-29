namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Contains well-known activity source identifiers.
/// </summary>
public static class ActivitySources
{
    /// <summary>
    /// The activity was created or selected manually.
    /// </summary>
    public const string Manual = "Manual";

    /// <summary>
    /// The activity was loaded for a dialer and is assigned dynamically later.
    /// </summary>
    public const string Dialer = "Dialer";

    /// <summary>
    /// The activity is driven by preview dialing.
    /// </summary>
    public const string PreviewDial = "PreviewDial";

    /// <summary>
    /// The activity is driven by power dialing.
    /// </summary>
    public const string PowerDial = "PowerDial";

    /// <summary>
    /// The activity is driven by progressive dialing.
    /// </summary>
    public const string ProgressiveDial = "ProgressiveDial";

    /// <summary>
    /// The activity is driven by predictive dialing.
    /// </summary>
    public const string PredictiveDial = "PredictiveDial";

    /// <summary>
    /// The activity is a callback request.
    /// </summary>
    public const string Callback = "Callback";

    /// <summary>
    /// The activity originated from an inbound customer contact.
    /// </summary>
    public const string Inbound = "Inbound";

    /// <summary>
    /// The activity was created by workflow automation.
    /// </summary>
    public const string Workflow = "Workflow";

    /// <summary>
    /// The activity was created by an API integration.
    /// </summary>
    public const string Api = "Api";
}
