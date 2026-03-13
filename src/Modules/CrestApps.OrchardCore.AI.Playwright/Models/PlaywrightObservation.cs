namespace CrestApps.OrchardCore.AI.Playwright.Models;

public sealed class PlaywrightObservation
{
    public string CurrentUrl { get; init; }

    public string PageTitle { get; init; }

    public string MainHeading { get; init; }

    public string ToastMessage { get; init; }

    public IReadOnlyList<string> ValidationMessages { get; init; } = [];

    public IReadOnlyList<string> VisibleButtons { get; init; } = [];

    public bool IsLoginPage { get; init; }

    public bool IsAuthenticated { get; init; }
}
