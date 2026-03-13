using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Playwright.Services;

public interface IPlaywrightActionVisualizer
{
    Task ShowLocatorActionAsync(
        IPage page,
        ILocator locator,
        string action,
        string target,
        CancellationToken cancellationToken = default);

    Task ShowPageActionAsync(
        IPage page,
        string action,
        string target,
        CancellationToken cancellationToken = default);
}
