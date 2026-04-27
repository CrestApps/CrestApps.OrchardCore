using CrestApps.Core.AI.Copilot.Models;
using CrestApps.Core.AI.Copilot.Services;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.OrchardCore.AI.Chat.Copilot.Settings;
using Microsoft.Extensions.Localization;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.Services;

internal sealed class CopilotOrchestratorAvailabilityProvider : IOrchestratorAvailabilityProvider
{
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CopilotOrchestratorAvailabilityProvider"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CopilotOrchestratorAvailabilityProvider(
        ISiteService siteService,
        IStringLocalizer<CopilotOrchestratorAvailabilityProvider> stringLocalizer)
    {
        _siteService = siteService;
        S = stringLocalizer;
    }

    public string OrchestratorName => CopilotOrchestrator.OrchestratorName;

    /// <summary>
    /// Retrieves the availability async.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<OrchestratorAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _siteService.GetSettingsAsync<CopilotSettings>();

        return settings.IsConfigured()
        ? new OrchestratorAvailability()
        : new OrchestratorAvailability
        {
            IsAvailable = false,
            Message = S["Copilot is not configured and cannot be used until it has been configured."],
        };
    }
}
