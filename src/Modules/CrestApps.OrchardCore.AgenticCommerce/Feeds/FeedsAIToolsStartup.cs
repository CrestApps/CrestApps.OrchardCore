using CrestApps.Core.AI;
using CrestApps.OrchardCore.AgenticCommerce.AI;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// Registers the agent product feed AI tools when the AI feature is on: <c>getAgentFeedStatus</c> and
/// <c>pushAgentFeeds</c>, with the permission of the Agent feeds settings page,
/// <see cref="AgentFeedPermissions.ManageAgentFeeds"/>.
/// </summary>
[Feature(AgenticCommerceConstants.Features.Feeds)]
[RequireFeatures(AIConstants.Feature.Area)]
public sealed class FeedsAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeedsAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public FeedsAIToolsStartup(IStringLocalizer<FeedsAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<GetAgentFeedStatusTool>(GetAgentFeedStatusTool.TheName)
            .WithTitle(S["Get Agent Feed Status"])
            .WithDescription(S["Returns the state of the AI agent product feed: how many products are in it, which are left out and why, the last SFTP push and the last push to each ACP feed API target with its result message. Use it to answer \"are our products reaching ChatGPT?\"."])
            .WithCategory(S["Agentic Commerce"])
            .Selectable();

        services.AddCoreAITool<PushAgentFeedsTool>(PushAgentFeedsTool.TheName)
            .WithTitle(S["Push Agent Feeds"])
            .WithDescription(S["Pushes the product feed now, over SFTP and to every enabled ACP feed API, using the saved settings, and returns each result. Use it after fixing products or settings instead of waiting for the nightly push."])
            .WithCategory(S["Agentic Commerce"])
            .AsExternal()
            .Selectable();
    }
}
