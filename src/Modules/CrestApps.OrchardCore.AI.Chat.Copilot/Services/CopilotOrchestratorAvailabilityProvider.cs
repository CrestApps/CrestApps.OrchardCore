using CrestApps.OrchardCore.AI.Chat.Copilot.Settings;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.Services;

internal sealed class CopilotOrchestratorAvailabilityProvider : IOrchestratorAvailabilityProvider
{
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    public CopilotOrchestratorAvailabilityProvider(
        ISiteService siteService,
        IStringLocalizer<CopilotOrchestratorAvailabilityProvider> stringLocalizer)
    {
        _siteService = siteService;
        S = stringLocalizer;
    }

    public string OrchestratorName => CopilotOrchestrator.OrchestratorName;

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
