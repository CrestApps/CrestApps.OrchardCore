namespace CrestApps.OrchardCore.DncRegistry.Models;

/// <summary>
/// Site settings for the Canada LNNTE-DNCL Registry integration.
/// </summary>
public sealed class CanadaDnclRegistrySettings
{
    /// <summary>
    /// Gets or sets the encrypted API key for authenticating with the LNNTE-DNCL API.
    /// </summary>
    public string ProtectedApiKey { get; set; }

    /// <summary>
    /// Gets or sets the organization account number registered with the CRTC.
    /// </summary>
    public string AccountNumber { get; set; }

    /// <summary>
    /// Gets or sets the base URL for the LNNTE-DNCL API.
    /// Defaults to the official endpoint if not set.
    /// </summary>
    public string BaseUrl { get; set; } = "https://www.lnnte-dncl.gc.ca/api/";
}
