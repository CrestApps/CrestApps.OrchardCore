using CrestApps.Core.AI;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Tickets.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.Tickets;

/// <summary>
/// The ticket AI tools, which administrators can pick on AI profiles: list the options, search, read, open, reply to or
/// note on, and change tickets, always as the signed-in user and within the tickets they may view; and the desks,
/// statuses, priorities and saved views as record kinds of the generic record tools.
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
        services.AddScoped<TicketAssistant>();
        services.AddDisplayDriver<Models.TicketDesk, TicketDeskAIDisplayDriver>();

        // Settings > Service Desk (its menu and permission come with Tickets): the profile every desk that names none uses.
        services.AddSiteDisplayDriver<TicketAISettingsDisplayDriver>();
        services.AddContentPart<Models.TicketPart>()
            .UseDisplayDriver<TicketAssistantDisplayDriver>();
        services.AddCoreAITool<UpdateTicketTool>(UpdateTicketTool.TheName).WithTitle(S["Update ticket"]).WithDescription(S["Changes a ticket's status, priority, team or assignee."]).WithCategory(S["Tickets"]).Selectable();

        // The Service Desk setup records, for the generic record tools: written through the same editors and rules as
        // their settings pages, with the permission those pages check.
        services.AddAIRecordKind<Models.TicketDesk>(TicketRecordKinds.DeskKind, kind => kind
            .WithTitle(S["Ticket desk"])
            .WithDescription(S["a ticket desk: where tickets are filed and who may see them (per-desk permissions), its default team, business hours, service level policy, ticket types, reopen and auto-close days, assignment and notifications. Its AI settings are changed on its page only, and protected information is never erased sooner from the chat"])
            .WithPermission(TicketsPermissions.ManageTicketSettings)
            .WithProjection(TicketRecordKinds.DescribeDesk)
            .ValidateUsing(TicketRecordKinds.ValidateDeskAsync)
            .WithInUseCheck(TicketRecordKinds.GetDeskUseAsync)
            .UseEditorReplay());

        services.AddAIRecordKind<Models.TicketStatus>(TicketRecordKinds.StatusKind, kind => kind
            .WithTitle(S["Ticket status"])
            .WithDescription(S["a ticket status in one of the categories New, Open, Pending, OnHold, Solved or Closed, with the label customers see, its color, its order and whether it pauses service-level timers"])
            .WithPermission(TicketsPermissions.ManageTicketSettings)
            .WithProjection(TicketRecordKinds.DescribeStatus)
            .WithInUseCheck(TicketRecordKinds.GetStatusUseAsync)
            .UseEditorReplay());

        services.AddAIRecordKind<Models.TicketPriority>(TicketRecordKinds.PriorityKind, kind => kind
            .WithTitle(S["Ticket priority"])
            .WithDescription(S["a ticket priority with its rank (higher is more urgent) and color; desks' priority matrices and service level policies name priorities by id"])
            .WithPermission(TicketsPermissions.ManageTicketSettings)
            .WithProjection(TicketRecordKinds.DescribePriority)
            .WithInUseCheck(TicketRecordKinds.GetPriorityUseAsync)
            .UseEditorReplay());

        services.AddAIRecordKind<Models.TicketView>(TicketRecordKinds.ViewKind, kind => kind
            .WithTitle(S["Ticket view"])
            .WithDescription(S["a saved ticket list on the Tickets page: the status categories, desks, assignee (me, unassigned or a user id), due within hours and sort it shows, shared with everyone or with one team"])
            .WithPermission(TicketsPermissions.ManageTicketSettings)
            .WithProjection(TicketRecordKinds.DescribeView)
            .ValidateUsing(TicketRecordKinds.ValidateViewAsync)
            .UseEditorReplay());
    }
}

/// <summary>
/// The <b>Triage Ticket With AI</b> workflow task.
/// </summary>
[RequireFeatures(AIConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class AIWorkflowsStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddActivity<TriageTicketTask, Workflows.TicketTaskDisplayDriver<TriageTicketTask>>();
}

/// <summary>
/// The recipe schema of the <b>Triage Ticket With AI</b> workflow task.
/// </summary>
[RequireFeatures(AIConstants.Feature.Area, OrchardCoreFeatureIds.Workflows, CrestApps.OrchardCore.Recipes.Core.CrestAppsRecipesConstants.Feature.Area)]
public sealed class AIWorkflowsRecipeSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
        => services.AddWorkflowActivitySchema<Recipes.Workflows.TriageTicketTaskSchema>();
}
