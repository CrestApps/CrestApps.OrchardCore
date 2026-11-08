using CrestApps.OrchardCore.ContactCenter.Recipes;
using CrestApps.OrchardCore.ContactCenter.Recipes.Workflows;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Describes the default agent numbers site settings to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OmnichannelConstants.Features.ChannelEndpoints)]
public sealed class AgentNumbersRecipeSchemasStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, ContactCenterDefaultAddressSettingsSchema>();
    }
}

/// <summary>
/// Describes the Find Agent Numbers workflow task to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows, OmnichannelConstants.Features.ChannelEndpoints)]
public sealed class AgentNumbersWorkflowRecipeSchemasStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWorkflowActivitySchema<FindAgentNumbersTaskSchema>();
    }
}

/// <summary>
/// Describes the dialer workflow tasks this module owns the schema of to CrestApps Recipes.
/// </summary>
[Feature(ContactCenterConstants.Feature.Dialer)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class DialerWorkflowRecipeSchemasStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWorkflowActivitySchema<ScheduleDialerRetryTaskSchema>();
    }
}
