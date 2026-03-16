namespace CrestApps.OrchardCore.AI.Playwright.Models;

public sealed class PlaywrightEditorTargetResult
{
    public string RequestedTarget { get; init; }

    public string MatchedTarget { get; init; }

    public string TargetKind { get; init; }

    public PlaywrightObservation Observation { get; init; }
}
