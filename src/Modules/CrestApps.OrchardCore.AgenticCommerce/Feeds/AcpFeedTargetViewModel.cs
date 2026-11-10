using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

public class AcpFeedTargetViewModel
{
    /// <summary>
    /// Gets or sets the target's id, or none for a new one.
    /// </summary>
    public string Id { get; set; }

    public string Name { get; set; }

    public string BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets a new bearer key, or none to keep the one stored.
    /// </summary>
    public string BearerToken { get; set; }

    public string FeedId { get; set; }

    public string TargetCountry { get; set; }

    public string Currency { get; set; }

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to remove the target.
    /// </summary>
    public bool Remove { get; set; }

    [BindNever]
    public bool HasBearerToken { get; set; }
}
