namespace CrestApps.OrchardCore.AI.Playwright.Models;

public sealed class PlaywrightPageContentResult
{
    public string Url { get; init; }

    public string Title { get; init; }

    public string MainHeading { get; init; }

    public string Content { get; init; }
}
