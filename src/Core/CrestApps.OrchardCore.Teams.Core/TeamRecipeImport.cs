using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;

namespace CrestApps.OrchardCore.Teams.Core;

/// <summary>
/// Imports teams from recipe JSON, for the Teams step and for the steps of modules that build on teams. Each team is
/// created, or updated when one has its id or its name; <c>parentName</c> names a parent that may be listed after it.
/// Every team goes through the team manager, so it is validated and the catalog handlers of every module run.
/// </summary>
public static class TeamRecipeImport
{
    /// <summary>
    /// The JSON options teams are read and written with: web casing, and enums by name.
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Creates or updates the teams.
    /// </summary>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="nodes">The JSON of the teams.</param>
    /// <param name="errors">Receives a message for each team that can't be read or is refused.</param>
    /// <param name="stepName">The recipe step, for the messages.</param>
    /// <param name="configure">
    /// Optional. Runs on each team before it is validated, with the JSON it was read from, so a module copies its own
    /// settings onto the team.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The teams that were saved.</returns>
    public static async Task<IReadOnlyList<Team>> ImportAsync(
        ITeamManager teamManager,
        IEnumerable<JsonObject> nodes,
        ICollection<string> errors,
        string stepName,
        Action<JsonObject, Team> configure = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(teamManager);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(errors);

        var pending = nodes.ToList();
        var teams = (await teamManager.GetAllAsync(cancellationToken)).ToList();
        var saved = new List<Team>();

        // A team can name a parent listed after it, so teams are placed in passes until no more can be.
        while (pending.Count > 0)
        {
            var placed = 0;

            foreach (var node in pending.ToList())
            {
                var parentName = String(node, "parentName");
                var parent = parentName is null
                    ? null
                    : teams.FirstOrDefault(team => Normalize(team.Name) == Normalize(parentName));

                if (parent is null && parentName is not null)
                {
                    continue;
                }

                pending.Remove(node);
                placed++;

                var incoming = Read(node, errors, stepName);

                if (incoming is null)
                {
                    continue;
                }

                if (parent is not null)
                {
                    incoming.ParentId = parent.ItemId;
                }

                var match = teams.FirstOrDefault(team => !string.IsNullOrEmpty(incoming.ItemId) && team.ItemId == incoming.ItemId)
                    ?? teams.FirstOrDefault(team => Normalize(team.Name) == Normalize(incoming.Name));
                var team = await SaveAsync(teamManager, node, incoming, match, errors, stepName, configure, cancellationToken);

                if (team is null)
                {
                    continue;
                }

                saved.Add(team);

                if (match is null)
                {
                    teams.Add(team);
                }
            }

            if (placed == 0)
            {
                foreach (var node in pending)
                {
                    errors.Add($"The team '{String(node, "name")}' in the {stepName} step names a parent team that doesn't exist: '{String(node, "parentName")}'.");
                }

                break;
            }
        }

        return saved;
    }

    /// <summary>
    /// Gets the form of a team name used to match it with another: trimmed and in upper case.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The normalized name, or <see langword="null"/> when it is empty.</returns>
    public static string Normalize(string name)
    {
        return string.IsNullOrWhiteSpace(name)
            ? null
            : name.Trim().ToUpperInvariant();
    }

    private static async Task<Team> SaveAsync(
        ITeamManager teamManager,
        JsonObject node,
        Team incoming,
        Team existing,
        ICollection<string> errors,
        string stepName,
        Action<JsonObject, Team> configure,
        CancellationToken cancellationToken)
    {
        var target = existing ?? await teamManager.NewAsync(cancellationToken: cancellationToken);

        // The generic fields are copied; what other modules keep on the team stays, unless their own step changes it.
        target.ItemId = existing?.ItemId ?? (string.IsNullOrEmpty(incoming.ItemId) ? target.ItemId : incoming.ItemId);
        target.Name = incoming.Name;
        target.Kind = incoming.Kind;
        target.ParentId = incoming.ParentId;
        target.Description = incoming.Description;
        target.LinkedRoleName = incoming.LinkedRoleName;
        target.TimeZoneId = incoming.TimeZoneId;
        target.Enabled = incoming.Enabled;

        configure?.Invoke(node, target);

        var validation = await teamManager.ValidateAsync(target, cancellationToken);

        if (!validation.Succeeded)
        {
            errors.Add($"A team in the {stepName} step was refused: {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage))}");

            return null;
        }

        if (existing is null)
        {
            await teamManager.CreateAsync(target, cancellationToken);
        }
        else
        {
            await teamManager.UpdateAsync(target, cancellationToken: cancellationToken);
        }

        return target;
    }

    private static Team Read(JsonObject node, ICollection<string> errors, string stepName)
    {
        try
        {
            return node.Deserialize<Team>(JsonOptions);
        }
        catch (JsonException ex)
        {
            errors.Add($"A team in the {stepName} step can't be read: {ex.Message}");

            return null;
        }
    }

    private static string String(JsonObject node, string property)
    {
        return node[property] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;
    }
}
