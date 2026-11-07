namespace CrestApps.OrchardCore.Consent;

/// <summary>
/// The kinds of record a consent question or change can name, so each consent source answers only for its own records.
/// The list is open: a module with its own contact records adds a kind.
/// </summary>
public static class ConsentContactKinds
{
    /// <summary>
    /// A CRM contact, by its content item id.
    /// </summary>
    public const string Contact = "Contact";

    /// <summary>
    /// A commerce customer, by its id, whose consent is to marketing.
    /// </summary>
    public const string Customer = "Customer";
}
