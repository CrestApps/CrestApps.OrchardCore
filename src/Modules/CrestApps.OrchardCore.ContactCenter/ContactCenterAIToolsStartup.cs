using CrestApps.Core.AI;
using CrestApps.OrchardCore.ContactCenter.AI;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Adds the Contact Center AI tools: read-only tools an AI profile can use so a manager can ask why an agent is not getting
/// calls, what is in a queue or campaign and why work is not being dialed.
/// </summary>
[Feature(ContactCenterConstants.Feature.AITools)]
public sealed class ContactCenterAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterAIToolsStartup(IStringLocalizer<ContactCenterAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        // The readers the supervisor dashboard and the reports use, so the tools and the screens never disagree. They are
        // registered here too because the tools do not need the Reports feature to be on.
        services.TryAddScoped<AgentAvailabilityReader>();
        services.TryAddScoped<DialerRecordClassifier>();
        services.TryAddScoped<IDialerQueueInventoryService, DialerQueueInventoryService>();

        services.AddCoreAITool<ExplainAgentNoCallsTool>(ExplainAgentNoCallsTool.ToolName)
            .WithTitle(S["Explain why an agent gets no calls"])
            .WithDescription(S["Explains why an agent is not receiving calls or preview offers: sign-in, presence, session, capacity, held work, queue state and recent offers and decisions."])
            .WithCategory(S["Contact Center"])
            .Selectable();

        services.AddCoreAITool<QueueInventoryTool>(QueueInventoryTool.ToolName)
            .WithTitle(S["Queue and campaign inventory"])
            .WithDescription(S["Shows what each campaign and queue holds: waiting, reserved, assigned, dialable now, held back and why, records in no queue and records assigned for manual work."])
            .WithCategory(S["Contact Center"])
            .Selectable();

        services.AddCoreAITool<DispatchDiagnosticsTool>(DispatchDiagnosticsTool.ToolName)
            .WithTitle(S["Dispatch diagnostics"])
            .WithDescription(S["Says whether routing and the dialer are working for each queue and campaign, and which queues have work due but nothing happening."])
            .WithCategory(S["Contact Center"])
            .Selectable();

        services.AddCoreAITool<DecisionLogTool>(DecisionLogTool.ToolName)
            .WithTitle(S["Dialer decision log"])
            .WithDescription(S["Queries routing decisions and dial suppressions by agent, campaign, queue, activity, reason and time, and calls out a queue drained in seconds."])
            .WithCategory(S["Contact Center"])
            .Selectable();

        services.AddCoreAITool<ExplainActivityTool>(ExplainActivityTool.ToolName)
            .WithTitle(S["Explain an activity"])
            .WithDescription(S["Explains one activity: its status, owner, queue item, dialer profile, destination and why it is or is not dialable."])
            .WithCategory(S["Contact Center"])
            .Selectable();

        services.AddCoreAITool<ListConfigurationTool>(ListConfigurationTool.ToolName)
            .WithTitle(S["Campaigns, profiles and queues"])
            .WithDescription(S["Lists the campaigns, dialer profiles and queues with their mode and settings."])
            .WithCategory(S["Contact Center"])
            .Selectable();

        services.AddCoreAITool<AgentRosterTool>(AgentRosterTool.ToolName)
            .WithTitle(S["Agent roster"])
            .WithDescription(S["Lists who is signed in where, with status, capacity and current work."])
            .WithCategory(S["Contact Center"])
            .Selectable();

        services.AddCoreAITool<StuckWorkTool>(StuckWorkTool.ToolName)
            .WithTitle(S["Stuck work"])
            .WithDescription(S["Finds reservations nobody released, stranded activities, stalled queues and agents with a stale state."])
            .WithCategory(S["Contact Center"])
            .Selectable();
    }
}
