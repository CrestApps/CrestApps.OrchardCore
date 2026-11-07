using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Deployments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Deployments.Sources;
using CrestApps.OrchardCore.Omnichannel.Messaging.Deployments.Steps;
using CrestApps.OrchardCore.Omnichannel.Messaging.Recipes;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.Omnichannel.Messaging;

/// <summary>
/// Registers the <c>MessagingBroadcasts</c> recipe step and the deployment step that writes it. Broadcasts travel as
/// drafts, without their delivery, so neither a recipe nor a deployment ever sends a message.
/// </summary>
[Feature(MessagingConstants.Feature.Workspace)]
public sealed class BroadcastsRecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<MessagingBroadcastsStep>();
        services.AddScoped<IRecipeStep, MessagingBroadcastsRecipeStepSchema>();
        services.AddDeployment<MessagingBroadcastsDeploymentSource, MessagingBroadcastsDeploymentStep, SummaryDeploymentStepDisplayDriver<MessagingBroadcastsDeploymentStep>>();
        services.AddDeploymentStepSchema<MessagingBroadcastsDeploymentStepSchema>();
    }
}
