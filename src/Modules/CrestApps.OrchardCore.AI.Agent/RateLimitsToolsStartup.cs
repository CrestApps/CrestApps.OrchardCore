using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.RateLimits;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list, save, enable, disable and delete rate-limit policies, when the Rate Limits feature
/// is on.
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

        services.AddCoreAITool<SaveRateLimitPolicyTool>(SaveRateLimitPolicyTool.TheName)
            .WithTitle(S["Save Rate-Limit Policy"])
            .WithDescription(S["Creates or updates a rate-limit policy: name, description, scope (global, endpoint path or group) and its limiters (fixed window, sliding window, concurrency, token bucket). It always saves a new policy as disabled (turn it on with enableRateLimitPolicy), and the target and limiters of an enabled policy cannot be changed until it is disabled."])
            .WithCategory(S["Rate Limits"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<EnableRateLimitPolicyTool>(EnableRateLimitPolicyTool.TheName)
            .WithTitle(S["Enable Rate-Limit Policy"])
            .WithDescription(S["Enables one or more rate-limit policies. Requests over the limit then get 429 responses, and the tenant reloads."])
            .WithCategory(S["Rate Limits"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<DisableRateLimitPolicyTool>(DisableRateLimitPolicyTool.TheName)
            .WithTitle(S["Disable Rate-Limit Policy"])
            .WithDescription(S["Disables one or more rate-limit policies, which removes that protection, and reloads the tenant."])
            .WithCategory(S["Rate Limits"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<DeleteRateLimitPolicyTool>(DeleteRateLimitPolicyTool.TheName)
            .WithTitle(S["Delete Rate-Limit Policy"])
            .WithDescription(S["Deletes one or more rate-limit policies."])
            .WithCategory(S["Rate Limits"])
            .RequireApproval()
            .Selectable();
    }
}
