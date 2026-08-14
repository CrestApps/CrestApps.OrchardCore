using CrestApps.Core.AI.Security;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Services;

internal sealed class PromptSecurityOptionsConfiguration : IConfigureOptions<PromptSecurityOptions>
{
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptSecurityOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    public PromptSecurityOptionsConfiguration(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <summary>
    /// Configures prompt security options from Orchard site settings.
    /// </summary>
    /// <param name="options">The options.</param>
    public void Configure(PromptSecurityOptions options)
    {
        var settings = _siteService.GetSettings<PromptSecurityOptions>();

        options.MaxPromptLength = settings.MaxPromptLength;
        options.EnableInjectionDetection = settings.EnableInjectionDetection;
        options.EnableOutputFiltering = settings.EnableOutputFiltering;
        options.EnableSecurityPreamble = settings.EnableSecurityPreamble;
        options.EnableInputDelimiters = settings.EnableInputDelimiters;
        options.EnableAuditLogging = settings.EnableAuditLogging;
        options.BlockingThreshold = settings.BlockingThreshold;
        options.LowRiskScoreThreshold = settings.LowRiskScoreThreshold;
        options.MediumRiskScoreThreshold = settings.MediumRiskScoreThreshold;
        options.HighRiskScoreThreshold = settings.HighRiskScoreThreshold;
        options.CriticalRiskScoreThreshold = settings.CriticalRiskScoreThreshold;
        options.CustomBlockedPatterns = settings.CustomBlockedPatterns;
        options.MaxMessagesPerWindow = settings.MaxMessagesPerWindow;
        options.RateLimitWindow = settings.RateLimitWindow;
        options.MaxAnonymousSessionsPerWindow = settings.MaxAnonymousSessionsPerWindow;
        options.AnonymousSessionRateLimitWindow = settings.AnonymousSessionRateLimitWindow;
    }
}
