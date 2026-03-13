namespace CrestApps.OrchardCore.AI.Playwright.Models;

public sealed class PlaywrightElementSearchResult
{
    public string Query { get; init; }

    public string Url { get; init; }

    public string Title { get; init; }

    public bool Exists { get; init; }

    public int MatchCount { get; init; }

    public IReadOnlyList<PlaywrightElementMatch> Matches { get; init; } = [];
}
