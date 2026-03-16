using CrestApps.OrchardCore.AI.Playwright.Models;

namespace CrestApps.OrchardCore.AI.Playwright.Services;

public interface IOrchardEvidenceService
{
    /// <summary>
    /// Attempts to find a named OrchardCore admin action (e.g. Edit, Publish Now, Save Draft, Preview)
    /// using multiple locator strategies in priority order.
    /// <para>
    /// On success, returns <see cref="PlaywrightElementDiagnosticsResult.Found"/> = true with the
    /// matched locator strategy name. No evidence files are written.
    /// </para>
    /// <para>
    /// On failure, captures a full-page screenshot, a container screenshot when possible, and the
    /// raw page HTML so the caller can diagnose whether the action is truly absent, hidden, renamed,
    /// or blocked by an overlay.
    /// </para>
    /// </summary>
    Task<PlaywrightElementDiagnosticsResult> FindOrchardElementWithEvidenceAsync(IPlaywrightSession session, string actionLabel, CancellationToken cancellationToken = default);
}
