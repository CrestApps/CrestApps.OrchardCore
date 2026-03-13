namespace CrestApps.OrchardCore.AI.Playwright.Models;

public sealed class PlaywrightVisibleWidgetsResult
{
    public string Url { get; init; }

    public string Title { get; init; }

    public int WidgetCount { get; init; }

    public IReadOnlyList<PlaywrightVisibleWidget> Widgets { get; init; } = [];
}
