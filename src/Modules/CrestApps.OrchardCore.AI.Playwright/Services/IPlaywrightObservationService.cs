using CrestApps.OrchardCore.AI.Playwright.Models;

namespace CrestApps.OrchardCore.AI.Playwright.Services;

public interface IPlaywrightObservationService
{
    Task<PlaywrightObservation> CaptureAsync(IPlaywrightSession session, CancellationToken cancellationToken = default);
}
