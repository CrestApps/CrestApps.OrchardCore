namespace CrestApps.OrchardCore.AI.Playwright.Models;

public sealed class PlaywrightContentListItem
{
    public string Title { get; init; }

    public string ContentType { get; init; }

    public string Status { get; init; }

    public bool CanEdit { get; init; }
}
