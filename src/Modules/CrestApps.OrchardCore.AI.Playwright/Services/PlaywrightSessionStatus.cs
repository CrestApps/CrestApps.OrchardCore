namespace CrestApps.OrchardCore.AI.Playwright.Services;

public enum PlaywrightSessionStatus
{
    /// <summary>Browser is open but no tool call is currently in flight.</summary>
    Idle,

    /// <summary>A tool call is currently executing inside the browser.</summary>
    Running,

    /// <summary>The user pressed Stop; the current operation was cancelled. Browser is still open.</summary>
    Stopped,

    /// <summary>The session has been disposed and the browser window is closed.</summary>
    Closed,
}
