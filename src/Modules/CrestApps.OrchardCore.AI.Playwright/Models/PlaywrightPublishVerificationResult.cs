namespace CrestApps.OrchardCore.AI.Playwright.Models;

public sealed class PlaywrightPublishVerificationResult
{
    public string Action { get; init; }

    public string ExpectedStatus { get; init; }

    public bool Verified { get; init; }

    public IReadOnlyList<string> VerificationSignals { get; init; } = [];

    public PlaywrightObservation Observation { get; init; }
}
