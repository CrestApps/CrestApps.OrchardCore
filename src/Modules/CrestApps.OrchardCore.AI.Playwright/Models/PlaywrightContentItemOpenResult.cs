namespace CrestApps.OrchardCore.AI.Playwright.Models;

public sealed class PlaywrightContentItemOpenResult
{
    public string RequestedTitle { get; init; }

    public string MatchedTitle { get; init; }

    public string MatchMode { get; init; }

    public bool UsedSearch { get; init; }

    public IReadOnlyList<string> ClosestTitles { get; init; } = [];

    public PlaywrightObservation Observation { get; init; }
}
