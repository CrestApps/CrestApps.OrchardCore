using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.DncRegistry.ViewModels;

/// <summary>
/// View model for the USA FTC DNC Registry settings.
/// </summary>
public class UsaFtcDncRegistrySettingsViewModel
{
    /// <summary>
    /// Gets or sets the API key for authenticating with the FTC DNC Registry API.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the organization ID registered with the FTC.
    /// </summary>
    public string OrganizationId { get; set; }

    /// <summary>
    /// Gets or sets the base URL for the FTC DNC Registry API.
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an API key has already been stored.
    /// </summary>
    [BindNever]
    public bool HasApiKey { get; set; }
}
