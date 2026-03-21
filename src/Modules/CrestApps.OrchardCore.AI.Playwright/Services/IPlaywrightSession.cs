using CrestApps.OrchardCore.AI.Playwright.Models;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Playwright.Services;

/// <summary>
/// Represents an active Playwright browser session bound to a single chat session.
/// </summary>
public interface IPlaywrightSession
{
    /// <summary>Gets the owning chat or interaction session identifier.</summary>
    string SessionId { get; }

    /// <summary>Gets the user or owner identifier associated with this Playwright session.</summary>
    string OwnerId { get; }

    /// <summary>Gets the current lifecycle status of this session.</summary>
    PlaywrightSessionStatus Status { get; }

    /// <summary>Gets the URL the browser is currently on, or null if not yet navigated.</summary>
    string CurrentUrl { get; }

    /// <summary>Gets the current page title, if known.</summary>
    string CurrentPageTitle { get; }

    /// <summary>Gets when this session was created.</summary>
    DateTime CreatedAtUtc { get; }

    /// <summary>Gets the last time a Playwright action touched this session.</summary>
    DateTime LastActivityUtc { get; }

    /// <summary>Gets the resolved public base URL for the current tenant.</summary>
    string BaseUrl { get; }

    /// <summary>Gets the resolved Orchard admin base URL.</summary>
    string AdminBaseUrl { get; }

    /// <summary>Gets the active browser mode used by the session.</summary>
    PlaywrightBrowserMode BrowserMode { get; }

    /// <summary>Gets whether the current page is considered authenticated for Orchard admin flows.</summary>
    bool IsAuthenticated { get; }

    /// <summary>Gets the most recent structured observation captured from the page.</summary>
    PlaywrightObservation LastObservation { get; }

    /// <summary>Gets the inactivity timeout for this session in minutes.</summary>
    int SessionInactivityTimeoutInMinutes { get; }

    /// <summary>Gets the active Playwright page.</summary>
    IPage Page { get; }

    /// <summary>
    /// Returns a <see cref="CancellationToken"/> that is cancelled when either the session Stop
    /// is requested or the session is closed.
    /// </summary>
    CancellationToken StopToken { get; }

    /// <summary>Marks the session as running (tool call starting).</summary>
    void MarkRunning();

    /// <summary>Marks the session as idle (tool call finished).</summary>
    void MarkIdle();

    /// <summary>Cancels the current operation. Browser stays open.</summary>
    void Stop();
}
