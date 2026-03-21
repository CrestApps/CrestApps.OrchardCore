using CrestApps.OrchardCore.AI.Playwright.Services;

namespace CrestApps.OrchardCore.AI.Playwright.Models;

/// <summary>Status payload returned by the session API endpoints.</summary>
public sealed class PlaywrightStatusResponse
{
    public string ChatSessionId { get; init; }

    public string Status { get; init; }

    public string CurrentUrl { get; init; }

    public string CurrentPageTitle { get; init; }

    public string BrowserMode { get; init; }

    public bool IsActive { get; init; }

    public bool IsAuthenticated { get; init; }

    public static PlaywrightStatusResponse FromSession(string chatSessionId, IPlaywrightSession session)
    {
        return new PlaywrightStatusResponse
        {
            ChatSessionId = chatSessionId,
            Status = session.Status.ToString(),
            CurrentUrl = session.CurrentUrl,
            CurrentPageTitle = session.CurrentPageTitle,
            BrowserMode = "Dedicated browser",
            IsActive = session.Status != PlaywrightSessionStatus.Closed,
            IsAuthenticated = session.IsAuthenticated,
        };
    }

    public static PlaywrightStatusResponse Inactive(string chatSessionId) => new()
    {
        ChatSessionId = chatSessionId,
        Status = PlaywrightSessionStatus.Closed.ToString(),
        IsActive = false,
    };
}
