using CrestApps.Core.AI;
using CrestApps.OrchardCore.Dispatch.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Dispatch;

/// <summary>
/// Adds the field service AI tools: work orders created, followed and summarized; the dispatch assistant that finds
/// technicians, optimizes and reschedules; and the customer's "where is my technician".
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.AI")]
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

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<WorkOrderCreateTool>(WorkOrderCreateTool.ToolName)
            .WithTitle(S["Create a work order"])
            .WithDescription(S["Creates a work order from a conversation, such as a customer's call."])
            .WithCategory(S["Field Service"])
            .Selectable();

        services.AddCoreAITool<WorkOrderStatusTool>(WorkOrderStatusTool.ToolName)
            .WithTitle(S["Work order status"])
            .WithDescription(S["Gives a work order's status, due times and visits."])
            .WithCategory(S["Field Service"])
            .Selectable();

        services.AddCoreAITool<WorkOrderSummarizeTool>(WorkOrderSummarizeTool.ToolName)
            .WithTitle(S["Summarize a work order"])
            .WithDescription(S["Gives a work order's details and history for a technician."])
            .WithCategory(S["Field Service"])
            .Selectable();

        services.AddCoreAITool<DispatchFindTechnicianTool>(DispatchFindTechnicianTool.ToolName)
            .WithTitle(S["Find a technician"])
            .WithDescription(S["Lists who can do a visit on a day, and why the others can't."])
            .WithCategory(S["Field Service"])
            .Selectable();

        services.AddCoreAITool<DispatchOptimizeTool>(DispatchOptimizeTool.ToolName)
            .WithTitle(S["Optimize and explain a day"])
            .WithDescription(S["Proposes a better dispatch day and applies the moves the dispatcher approves."])
            .WithCategory(S["Field Service"])
            .Selectable();

        services.AddCoreAITool<DispatchRescheduleVisitTool>(DispatchRescheduleVisitTool.ToolName)
            .WithTitle(S["Reschedule a visit"])
            .WithDescription(S["Moves a visit to another time or technician."])
            .WithCategory(S["Field Service"])
            .Selectable();

        services.AddCoreAITool<WhereIsMyTechnicianTool>(WhereIsMyTechnicianTool.ToolName)
            .WithTitle(S["Where is my technician"])
            .WithDescription(S["Tells a customer, found by their phone number or tracking link, when their technician comes."])
            .WithCategory(S["Field Service"])
            .Selectable();
    }
}
