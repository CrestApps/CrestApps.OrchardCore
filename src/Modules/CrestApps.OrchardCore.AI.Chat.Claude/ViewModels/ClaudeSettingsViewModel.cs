using CrestApps.Core.AI.Claude.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.Chat.Claude.ViewModels;

/// <summary>
/// Represents the view model for claude settings.
/// </summary>
public class ClaudeSettingsViewModel
{
    /// <summary>
    /// Gets or sets the authentication type.
    /// </summary>
    public ClaudeAuthenticationType AuthenticationType { get; set; }

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
    /// Gets or sets the default model.
    /// </summary>
    public string DefaultModel { get; set; }

    /// <summary>
    /// Gets or sets the authentication types.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> AuthenticationTypes { get; set; }

    /// <summary>
    /// Gets or sets the available models.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> AvailableModels { get; set; }
}
