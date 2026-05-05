using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Context passed to <see cref="Services.ICampaignActionExecutor"/> when processing campaign actions.
/// </summary>
public sealed class CampaignActionExecutionContext
{
    /// <summary>
    /// Gets or sets the activity being completed.
    /// </summary>
    public OmnichannelActivity Activity { get; set; }

    /// <summary>
    /// Gets or sets the contact content item.
    /// </summary>
    public ContentItem Contact { get; set; }

    /// <summary>
    /// Gets or sets the subject content item.
    /// </summary>
    public ContentItem Subject { get; set; }

    /// <summary>
    /// Gets or sets the selected disposition.
    /// </summary>
    public OmnichannelDisposition Disposition { get; set; }

    /// <summary>
    /// Gets or sets the schedule dates provided by the user during completion.
    /// Key is the campaign action ItemId, value is the schedule date.
    /// </summary>
    public IDictionary<string, DateTime?> ActionScheduleDates { get; set; }
}
