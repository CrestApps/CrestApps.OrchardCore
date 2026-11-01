using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.Recipes;
using CrestApps.OrchardCore.Workforce.Services;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Recipes;

/// <summary>
/// The WorkforceForecasting recipe step: planning groups (with their queues by <c>queueNames</c> and their team by
/// <c>teamName</c>), forecast events (with their planning groups by <c>planningGroupNames</c>) and capacity plans (with
/// their planning group by <c>planningGroupName</c>), each matched by id or name.
/// </summary>
internal sealed class ForecastingRecipeStep : NamedRecipeStepHandler
{
    public const string ForecastingStepName = "WorkforceForecasting";

    private readonly PlanningGroupManager _groupManager;
    private readonly ForecastEventManager _eventManager;
    private readonly CapacityPlanManager _planManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly ITeamManager _teamManager;

    public ForecastingRecipeStep(PlanningGroupManager groupManager, ForecastEventManager eventManager, CapacityPlanManager planManager, IActivityQueueManager queueManager, ITeamManager teamManager)
        : base(ForecastingStepName)
    {
        _groupManager = groupManager;
        _eventManager = eventManager;
        _planManager = planManager;
        _queueManager = queueManager;
        _teamManager = teamManager;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var import = new RecipeImport(context, ForecastingStepName);
        var queues = (await _queueManager.GetAllAsync()).ToList();
        var teams = (await _teamManager.GetAllAsync()).ToList();
        var groups = (await _groupManager.GetAllAsync()).ToList();

        foreach (var (node, incoming) in import.Read<PlanningGroup>("PlanningGroups"))
        {
            var ok = true;

            foreach (var queueName in RecipeImport.Strings(node, "queueNames"))
            {
                var queue = import.Named(queues, item => item.Name, queueName, "planning group", incoming.Name, "a queue");

                ok &= queue is not null;

                if (queue is not null && !incoming.QueueIds.Contains(queue.ItemId))
                {
                    incoming.QueueIds.Add(queue.ItemId);
                }
            }

            if (RecipeImport.String(node, "teamName") is { } teamName)
            {
                var team = import.Named(teams, item => item.Name, teamName, "planning group", incoming.Name, "a team");

                ok &= team is not null;
                incoming.TeamId = team?.ItemId;
            }

            if (ok && await import.SaveAsync(_groupManager, incoming, RecipeImport.Existing(groups, incoming, item => item.Name), $"planning group '{incoming.Name}'") is { } saved && !groups.Contains(saved))
            {
                groups.Add(saved);
            }
        }

        var events = (await _eventManager.GetAllAsync()).ToList();

        foreach (var (node, incoming) in import.Read<ForecastEvent>("ForecastEvents"))
        {
            if (!Resolve(import, groups, RecipeImport.Strings(node, "planningGroupNames"), "forecast event", incoming.Name, incoming.PlanningGroupIds))
            {
                continue;
            }

            if (await import.SaveAsync(_eventManager, incoming, RecipeImport.Existing(events, incoming, item => item.Name), $"forecast event '{incoming.Name}'") is { } saved && !events.Contains(saved))
            {
                events.Add(saved);
            }
        }

        var plans = (await _planManager.GetAllAsync()).ToList();

        foreach (var (node, incoming) in import.Read<CapacityPlan>("CapacityPlans"))
        {
            if (RecipeImport.String(node, "planningGroupName") is { } groupName)
            {
                var group = import.Named(groups, item => item.Name, groupName, "capacity plan", incoming.Name, "a planning group");

                if (group is null)
                {
                    continue;
                }

                incoming.PlanningGroupId = group.ItemId;
            }

            // A plan runs week by week from a Monday.
            incoming.StartDate = incoming.StartDate.AddDays(-(((int)incoming.StartDate.DayOfWeek + 6) % 7));

            if (await import.SaveAsync(_planManager, incoming, RecipeImport.Existing(plans, incoming, item => item.Name), $"capacity plan '{incoming.Name}'") is { } saved && !plans.Contains(saved))
            {
                plans.Add(saved);
            }
        }
    }

    private static bool Resolve(RecipeImport import, IReadOnlyList<PlanningGroup> groups, IReadOnlyList<string> names, string owner, string ownerName, List<string> ids)
    {
        var ok = true;

        foreach (var name in names)
        {
            var group = import.Named(groups, item => item.Name, name, owner, ownerName, "a planning group");

            ok &= group is not null;

            if (group is not null && !ids.Contains(group.ItemId))
            {
                ids.Add(group.ItemId);
            }
        }

        return ok;
    }
}
