using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Teams.Core.Services;
using CrestApps.OrchardCore.Teams.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Teams.Core.Handlers;

/// <summary>
/// Stamps teams, tidies their fields, and keeps the hierarchy a tree: a name is required, the parent must exist, a team
/// can't sit under itself, and the time zone must be known.
/// </summary>
internal sealed class TeamCatalogHandler : CatalogEntryHandlerBase<Team>
{
    private readonly TeamStore _store;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public TeamCatalogHandler(
        TeamStore store,
        IClock clock,
        IStringLocalizer<TeamCatalogHandler> stringLocalizer)
    {
        _store = store;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializedAsync(InitializedContext<Team> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override Task CreatingAsync(CreatingContext<Team> context, CancellationToken cancellationToken = default)
    {
        Tidy(context.Model);

        return Task.CompletedTask;
    }

    public override Task UpdatingAsync(UpdatingContext<Team> context, CancellationToken cancellationToken = default)
    {
        Tidy(context.Model);
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override async Task ValidatingAsync(ValidatingContext<Team> context, CancellationToken cancellationToken = default)
    {
        var team = context.Model;

        if (!string.IsNullOrWhiteSpace(team.TimeZoneId) && !TeamTimeZones.IsValid(team.TimeZoneId))
        {
            context.Result.Fail(new ValidationResult(S["'{0}' is not a time zone.", team.TimeZoneId], [nameof(Team.TimeZoneId)]));
        }

        Tidy(team);

        if (string.IsNullOrEmpty(team.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(Team.Name)]));
        }

        if (string.IsNullOrEmpty(team.ParentId))
        {
            return;
        }

        var teams = (await _store.ListAsync(cancellationToken)).ToDictionary(item => item.ItemId, StringComparer.Ordinal);

        if (!teams.ContainsKey(team.ParentId))
        {
            context.Result.Fail(new ValidationResult(S["The parent team doesn't exist."], [nameof(Team.ParentId)]));

            return;
        }

        // Walk up from the new parent: reaching the team itself means it would sit under itself.
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var current = team.ParentId; !string.IsNullOrEmpty(current) && seen.Add(current); current = teams.GetValueOrDefault(current)?.ParentId)
        {
            if (current == team.ItemId)
            {
                context.Result.Fail(new ValidationResult(S["A team can't be placed under itself or under one of its own teams."], [nameof(Team.ParentId)]));

                return;
            }
        }
    }

    private static void Tidy(Team team)
    {
        team.Name = team.Name?.Trim();
        team.Description = string.IsNullOrWhiteSpace(team.Description) ? null : team.Description.Trim();
        team.ParentId = string.IsNullOrWhiteSpace(team.ParentId) ? null : team.ParentId;
        team.LinkedRoleName = string.IsNullOrWhiteSpace(team.LinkedRoleName) ? null : team.LinkedRoleName.Trim();
        team.TimeZoneId = TeamTimeZones.Normalize(team.TimeZoneId);
    }
}
