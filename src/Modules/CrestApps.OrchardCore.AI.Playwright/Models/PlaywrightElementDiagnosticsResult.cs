namespace CrestApps.OrchardCore.AI.Playwright.Models;

public sealed class PlaywrightElementDiagnosticsResult
{
    /// <summary>
    /// True when at least one locator strategy matched a visible element.
    /// </summary>
    public bool Found { get; init; }

    /// <summary>
    /// Name of the locator strategy that matched, e.g. "role:link[Edit]".
    /// Null when <see cref="Found"/> is false.
    /// </summary>
    public string MatchedLocator { get; init; }

    /// <summary>Current page URL at the time of the search.</summary>
    public string Url { get; init; }

    /// <summary>Current page title at the time of the search.</summary>
    public string Title { get; init; }

    /// <summary>
    /// Full-page screenshot path. Populated only on failure to help diagnose the missing action.
    /// </summary>
    public string PageScreenshotPath { get; init; }

    /// <summary>
    /// Screenshot of the nearest relevant OrchardCore container found on the page.
    /// Populated only on failure when a candidate container is located.
    /// </summary>
    public string ContainerScreenshotPath { get; init; }

    /// <summary>
    /// Path to the saved raw page HTML. Populated only on failure.
    /// </summary>
    public string PageHtmlPath { get; init; }

    /// <summary>
    /// Ordered list of locator strategy descriptions that were attempted.
    /// Always populated so the AI can report exactly what was tried.
    /// </summary>
    public IReadOnlyList<string> Attempts { get; init; } = [];
}
