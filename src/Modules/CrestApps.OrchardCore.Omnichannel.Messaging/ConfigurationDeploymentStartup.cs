using CrestApps.OrchardCore.Omnichannel.Messaging.Deployments.Drivers;
using CrestApps.OrchardCore.Omnichannel.Messaging.Deployments.Sources;
using CrestApps.OrchardCore.Omnichannel.Messaging.Deployments.Steps;
using CrestApps.OrchardCore.Omnichannel.Messaging.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.Omnichannel.Messaging;

/// <summary>
/// Registers the deployment step that exports the messaging workspace's canned-response templates.
/// </summary>
/// <remarks>
/// Conversations, broadcasts and the unread and assignment state of the inbox are the workspace's operational record,
/// not its configuration, so they are deliberately left out of a deployment plan. The routing a channel endpoint carries
/// travels with the endpoint's own step.
/// </remarks>
[Feature(MessagingConstants.Feature.Workspace)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class ConfigurationDeploymentStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<OmnichannelMessageTemplateDeploymentSource, OmnichannelMessageTemplateDeploymentStep>();
        services.AddDisplayDriver<DeploymentStep, OmnichannelMessageTemplateDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Registers the recipe step that imports the messaging workspace's canned-response templates.
/// </summary>
[Feature(MessagingConstants.Feature.Workspace)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class ConfigurationRecipesStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<OmnichannelMessageTemplateStep>();
    }
}
