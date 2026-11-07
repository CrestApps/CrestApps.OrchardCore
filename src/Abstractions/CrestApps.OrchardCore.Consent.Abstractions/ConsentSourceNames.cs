namespace CrestApps.OrchardCore.Consent;

/// <summary>
/// The names of the consent sources the platform ships.
/// </summary>
public static class ConsentSourceNames
{
    /// <summary>
    /// The CRM contact's do-not-call, do-not-text and do-not-e-mail flags (followed to every record that shares the
    /// number), and the opt-outs recorded against an address no contact held.
    /// </summary>
    public const string ContactPreferences = "contact-preferences";

    /// <summary>
    /// The do-not-call lists a site keeps itself.
    /// </summary>
    public const string DoNotCallLists = "do-not-call-lists";

    /// <summary>
    /// A commerce customer's consent to marketing, which a campaign's unsubscribe link withdraws.
    /// </summary>
    public const string CustomerMarketing = "customer-marketing";
}
