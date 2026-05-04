using CrestApps.Core.AI.Claude.Models;
using CrestApps.Core.AI.Claude.Services;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.OrchardCore.AI.Chat.Claude.Settings;
using Microsoft.Extensions.Localization;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Claude.Services;

internal sealed class ClaudeOrchestratorAvailabilityProvider : IOrchestratorAvailabilityProvider
{
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClaudeOrchestratorAvailabilityProvider"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ClaudeOrchestratorAvailabilityProvider(
        ISiteService siteService,
        IStringLocalizer<ClaudeOrchestratorAvailabilityProvider> stringLocalizer)
    {
        _siteService = siteService;
        S = stringLocalizer;
    }

    public string OrchestratorName => ClaudeOrchestrator.OrchestratorName;

    /// <summary>
    /// Retrieves the availability async.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<OrchestratorAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _siteService.GetSettingsAsync<ClaudeSettings>();

        return settings.IsConfigured()
            ? new OrchestratorAvailability()
            : new OrchestratorAvailability
            {
                IsAvailable = false,
                Message = S["Claude is not configured and cannot be used until it has been configured."],
            };
    }
}
