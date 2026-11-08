using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Workflows;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Sms;

/// <summary>
/// Describes the Send Text Message workflow task to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowRecipeSchemasStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddWorkflowActivitySchema<SendTextMessageTaskSchema>();
    }
}
