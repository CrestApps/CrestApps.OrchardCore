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

    Task<PlaywrightEditorTargetResult> OpenEditorTabAsync(
        IPlaywrightSession session,
        string tabName,
        bool exact = false,
        CancellationToken cancellationToken = default);

    Task<PlaywrightObservation> SetContentTitleAsync(IPlaywrightSession session, string title, CancellationToken cancellationToken = default);

    Task<PlaywrightFieldEditResult> SetFieldValueAsync(
        IPlaywrightSession session,
        string label,
        string value,
        string fieldType = "auto",
        bool exact = false,
        CancellationToken cancellationToken = default);

    Task<PlaywrightFieldEditResult> SetBodyFieldAsync(
        IPlaywrightSession session,
        string label,
        string value,
        string writeMode = "append",
        bool exact = false,
        CancellationToken cancellationToken = default);

    Task<PlaywrightObservation> SaveDraftAsync(IPlaywrightSession session, CancellationToken cancellationToken = default);

    Task<PlaywrightObservation> PublishContentAsync(IPlaywrightSession session, CancellationToken cancellationToken = default);

    Task<PlaywrightPublishVerificationResult> PublishAndVerifyAsync(
        IPlaywrightSession session,
        string expectedStatus = "Published",
        CancellationToken cancellationToken = default);
}
