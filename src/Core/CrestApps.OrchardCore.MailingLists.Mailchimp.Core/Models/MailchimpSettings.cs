namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Models;

/// <summary>
/// The tenant's connection to Mailchimp, kept in the site settings and edited on the mailing lists settings page. The
/// API key itself is never here: it is an Orchard Core text secret this names.
/// </summary>
public sealed class MailchimpSettings
{
    /// <summary>
    /// Gets or sets the name of the Orchard Core text secret that holds the Mailchimp API key.
    /// </summary>
    public string SecretName { get; set; }

    /// <summary>
    /// Gets or sets the Mailchimp server prefix (data center), such as <c>us21</c>. Empty takes it from the API key's
    /// suffix, and without one uses <c>api.mailchimp.com</c>.
    /// </summary>
    public string ServerPrefix { get; set; }

    /// <summary>
    /// Returns whether a secret is named.
    /// </summary>
    public bool IsConfigured()
        => !string.IsNullOrWhiteSpace(SecretName);
}
