using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Tickets.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets;

/// <summary>
/// The ticket AI tools, which administrators can pick on AI profiles: list the options, search, read, open, reply to or
/// note on, and change tickets, always as the signed-in user and within the tickets they may view.
/// </summary>
[RequireFeatures(AIConstants.Feature.Area)]
public sealed class AIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIToolsStartup(IStringLocalizer<AIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListTicketOptionsTool>(ListTicketOptionsTool.TheName).WithTitle(S["List ticket options"]).WithDescription(S["Lists the desks, statuses and priorities of tickets, with their ids."]).WithCategory(S["Tickets"]).Selectable();
        services.AddCoreAITool<SearchTicketsTool>(SearchTicketsTool.TheName).WithTitle(S["Search tickets"]).WithDescription(S["Finds the tickets the user may view."]).WithCategory(S["Tickets"]).Selectable();
        services.AddCoreAITool<GetTicketTool>(GetTicketTool.TheName).WithTitle(S["Get ticket"]).WithDescription(S["Reads a ticket with its latest replies, notes and events."]).WithCategory(S["Tickets"]).Selectable();
        services.AddCoreAITool<CreateTicketTool>(CreateTicketTool.TheName).WithTitle(S["Create ticket"]).WithDescription(S["Opens a ticket on a desk the user may reply in."]).WithCategory(S["Tickets"]).Selectable();
        services.AddCoreAITool<AddTicketReplyTool>(AddTicketReplyTool.TheName).WithTitle(S["Add ticket reply"]).WithDescription(S["Sends a public reply or adds an internal note to a ticket."]).WithCategory(S["Tickets"]).Selectable();
        services.AddCoreAITool<UpdateTicketTool>(UpdateTicketTool.TheName).WithTitle(S["Update ticket"]).WithDescription(S["Changes a ticket's status, priority, team or assignee."]).WithCategory(S["Tickets"]).Selectable();
    }
}
