using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.MailingLists.Mailchimp.ViewModels;

/// <summary>
/// The Mailchimp card of the mailing lists settings page.
/// </summary>
public class MailchimpSettingsViewModel
{
    /// <summary>
    /// Gets or sets the name of the Orchard Core text secret that holds the API key.
    /// </summary>
    public string SecretName { get; set; }

    /// <summary>
    /// Gets or sets the server prefix (data center), such as <c>us21</c>.
    /// </summary>
    public string ServerPrefix { get; set; }

    /// <summary>
    /// Gets or sets the text secrets to choose from.
    /// </summary>
    [BindNever]
    public List<SelectListItem> Secrets { get; set; } = [];

    /// <summary>
    /// Gets or sets whether a secret is named, so the connection can be tested.
    /// </summary>
    [BindNever]
    public bool IsConfigured { get; set; }
}
