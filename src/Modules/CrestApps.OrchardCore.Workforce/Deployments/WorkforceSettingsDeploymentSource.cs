using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Workforce.Recipes;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Workforce.Deployments;

/// <summary>
/// Writes the WorkforceShiftPlanning, WorkforceTimeOff and WorkforceTimeClock steps of the features that are on, with
/// references written as names so they import into another site.
/// </summary>
internal sealed class WorkforceSettingsDeploymentSource : DeploymentSourceBase<WorkforceSettingsDeploymentStep>
{
    private readonly IServiceProvider _services;
    private readonly ITeamManager _teamManager;
    private readonly IActivityCodeManager _activityCodeManager;

    public WorkforceSettingsDeploymentSource(IServiceProvider services, ITeamManager teamManager, IActivityCodeManager activityCodeManager)
    {
        _services = services;
        _teamManager = teamManager;
        _activityCodeManager = activityCodeManager;
    }

    protected override async Task ProcessAsync(WorkforceSettingsDeploymentStep step, DeploymentPlanResult result)
    {
        var features = _services.GetService<IShellFeaturesManager>() is { } manager
            ? (await manager.GetEnabledFeaturesAsync()).Select(feature => feature.Id).ToHashSet(StringComparer.Ordinal)
            : null;
        bool On(string feature) => features is null || features.Contains(feature);

        var teams = (await _teamManager.GetAllAsync()).ToDictionary(team => team.ItemId, team => team.Name, StringComparer.Ordinal);

        if (On(WorkforceConstants.Features.ShiftPlanning) && _services.GetService<IShiftTemplateManager>() is { } templates)
        {
            var activities = (await _activityCodeManager.GetAllAsync()).ToDictionary(code => code.ItemId, code => code.Name, StringComparer.Ordinal);

            result.Steps.Add(new JsonObject
            {
                ["name"] = ShiftPlanningRecipeStep.ShiftPlanningStepName,
                ["Templates"] = new JsonArray([.. (await templates.GetAllAsync()).Select(template =>
                {
                    var node = Serialize(template, "teamIds");

                    node["teamNames"] = Names(template.TeamIds, teams);

                    if (node["segments"] is JsonArray segments)
                    {
                        foreach (var segment in segments.OfType<JsonObject>())
                        {
                            if (segment["activityCodeId"]?.GetValue<string>() is { } activityId && activities.TryGetValue(activityId, out var activityName))
                            {
                                segment["activityName"] = activityName;
                            }

                            segment.Remove("activityCodeId");
                        }
                    }

                    return (JsonNode)node;
                })]),
            });
        }

        if (On(WorkforceConstants.Features.TimeOff) && _services.GetService<ITimeOffTypeManager>() is { } typeManager)
        {
            var types = (await typeManager.GetAllAsync()).ToList();
            var typeNames = types.ToDictionary(type => type.ItemId, type => type.Name, StringComparer.Ordinal);

            result.Steps.Add(new JsonObject
            {
                ["name"] = TimeOffRecipeStep.TimeOffStepName,
                ["Types"] = new JsonArray([.. types.Select(type => (JsonNode)Serialize(type))]),
                ["Policies"] = new JsonArray([.. (await _services.GetRequiredService<ITimeOffPolicyManager>().GetAllAsync()).Select(policy =>
                {
                    var node = Serialize(policy, "typeId", "assignments");

                    node["typeName"] = typeNames.GetValueOrDefault(policy.TypeId ?? string.Empty);
                    node["teamNames"] = Names(policy.Assignments.Select(assignment => assignment.TeamId), teams);

                    return (JsonNode)node;
                })]),
                ["Allotments"] = new JsonArray([.. (await _services.GetRequiredService<ITimeOffAllotmentManager>().GetAllAsync()).Select(allotment =>
                    (JsonNode)WithTeamAndTypes(Serialize(allotment, "teamId", "typeIds"), allotment.TeamId, allotment.TypeIds, teams, typeNames))]),
                ["BlackoutPeriods"] = new JsonArray([.. (await _services.GetRequiredService<IBlackoutPeriodManager>().GetAllAsync()).Select(blackout =>
                    (JsonNode)WithTeamAndTypes(Serialize(blackout, "teamId", "typeIds"), blackout.TeamId, blackout.TypeIds, teams, typeNames))]),
            });
        }

        if (On(WorkforceConstants.Features.TimeClock) && _services.GetService<IPayPeriodCalendarManager>() is { } calendarManager)
        {
            var calendars = (await calendarManager.GetAllAsync()).ToList();
            var calendarNames = calendars.ToDictionary(calendar => calendar.ItemId, calendar => calendar.Name, StringComparer.Ordinal);
            var allTeams = await _teamManager.GetAllAsync();

            result.Steps.Add(new JsonObject
            {
                ["name"] = TimeClockRecipeStep.TimeClockStepName,
                ["PayPeriods"] = new JsonArray([.. calendars.Select(calendar => (JsonNode)Serialize(calendar))]),
                ["PayCodes"] = new JsonArray([.. (await _services.GetRequiredService<IPayCodeManager>().GetAllAsync()).Select(code => (JsonNode)Serialize(code))]),
                ["Policies"] = new JsonArray([.. (await _services.GetRequiredService<ITimeClockPolicyManager>().GetAllAsync()).Select(policy =>
                {
                    var node = Serialize(policy, "payPeriodCalendarId");

                    node["payPeriodsName"] = calendarNames.GetValueOrDefault(policy.PayPeriodCalendarId ?? string.Empty);
                    node["teamNames"] = new JsonArray([.. allTeams.Where(team => team.TimeClockPolicyId == policy.ItemId).Select(team => (JsonNode)team.Name)]);

                    return (JsonNode)node;
                })]),
            });
        }
    }

    private static JsonObject WithTeamAndTypes(JsonObject node, string teamId, IEnumerable<string> typeIds, Dictionary<string, string> teams, Dictionary<string, string> types)
    {
        if (!string.IsNullOrEmpty(teamId) && teams.TryGetValue(teamId, out var teamName))
        {
            node["teamName"] = teamName;
        }

        node["typeNames"] = Names(typeIds, types);

        return node;
    }

    private static JsonArray Names(IEnumerable<string> ids, Dictionary<string, string> names)
    {
        return new JsonArray([.. ids.Where(id => !string.IsNullOrEmpty(id) && names.ContainsKey(id)).Distinct(StringComparer.Ordinal).Select(id => (JsonNode)names[id])]);
    }

    private static JsonObject Serialize<T>(T record, params string[] without)
    {
        var node = JsonSerializer.SerializeToNode(record, WorkforceRecipeStep.JsonOptions).AsObject();

        node.Remove("id");
        node.Remove("createdUtc");
        node.Remove("modifiedUtc");

        foreach (var name in without)
        {
            node.Remove(name);
        }

        return node;
    }
}
