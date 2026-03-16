namespace CrestApps.OrchardCore.AI.Playwright.Models;

public sealed class PlaywrightFieldEditResult
{
    public string Label { get; init; }

    public string RequestedFieldType { get; init; }

    public string RequestedEditMode { get; init; }

    public string ResolvedFieldType { get; init; }

    public PlaywrightObservation Observation { get; init; }
}
