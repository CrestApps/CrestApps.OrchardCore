using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.OpenAI.ViewModels;

/// <summary>
/// Represents the view model for open AI connection.
/// </summary>
public class OpenAIConnectionViewModel
{
    /// <summary>
    /// Gets or sets the endpoint.
    /// </summary>
    public string Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the api key.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether has api key.
    /// </summary>
    [BindNever]
    public bool HasApiKey { get; set; }
}
