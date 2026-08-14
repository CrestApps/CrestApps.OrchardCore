using CrestApps.Core.Azure.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.OpenAI.Azure.ViewModels;

/// <summary>
/// Represents the view model for azure open AI connection.
/// </summary>
public class AzureOpenAIConnectionViewModel
{
    /// <summary>
    /// Gets or sets the authentication type.
    /// </summary>
    public AzureAuthenticationType AuthenticationType { get; set; }

    /// <summary>
    /// Gets or sets the endpoint.
    /// </summary>
    public string Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the api key.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the identity id.
    /// </summary>
    public string IdentityId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether has api key.
    /// </summary>
    [BindNever]
    public bool HasApiKey { get; set; }

    /// <summary>
    /// Gets or sets the authentication types.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> AuthenticationTypes { get; set; }
}
