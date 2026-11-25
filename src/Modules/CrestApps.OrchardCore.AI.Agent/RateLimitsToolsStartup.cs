using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.RateLimits;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list rate-limit policies and enable or disable them, when the Rate Limits feature is on.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.RateLimits)]
public sealed class RateLimitsToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitsToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public RateLimitsToolsStartup(IStringLocalizer<RateLimitsToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListRateLimitPoliciesTool>(ListRateLimitPoliciesTool.TheName)
            .WithTitle(S["List Rate-Limit Policies"])
            .WithDescription(S["Lists the site's rate-limit policies with scope, path or group, status and limiters, plus the built-in route and group limits that modules register. Optional search text and status filter."])
            .WithCategory(S["Rate Limits"])
            .Selectable();

        services.AddCoreAITool<SetRateLimitPolicyEnabledTool>(SetRateLimitPolicyEnabledTool.TheName)
            .WithTitle(S["Enable or Disable Rate-Limit Policies"])
            .WithDescription(S["Enables or disables one or more rate-limit policies, and the tenant reloads. Enabling a global policy with a low limit can lock everyone out, this chat included; disabling removes that protection and needs a confirmation for a policy that protects sign-in."])
            .WithCategory(S["Rate Limits"])
            .AsSecurity()
            .Selectable();
    }
}
