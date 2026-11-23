namespace CrestApps.OrchardCore.Consent.ViewModels;

/// <summary>
/// What the e-mail preferences page shows for one unsubscribe link.
/// </summary>
public sealed class EmailPreferencesViewModel
{
    /// <summary>
    /// Gets or sets the link's token, which the page's buttons post back.
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the link is valid: signed by this site for unsubscribing, and not expired.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the e-mail address the link was sent to.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the name of what the link stops, such as a mailing list's name, or <see langword="null"/> when it no
    /// longer exists.
    /// </summary>
    public string ScopeTitle { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the link stops all marketing e-mail, so the page offers one button.
    /// </summary>
    public bool IsAllMarketing { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the link's own scope can be stopped on its own, apart from all
    /// marketing e-mail.
    /// </summary>
    public bool CanUnsubscribeScope { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the person just stopped the link's scope.
    /// </summary>
    public bool Unsubscribed { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the person just stopped all marketing e-mail.
    /// </summary>
    public bool StoppedAllMarketing { get; set; }
}
