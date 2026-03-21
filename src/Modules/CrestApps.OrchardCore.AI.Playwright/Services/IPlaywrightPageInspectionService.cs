using CrestApps.OrchardCore.AI.Playwright.Models;

namespace CrestApps.OrchardCore.AI.Playwright.Services;

public interface IPlaywrightPageInspectionService
{
    Task<PlaywrightPageContentResult> GetPageContentAsync(IPlaywrightSession session, int maxLength, CancellationToken cancellationToken = default);

    Task<PlaywrightElementSearchResult> FindElementsAsync(IPlaywrightSession session, string query, int maxMatches, CancellationToken cancellationToken = default);

    Task<PlaywrightElementSearchResult> CheckElementExistsAsync(IPlaywrightSession session, string query, int maxMatches, CancellationToken cancellationToken = default);

    Task<PlaywrightVisibleWidgetsResult> GetVisibleWidgetsAsync(IPlaywrightSession session, int maxItems, CancellationToken cancellationToken = default);

    Task<PlaywrightScreenshotResult> TakeScreenshotAsync(IPlaywrightSession session, bool fullPage, CancellationToken cancellationToken = default);
}
