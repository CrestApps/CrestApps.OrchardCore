using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Workforce.Recipes;

/// <summary>
/// Imports the Workforce step: activity codes (by id or name), teams (by id or name, with the parent by
/// <c>parentName</c> in any order) and workers (by id, employee number or user, with their teams by <c>teamName</c>).
/// Every record goes through its manager, so it is validated and each team gets its calendar.
/// </summary>
internal sealed class WorkforceRecipeStep : NamedRecipeStepHandler
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly IActivityCodeManager _activityCodeManager;
    private readonly ITeamManager _teamManager;
    private readonly IWorkerManager _workerManager;

    public WorkforceRecipeStep(
        IActivityCodeManager activityCodeManager,
        ITeamManager teamManager,
        IWorkerManager workerManager)
        : base(WorkforceConstants.RecipeStepName)
    {
        _activityCodeManager = activityCodeManager;
        _teamManager = teamManager;
        _workerManager = workerManager;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        await ImportActivityCodesAsync(context);
        await ImportTeamsAsync(context);
        await ImportWorkersAsync(context);
    }

    private async Task ImportActivityCodesAsync(RecipeExecutionContext context)
    {
        var existing = (await _activityCodeManager.GetAllAsync()).ToList();

        foreach (var node in Nodes(context, "ActivityCodes"))
        {
            var incoming = Read<ActivityCode>(context, node, "ActivityCodes");

            if (incoming is null)
            {
                continue;
            }

            var match = existing.FirstOrDefault(code => code.ItemId == incoming.ItemId)
                ?? existing.FirstOrDefault(code => WorkforceNames.Normalize(code.Name) == WorkforceNames.Normalize(incoming.Name));

            if (await SaveAsync(context, _activityCodeManager, incoming, match, "activity code") is { } saved && match is null)
            {
                existing.Add(saved);
            }
        }
    }

    private async Task ImportTeamsAsync(RecipeExecutionContext context)
    {
        var pending = Nodes(context, "Teams").ToList();
        var teams = (await _teamManager.GetAllAsync()).ToList();

        // A team can name a parent listed after it, so teams are placed in passes until no more can be.
        while (pending.Count > 0)
        {
            var placed = 0;

            foreach (var node in pending.ToList())
            {
                var parentName = node["parentName"]?.GetValue<string>();
                var parent = string.IsNullOrWhiteSpace(parentName)
                    ? null
                    : teams.FirstOrDefault(team => WorkforceNames.Normalize(team.Name) == WorkforceNames.Normalize(parentName));

                if (parent is null && !string.IsNullOrWhiteSpace(parentName))
                {
                    continue;
                }

                pending.Remove(node);
                placed++;

                var incoming = Read<WorkforceTeam>(context, node, "Teams");

                if (incoming is null)
                {
                    continue;
                }

                if (parent is not null)
                {
                    incoming.ParentId = parent.ItemId;
                }

                var match = teams.FirstOrDefault(team => team.ItemId == incoming.ItemId)
                    ?? teams.FirstOrDefault(team => WorkforceNames.Normalize(team.Name) == WorkforceNames.Normalize(incoming.Name));

                // The calendar belongs to the site; an imported team keeps the one it has here.
                incoming.CalendarId = match?.CalendarId;

                if (await SaveAsync(context, _teamManager, incoming, match, "team") is { } saved && match is null)
                {
                    teams.Add(saved);
                }
            }

            if (placed == 0)
            {
                foreach (var node in pending)
                {
                    context.Errors.Add($"The team '{node["name"]}' in the Workforce step names a parent team that doesn't exist: '{node["parentName"]}'.");
                }

                break;
            }
        }
    }

    private async Task ImportWorkersAsync(RecipeExecutionContext context)
    {
        var teams = await _teamManager.GetAllAsync();

        foreach (var node in Nodes(context, "Workers"))
        {
            var incoming = Read<WorkerProfile>(context, node, "Workers");

            if (incoming is null)
            {
                continue;
            }

            var memberships = node["teamMemberships"] as JsonArray ?? [];
            var missingTeam = false;

            for (var i = 0; i < incoming.TeamMemberships.Count && i < memberships.Count; i++)
            {
                var teamName = memberships[i]?["teamName"]?.GetValue<string>();

                if (string.IsNullOrWhiteSpace(teamName))
                {
                    continue;
                }

                var team = teams.FirstOrDefault(item => WorkforceNames.Normalize(item.Name) == WorkforceNames.Normalize(teamName));

                if (team is null)
                {
                    context.Errors.Add($"The worker '{incoming.DisplayName}' in the Workforce step belongs to a team that doesn't exist: '{teamName}'.");
                    missingTeam = true;

                    break;
                }

                incoming.TeamMemberships[i].TeamId = team.ItemId;
            }

            if (missingTeam)
            {
                continue;
            }

            var match = (string.IsNullOrEmpty(incoming.ItemId) ? null : await _workerManager.FindByIdAsync(incoming.ItemId))
                ?? await _workerManager.FindByEmployeeNumberAsync(incoming.EmployeeNumber)
                ?? await _workerManager.FindByUserIdAsync(incoming.UserId);

            await SaveAsync(context, _workerManager, incoming, match, "worker");
        }
    }

    private static async Task<T> SaveAsync<T>(RecipeExecutionContext context, ICatalogManager<T> manager, T incoming, T existing, string label)
        where T : CatalogItem
    {
        var target = existing ?? await manager.NewAsync();
        var id = existing?.ItemId ?? (string.IsNullOrEmpty(incoming.ItemId) ? target.ItemId : incoming.ItemId);

        CopyValues(incoming, target);
        target.ItemId = id;

        var validation = await manager.ValidateAsync(target);

        if (!validation.Succeeded)
        {
            context.Errors.Add($"A {label} in the Workforce step was refused: {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage))}");

            return null;
        }

        if (existing is null)
        {
            await manager.CreateAsync(target);
        }
        else
        {
            await manager.UpdateAsync(target);
        }

        return target;
    }

    private static void CopyValues<T>(T source, T target)
    {
        foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0 && property.Name is not ("CreatedUtc" or "ModifiedUtc"))
            {
                property.SetValue(target, property.GetValue(source));
            }
        }
    }

    private static IEnumerable<JsonObject> Nodes(RecipeExecutionContext context, string section)
    {
        return (context.Step[section] as JsonArray ?? []).OfType<JsonObject>();
    }

    private static T Read<T>(RecipeExecutionContext context, JsonObject node, string section)
        where T : class
    {
        try
        {
            return node.Deserialize<T>(JsonOptions);
        }
        catch (JsonException ex)
        {
            context.Errors.Add($"An entry of {section} in the Workforce step can't be read: {ex.Message}");

            return null;
        }
    }
}
