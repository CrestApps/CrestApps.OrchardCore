namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// The one-line summary of the dialer's state the live dashboard shows above its panels.
/// </summary>
public sealed class DialerPacingBanner
{
    /// <summary>
    /// Gets or sets the most serious state of any paced campaign the viewer may see.
    /// </summary>
    public DialerPacingHealthState State { get; set; }

    /// <summary>
    /// Gets or sets how urgent the banner is: <c>danger</c>, <c>warning</c>, <c>info</c>, <c>success</c> or
    /// <c>secondary</c>, matching the page's alert styles.
    /// </summary>
    public string Level { get; set; }

    /// <summary>
    /// Gets or sets the localized summary.
    /// </summary>
    public string Message { get; set; }
}
