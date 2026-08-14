using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.DncRegistry.ViewModels;

/// <summary>
/// View model for the Canada LNNTE-DNCL Registry settings.
/// </summary>
public class CanadaDnclRegistrySettingsViewModel
{
    /// <summary>
    /// Gets or sets the API key for authenticating with the LNNTE-DNCL API.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the organization account number registered with the CRTC.
    /// </summary>
    public string AccountNumber { get; set; }

    /// <summary>
    /// Gets or sets the base URL for the LNNTE-DNCL API.
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an API key has already been stored.
    /// </summary>
    [BindNever]
    public bool HasApiKey { get; set; }
}
