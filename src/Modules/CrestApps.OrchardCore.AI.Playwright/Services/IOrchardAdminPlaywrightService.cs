using CrestApps.OrchardCore.AI.Playwright.Models;

namespace CrestApps.OrchardCore.AI.Playwright.Services;

public interface IOrchardAdminPlaywrightService
{
    Task<PlaywrightObservation> CaptureStateAsync(IPlaywrightSession session, CancellationToken cancellationToken = default);

    Task<PlaywrightObservation> OpenAdminHomeAsync(IPlaywrightSession session, CancellationToken cancellationToken = default);

    Task<PlaywrightObservation> OpenContentItemsAsync(IPlaywrightSession session, CancellationToken cancellationToken = default);

    Task<PlaywrightContentListResult> ListVisibleContentItemsAsync(IPlaywrightSession session, int maxItems = 20, CancellationToken cancellationToken = default);

    Task<PlaywrightObservation> OpenNewContentItemAsync(IPlaywrightSession session, string contentType, CancellationToken cancellationToken = default);

    Task<PlaywrightContentItemOpenResult> OpenContentItemEditorAsync(IPlaywrightSession session, string title, CancellationToken cancellationToken = default);

    Task<PlaywrightObservation> SetContentTitleAsync(IPlaywrightSession session, string title, CancellationToken cancellationToken = default);

    Task<PlaywrightObservation> SaveDraftAsync(IPlaywrightSession session, CancellationToken cancellationToken = default);

    Task<PlaywrightObservation> PublishContentAsync(IPlaywrightSession session, CancellationToken cancellationToken = default);

    Task<PlaywrightObservation> ClickByRoleAsync(
        IPlaywrightSession session,
        string role,
        string name,
        bool exact = false,
        CancellationToken cancellationToken = default);

    Task<PlaywrightObservation> FillByLabelAsync(
        IPlaywrightSession session,
        string label,
        string value,
        bool exact = false,
        CancellationToken cancellationToken = default);

    Task<PlaywrightObservation> WaitForUrlAsync(
        IPlaywrightSession session,
        string urlPattern,
        int timeoutMs = 15000,
        CancellationToken cancellationToken = default);
}
