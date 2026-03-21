namespace CrestApps.OrchardCore.AI.Playwright.Models;

public sealed class PlaywrightSessionRequest
{
    public string ChatSessionId { get; init; }

    public string OwnerId { get; init; }

    public string ResourceItemId { get; init; }

    public string BaseUrl { get; init; }

    public string AdminBaseUrl { get; init; }

    public PlaywrightBrowserMode BrowserMode { get; init; }

    public string Username { get; init; }

    public string Password { get; init; }

    public bool CanAttemptLogin { get; init; }

    public string CdpEndpoint { get; init; }

    public string PersistentProfilePath { get; init; }

    public bool Headless { get; init; }

    public bool PublishByDefault { get; init; }

    public int SessionInactivityTimeoutInMinutes { get; init; } = PlaywrightConstants.DefaultSessionInactivityTimeoutInMinutes;
}
