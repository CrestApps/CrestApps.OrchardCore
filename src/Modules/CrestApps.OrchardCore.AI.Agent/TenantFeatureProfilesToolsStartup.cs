using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.TenantFeatureProfiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list and save tenant feature profiles, when the Tenant Feature Profiles feature is on. That
/// feature runs on the Default tenant only.
/// </summary>
[RequireFeatures(OrchardCoreFeatureIds.TenantsFeatureProfiles)]
public sealed class TenantFeatureProfilesToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantFeatureProfilesToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TenantFeatureProfilesToolsStartup(IStringLocalizer<TenantFeatureProfilesToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListTenantFeatureProfilesTool>(ListTenantFeatureProfilesTool.TheName)
            .WithTitle(S["List Tenant Feature Profiles"])
            .WithDescription(S["Lists the tenant feature profiles with their include and exclude rules and the tenants that use each one."])
            .WithCategory(S["Tenants Management"])
            .Selectable();

        services.AddCoreAITool<SaveTenantFeatureProfileTool>(SaveTenantFeatureProfileTool.TheName)
            .WithTitle(S["Save Tenant Feature Profile"])
            .WithDescription(S["Creates or updates a tenant feature profile: a name and an ordered list of rules, each with a rule name (Include or Exclude) and an expression. It limits which features the tenants using the profile can enable."])
            .WithCategory(S["Tenants Management"])
            .Selectable();
    }
}
