using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.AIModels;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that manage AI deployments, their prices and the voice library of text-to-speech deployments.
/// Keys are never taken or shown, and the deployments the running assistant uses cannot be changed from a chat.
/// </summary>
[RequireFeatures(AIConstants.Feature.Area)]
public sealed class AIDeploymentsToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDeploymentsToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIDeploymentsToolsStartup(IStringLocalizer<AIDeploymentsToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListAIDeploymentsTool>(ListAIDeploymentsTool.TheName)
            .WithTitle(S["List AI Deployments"])
            .WithDescription(S["Lists deployments with provider, connection, model, capabilities, prices, whether a contained key is stored, whether they come from configuration, and which slot defaults and profiles use them. Filter by provider, connection or capability."])
            .WithCategory(S["AI Models"])
            .Selectable();

        services.AddCoreAITool<SaveAIDeploymentTool>(SaveAIDeploymentTool.TheName)
            .WithTitle(S["Save AI Deployment"])
            .WithDescription(S["Creates a deployment under a provider or updates one by id: name, model, connection, capabilities and, for speech providers, the contained endpoint. Keys are never taken or shown: a person enters the key on the admin page the result links to. It decides which provider receives the site's AI traffic. Deployments from configuration cannot be changed."])
            .WithCategory(S["AI Models"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<SetAIDeploymentPricingTool>(SetAIDeploymentPricingTool.TheName)
            .WithTitle(S["Set AI Deployment Pricing"])
            .WithDescription(S["Sets or clears what a deployment costs: currency and a list of prices per meter (for example input tokens per 1,000,000). Only affects cost estimates in the usage reports."])
            .WithCategory(S["AI Models"])
            .Selectable();

        services.AddCoreAITool<DeleteAIDeploymentTool>(DeleteAIDeploymentTool.TheName)
            .WithTitle(S["Delete AI Deployments"])
            .WithDescription(S["Deletes deployments by id. Profiles that named a deleted deployment fall back or stop working. Deployments from configuration and the site's default deployments are skipped."])
            .WithCategory(S["AI Models"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<ListAIVoicesTool>(ListAIVoicesTool.TheName)
            .WithTitle(S["List AI Voices"])
            .WithDescription(S["Searches the voices of a text-to-speech deployment by text and language and returns their ids and names, with a cursor for the next page. Use it to find a voice id for a profile or persona."])
            .WithCategory(S["AI Models"])
            .Selectable();

        services.AddCoreAITool<CloneAIVoiceTool>(CloneAIVoiceTool.TheName)
            .WithTitle(S["Clone AI Voice"])
            .WithDescription(S["Creates a cloned voice at the provider from an audio file in the media library (up to 10 MB) and returns its id. Only for providers that support cloning; the provider may charge for it."])
            .WithCategory(S["AI Models"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<DeleteAIVoiceTool>(DeleteAIVoiceTool.TheName)
            .WithTitle(S["Delete AI Voice"])
            .WithDescription(S["Deletes a voice at the provider. It cannot be undone, and profiles or personas that use the voice lose it."])
            .WithCategory(S["AI Models"])
            .RequireApproval()
            .Selectable();
    }
}
