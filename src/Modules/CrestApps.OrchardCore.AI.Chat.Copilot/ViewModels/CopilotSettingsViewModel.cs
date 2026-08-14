using CrestApps.Core.AI.Copilot.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.ViewModels;

/// <summary>
/// Represents the view model for copilot settings.
/// </summary>
public class CopilotSettingsViewModel
{
    /// <summary>
    /// Gets or sets the authentication type.
    /// </summary>
    public CopilotAuthenticationType AuthenticationType { get; set; }

    /// <summary>
    /// Gets or sets the client id.
    /// </summary>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the client secret.
    /// </summary>
    public string ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether has secret.
    /// </summary>
    public bool HasSecret { get; set; }

    /// <summary>
    /// The auto-computed callback URL to display to the user (read-only).
    /// </summary>
    [BindNever]
    public string ComputedCallbackUrl { get; set; }

    /// <summary>
    /// Gets or sets the provider type.
    /// </summary>
    public string ProviderType { get; set; }

    /// <summary>
    /// Gets or sets the base url.
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the api key.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether has api key.
    /// </summary>
    public bool HasApiKey { get; set; }

    /// <summary>
    /// Gets or sets the wire api.
    /// </summary>
    public string WireApi { get; set; }

    /// <summary>
    /// Gets or sets the default model.
    /// </summary>
    public string DefaultModel { get; set; }

    /// <summary>
    /// Gets or sets the azure api version.
    /// </summary>
    public string AzureApiVersion { get; set; }

    /// <summary>
    /// Gets or sets the authentication types.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> AuthenticationTypes { get; set; }

    /// <summary>
    /// Gets or sets the provider types.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> ProviderTypes { get; set; }

    /// <summary>
    /// Gets or sets the wire api options.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> WireApiOptions { get; set; }
}
