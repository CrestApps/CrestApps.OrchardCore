namespace CrestApps.OrchardCore.AI.Playwright.Models;

public sealed class PlaywrightContentListResult
{
    public string Url { get; init; }

    public string Title { get; init; }

    public string MainHeading { get; init; }

    public int ItemCount { get; init; }

    public IReadOnlyList<PlaywrightContentListItem> Items { get; init; } = [];
}
