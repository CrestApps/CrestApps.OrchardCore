namespace CrestApps.OrchardCore.AI.Playwright.Models;

public sealed class PlaywrightElementMatch
{
    public string TagName { get; init; }

    public string Role { get; init; }

    public string Text { get; init; }

    public string Label { get; init; }

    public string Id { get; init; }

    public string Name { get; init; }

    public string Title { get; init; }

    public string Placeholder { get; init; }

    public string WidgetType { get; init; }

    public string SelectorHint { get; init; }
}
