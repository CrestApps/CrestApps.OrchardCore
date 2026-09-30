using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Core.Indexes;

/// <summary>
/// The index of <see cref="Models.NotInServiceNumber"/> records, looked up by number when contacts are loaded and dialed.
/// </summary>
public sealed class NotInServiceNumberIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the number, in E.164 form.
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets what found the number to be out of service.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the campaign whose attempt found the number out of service.
    /// </summary>
    public string CampaignId { get; set; }

    /// <summary>
    /// Gets or sets when the number was most recently found out of service.
    /// </summary>
    public DateTime LastDetectedUtc { get; set; }
}
