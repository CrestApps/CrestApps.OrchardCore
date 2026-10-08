namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// The AI assistant's summary of a conversation it handed to a live agent, as shown when completing the activity.
/// </summary>
public class OmnichannelActivityHandoffSummaryViewModel
{
    /// <summary>
    /// Gets or sets the summary.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Gets or sets when the summary was written, in the site's time zone.
    /// </summary>
    public DateTime? WrittenLocal { get; set; }
}
