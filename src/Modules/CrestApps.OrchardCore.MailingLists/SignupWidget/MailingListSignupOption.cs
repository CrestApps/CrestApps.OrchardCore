namespace CrestApps.OrchardCore.MailingLists.SignupWidget;

/// <summary>
/// A list a sign-up widget offers.
/// </summary>
public sealed class MailingListSignupOption
{
    /// <summary>
    /// Gets or sets the list's id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the name people see.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets what the list sends and how often.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the consent text people agree to.
    /// </summary>
    public string ConsentText { get; set; }
}
