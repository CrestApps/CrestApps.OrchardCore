using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Managements.Schemas;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements;

/// <summary>
/// Registers recipe schema contributors for the Omnichannel Management feature.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, OmnichannelContactPartSchemaDefinition>();
    }
}

/// <summary>
/// Describes the lead and opportunity parts of the CRM to CrestApps Recipes.
/// </summary>
[Feature(OmnichannelConstants.Features.Crm)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class CrmRecipeSchemasStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, LeadPartSchemaDefinition>();
        services.AddScoped<IContentSchemaDefinition, OpportunityPartSchemaDefinition>();
    }
}

/// <summary>
/// Describes the CRM workflow activities to CrestApps Recipes.
/// </summary>
[Feature(OmnichannelConstants.Features.Crm)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class CrmWorkflowRecipeSchemasStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<ConvertLeadTaskSchema>()
            .AddWorkflowActivitySchema<LeadConvertedEventSchema>();
    }
}
