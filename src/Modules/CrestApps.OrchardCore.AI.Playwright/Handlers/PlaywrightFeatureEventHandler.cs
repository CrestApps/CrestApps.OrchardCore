using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;

// IFeatureInfo lives in OrchardCore.Environment.Extensions.Features.
// IFeatureEventHandler (the one registered in DI) lives in OrchardCore.Environment.Shell.

namespace CrestApps.OrchardCore.AI.Playwright.Handlers;

/// <summary>
/// Installs Playwright browser binaries the first time the feature is enabled.
/// This ensures Chromium is available before any session is created.
/// </summary>
public sealed class PlaywrightFeatureEventHandler : IFeatureEventHandler
{
    private readonly ILogger<PlaywrightFeatureEventHandler> _logger;

    public PlaywrightFeatureEventHandler(ILogger<PlaywrightFeatureEventHandler> logger)
    {
        _logger = logger;
    }

    public Task InstallingAsync(IFeatureInfo feature) => Task.CompletedTask;
    public Task InstalledAsync(IFeatureInfo feature) => Task.CompletedTask;
    public Task EnablingAsync(IFeatureInfo feature) => Task.CompletedTask;

    public Task EnabledAsync(IFeatureInfo feature)
    {
        if (!feature.Id.Equals(PlaywrightConstants.Feature.AdminWidget, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        return InstallBrowsersAsync();
    }

    public Task DisablingAsync(IFeatureInfo feature) => Task.CompletedTask;
    public Task DisabledAsync(IFeatureInfo feature) => Task.CompletedTask;
    public Task UninstallingAsync(IFeatureInfo feature) => Task.CompletedTask;
    public Task UninstalledAsync(IFeatureInfo feature) => Task.CompletedTask;

    private Task InstallBrowsersAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                _logger.LogDebug("Installing Playwright browser binaries (chromium)...");

                var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);

                if (exitCode != 0)
                {
                    _logger.LogDebug(
                        "Playwright browser installation exited with code {ExitCode}. " +
                        "You may need to run 'playwright install chromium' manually.", exitCode);
                }
                else
                {
                    _logger.LogDebug("Playwright browser binaries installed successfully.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex,
                    "Failed to install Playwright browser binaries. " +
                    "Run 'playwright install chromium' manually before using browser automation.");
            }
        });
    }
}
