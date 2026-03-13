namespace CrestApps.OrchardCore.AI.Playwright.Models;

/// <summary>
/// Shared Playwright session metadata that can be attached to either an AI profile
/// or a chat interaction via EntityExtensions.Put/As.
/// </summary>
public sealed class PlaywrightSessionMetadata
{
    public bool Enabled { get; set; }

    public PlaywrightBrowserMode BrowserMode { get; set; } = PlaywrightBrowserMode.PersistentContext;

    public string Username { get; set; }

    public string ProtectedPassword { get; set; }

    public string BaseUrl { get; set; }

    public string AdminBaseUrl { get; set; }

    public string CdpEndpoint { get; set; }

    public string PersistentProfilePath { get; set; }

    public bool Headless { get; set; }

    public bool PublishByDefault { get; set; }
}
