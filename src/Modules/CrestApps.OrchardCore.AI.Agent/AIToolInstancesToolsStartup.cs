using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.ToolInstances;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list, save and delete AI tool instances, when the AI Tool Instances feature is on. No tool
/// returns or takes a credential.
/// </summary>
[RequireFeatures(AIConstants.Feature.ToolInstances)]
public sealed class AIToolInstancesToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolInstancesToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolInstancesToolsStartup(IStringLocalizer<AIToolInstancesToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListAIToolInstancesTool>(ListAIToolInstancesTool.TheName)
            .WithTitle(S["List AI Tool Instances"])
            .WithDescription(S["Lists tool instances with source, name, description, function name, owner, non-secret settings and which profiles use them. With includeSources it also lists the available sources and the settings each one takes. Stored credentials are reported only as present or absent."])
            .WithCategory(S["AI Tool Instances"])
            .Selectable();

        services.AddCoreAITool<SaveAIToolInstanceTool>(SaveAIToolInstanceTool.TheName)
            .WithTitle(S["Save AI Tool Instance"])
            .WithDescription(S["Creates a tool instance from a source or updates one by id. An instance can call an outside address with stored credentials, so check the address with the user. The name cannot change after creation. An instance attached to the running assistant cannot be changed."])
            .WithCategory(S["AI Tool Instances"])
            .AsExternal()
            .Selectable();

        services.AddCoreAITool<DeleteAIToolInstanceTool>(DeleteAIToolInstanceTool.TheName)
            .WithTitle(S["Delete AI Tool Instances"])
            .WithDescription(S["Deletes tool instances by id. Instances owned by someone else are skipped unless the user may manage those."])
            .WithCategory(S["AI Tool Instances"])
            .AsDestructive()
            .Selectable();
    }
}
