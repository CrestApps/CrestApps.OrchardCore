namespace CrestApps.OrchardCore.ContactCenter.Reports.Models;

/// <summary>
/// A dialer profile carried by pending activities, with how many carry it.
/// </summary>
public sealed class DialerInventoryProfileCount
{
    /// <summary>
    /// Gets or sets the profile identifier, or <see langword="null"/> for activities that carry no profile.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets or sets the profile name, or <see langword="null"/> when the activities carry no profile or it no longer
    /// exists.
    /// </summary>
    public string ProfileName { get; set; }

    /// <summary>
    /// Gets or sets the number of pending activities that carry the profile.
    /// </summary>
    public int Count { get; set; }
}
