using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Describes who or what handles an activity — or, once completed, who dispositioned it — for the badges shown on
/// each activity row.
/// </summary>
/// <remarks>
/// Not sealed: the display driver builds it through <c>Initialize&lt;T&gt;</c>, which derives a shape type from it.
/// </remarks>
public class ActivityHandlerViewModel
{
    /// <summary>
    /// Gets or sets the activity being described.
    /// </summary>
    public OmnichannelActivity Activity { get; set; }

    /// <summary>
    /// Gets or sets the localized interaction type, such as "Automated (AI)" or "Manual".
    /// </summary>
    public string InteractionTypeName { get; set; }

    /// <summary>
    /// Gets or sets the localized activity source, such as "Automatic", "Preview dialer" or "Callback".
    /// </summary>
    public string SourceName { get; set; }

    /// <summary>
    /// Gets or sets the name of the AI profile whose agent will handle the automated activity.
    /// </summary>
    public string AIProfileName { get; set; }

    /// <summary>
    /// Gets or sets the name of the dialer profile that will dial the activity.
    /// </summary>
    public string DialerProfileName { get; set; }

    /// <summary>
    /// Gets or sets the name of the campaign the activity belongs to.
    /// </summary>
    public string CampaignName { get; set; }

    /// <summary>
    /// Gets or sets the display name of the user the activity is assigned to.
    /// </summary>
    public string AssignedToName { get; set; }

    /// <summary>
    /// Gets or sets who or what dispositioned the completed activity.
    /// </summary>
    public ActivityDispositionActor DispositionedBy { get; set; }

    /// <summary>
    /// Gets or sets the localized description of who or what dispositioned the completed activity, such as a
    /// user's display name, "AI voice agent (profile: Sales)" or "Dialer (automatic)".
    /// </summary>
    public string DispositionedByName { get; set; }
}
