using CrestApps.OrchardCore.Plaid.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Plaid.ViewModels;

/// <summary>
/// The Plaid settings editor.
/// </summary>
public class PlaidSettingsViewModel
{
    /// <summary>
    /// Gets or sets the environment.
    /// </summary>
    public PlaidEnvironment Environment { get; set; }

    /// <summary>
    /// Gets or sets the client id.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the name of the Orchard secret that holds the Plaid secret.
    /// </summary>
    public string SecretName { get; set; }

    /// <summary>
    /// Gets or sets the environments to choose from.
    /// </summary>
    [BindNever]
    public List<SelectListItem> Environments { get; set; } = [];

    /// <summary>
    /// Gets or sets the text secrets to choose from.
    /// </summary>
    [BindNever]
    public List<SelectListItem> Secrets { get; set; } = [];

    /// <summary>
    /// Gets or sets this tenant's webhook URL, to enter in the Plaid dashboard when asked.
    /// </summary>
    [BindNever]
    public string WebhookUrl { get; set; }

    /// <summary>
    /// Gets or sets whether a client id and a secret are set.
    /// </summary>
    [BindNever]
    public bool IsConfigured { get; set; }
}
