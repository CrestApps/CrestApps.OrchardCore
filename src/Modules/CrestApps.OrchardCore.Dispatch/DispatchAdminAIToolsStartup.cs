using CrestApps.Core.AI;
using CrestApps.OrchardCore.Dispatch.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Dispatch;

/// <summary>
/// Adds the dispatch administration AI tools: the visits list and a visit, making visits, pinning and unassigning,
/// moving a visit along or closing it, the dispatch board's day and the field service KPIs. They work only for a
/// signed-in person with the permission of the matching admin page. The existing field service tools in
/// <see cref="AIToolsStartup"/> are unchanged.
/// </summary>
[Feature(DispatchConstants.Features.Dispatch)]
[RequireFeatures(AIFeatureIds.Area)]
public sealed class DispatchAdminAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DispatchAdminAIToolsStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DispatchAdminAIToolsStartup(IStringLocalizer<DispatchAdminAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<ListVisitsTool>(ListVisitsTool.ToolName)
            .WithTitle(S["List visits"])
            .WithDescription(S["Lists visits filtered by status (or all open), day, technician and a search on visit number, work order number or summary, with paging; also says how many work orders have no visit."])
            .WithCategory(S["Field Service"])
            .Selectable();

        services.AddCoreAITool<GetVisitTool>(GetVisitTool.ToolName)
            .WithTitle(S["Get visit"])
            .WithDescription(S["Gets one visit: its work order, status and history, technicians, times, arrival window, ETA, travel, equipment, whether it is pinned or at risk, the customer notices sent and the statuses it can move to next."])
            .WithCategory(S["Field Service"])
            .Selectable();

        services.AddCoreAITool<CreateVisitsTool>(CreateVisitsTool.ToolName)
            .WithTitle(S["Create visits"])
            .WithDescription(S["Makes a visit for one work order, or for every open work order that has none. New visits start unscheduled."])
            .WithCategory(S["Field Service"])
            .Selectable();

        services.AddCoreAITool<UpdateVisitTool>(UpdateVisitTool.ToolName)
            .WithTitle(S["Update visit"])
            .WithDescription(S["Pins a visit so the optimizer leaves its technician and time alone, unpins it, or takes it off its technicians and time back to the unscheduled visits."])
            .WithCategory(S["Field Service"])
            .Selectable();

        services.AddCoreAITool<SetVisitStatusTool>(SetVisitStatusTool.ToolName)
            .WithTitle(S["Set visit status"])
            .WithDescription(S["Moves a visit to Dispatched, En route, On site or Paused, with an optional note. En route texts or emails the customer when customer notices are on."])
            .WithCategory(S["Field Service"])
            .Selectable()
            .RequireApproval();

        services.AddCoreAITool<CloseVisitTool>(CloseVisitTool.ToolName)
            .WithTitle(S["Close visit"])
            .WithDescription(S["Ends a visit as Completed, Incomplete or Canceled, with a note. These are final, and Completed and Canceled notify the customer when notices are on."])
            .WithCategory(S["Field Service"])
            .Selectable()
            .RequireApproval();

        services.AddCoreAITool<GetDispatchBoardTool>(GetDispatchBoardTool.ToolName)
            .WithTitle(S["Get dispatch board"])
            .WithDescription(S["Gives a day's dispatch picture: each technician with their shift, time off and visits in order, and the visits waiting for a time; optionally for one team or capability. Use it to answer \"what does tomorrow look like\"."])
            .WithCategory(S["Field Service"])
            .Selectable();

        services.AddCoreAITool<GetFieldServiceKpisTool>(GetFieldServiceKpisTool.ToolName)
            .WithTitle(S["Get field service KPIs"])
            .WithDescription(S["Gives the field-service KPIs for a date range: finished visits, first-time fix, on-time arrival, travel and on-site time, jobs per technician day, repeat work, SLA compliance, hours to repair, revenue per work order and parts used."])
            .WithCategory(S["Field Service"])
            .Selectable();
    }
}
