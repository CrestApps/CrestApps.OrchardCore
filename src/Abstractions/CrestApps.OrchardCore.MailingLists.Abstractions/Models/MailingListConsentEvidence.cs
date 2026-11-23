namespace CrestApps.OrchardCore.MailingLists.Models;

/// <summary>
/// What a person agreed to when they joined a mailing list, kept as proof of consent.
/// </summary>
public sealed class MailingListConsentEvidence
{
    /// <summary>
    /// Gets or sets the consent text the form showed.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets where the person agreed, such as the sign-up widget's content item id or the page URL.
    /// </summary>
    public string Form { get; set; }

    /// <summary>
    /// Gets or sets when the person agreed (UTC).
    /// </summary>
    public DateTime AgreedUtc { get; set; }

    /// <summary>
    /// Gets or sets the address the request came from.
    /// </summary>
    public string ClientAddress { get; set; }
}
