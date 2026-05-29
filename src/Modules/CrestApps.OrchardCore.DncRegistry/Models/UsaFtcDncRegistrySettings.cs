namespace CrestApps.OrchardCore.DncRegistry.Models;

/// <summary>
/// Site settings for the USA FTC Do Not Call Registry integration.
/// </summary>
public sealed class UsaFtcDncRegistrySettings
{
    /// <summary>
    /// Gets or sets the encrypted API key for authenticating with the FTC DNC Registry API.
    /// </summary>
    public string ProtectedApiKey { get; set; }

    /// <summary>
    /// Gets or sets the organization ID registered with the FTC.
    /// </summary>
    public string OrganizationId { get; set; }

    /// <summary>
    /// Gets or sets the base URL for the FTC DNC Registry API.
    /// Defaults to the official endpoint if not set.
    /// </summary>
    public string BaseUrl { get; set; } = "https://telemarketing.donotcall.gov/api/";
}
