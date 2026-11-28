using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Agent.Connections;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// Registers the tools that list, save and delete Agent-to-Agent connections, when the A2A client is on. Credentials
/// are never taken or shown, and the connections the running assistant uses cannot be changed from a chat.
/// </summary>
[RequireFeatures(AIConstants.Feature.A2A)]
public sealed class A2AConnectionsToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2AConnectionsToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public A2AConnectionsToolsStartup(IStringLocalizer<A2AConnectionsToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListA2AConnectionsTool>(ListA2AConnectionsTool.TheName)
            .WithTitle(S["List A2A Connections"])
            .WithDescription(S["Lists A2A connections with title, endpoint, authentication type, whether credentials are stored and which profiles use them."])
            .WithCategory(S["Agent-to-Agent"])
            .Selectable();

        services.AddCoreAITool<SaveA2AConnectionTool>(SaveA2AConnectionTool.TheName)
            .WithTitle(S["Save A2A Connection"])
            .WithDescription(S["Creates or updates a connection to a remote A2A host: title, HTTP or HTTPS endpoint and authentication. Credentials are never taken: a person enters them on the admin page. Profiles that use it send conversation content to that host."])
            .WithCategory(S["Agent-to-Agent"])
            .AsExternal()
            .Selectable();

        services.AddCoreAITool<DeleteA2AConnectionTool>(DeleteA2AConnectionTool.TheName)
            .WithTitle(S["Delete A2A Connections"])
            .WithDescription(S["Deletes A2A connections by id. Profiles that used them lose those remote agents."])
            .WithCategory(S["Agent-to-Agent"])
            .AsDestructive()
            .Selectable();
    }
}
