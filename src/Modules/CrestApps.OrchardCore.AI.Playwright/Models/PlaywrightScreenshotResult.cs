namespace CrestApps.OrchardCore.AI.Playwright.Models;

public sealed class PlaywrightScreenshotResult
{
    public string Url { get; init; }

    public string Title { get; init; }

    public string SavedPath { get; init; }

    public bool FullPage { get; init; }

    public DateTime TakenAtUtc { get; init; }
}
