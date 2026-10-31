using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Handlers;

/// <summary>
/// Stamps teams, keeps the hierarchy a tree, and gives each team its Workforce calendar.
/// </summary>
internal sealed class TeamCatalogHandler : CatalogEntryHandlerBase<WorkforceTeam>
{
    private readonly TeamStore _store;
    private readonly WorkforceCalendarService _calendarService;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public TeamCatalogHandler(
        TeamStore store,
        WorkforceCalendarService calendarService,
        IClock clock,
        IStringLocalizer<TeamCatalogHandler> stringLocalizer)
    {
        _store = store;
        _calendarService = calendarService;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializedAsync(InitializedContext<WorkforceTeam> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override async Task CreatingAsync(CreatingContext<WorkforceTeam> context, CancellationToken cancellationToken = default)
    {
        Tidy(context.Model);
        await _calendarService.EnsureCalendarAsync(context.Model, cancellationToken);
    }

    public override async Task UpdatingAsync(UpdatingContext<WorkforceTeam> context, CancellationToken cancellationToken = default)
    {
        Tidy(context.Model);
        context.Model.ModifiedUtc = _clock.UtcNow;
        await _calendarService.EnsureCalendarAsync(context.Model, cancellationToken);
    }

    public override async Task ValidatingAsync(ValidatingContext<WorkforceTeam> context, CancellationToken cancellationToken = default)
    {
        var team = context.Model;

        Tidy(team);

        if (string.IsNullOrEmpty(team.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(WorkforceTeam.Name)]));
        }

        if (!string.IsNullOrEmpty(team.TimeZoneId) && !CalendarTime.IsValidZone(team.TimeZoneId))
        {
            context.Result.Fail(new ValidationResult(S["'{0}' is not a time zone.", team.TimeZoneId], [nameof(WorkforceTeam.TimeZoneId)]));
        }

        if (string.IsNullOrEmpty(team.ParentId))
        {
            return;
        }

        var teams = (await _store.ListAsync(cancellationToken)).ToDictionary(item => item.ItemId, StringComparer.Ordinal);

        if (!teams.ContainsKey(team.ParentId))
        {
            context.Result.Fail(new ValidationResult(S["The parent team doesn't exist."], [nameof(WorkforceTeam.ParentId)]));

            return;
        }

        // Walk up from the new parent: reaching the team itself means it would sit under itself.
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var current = team.ParentId; !string.IsNullOrEmpty(current) && seen.Add(current); current = teams.GetValueOrDefault(current)?.ParentId)
        {
            if (current == team.ItemId)
            {
                context.Result.Fail(new ValidationResult(S["A team can't be placed under itself or under one of its own teams."], [nameof(WorkforceTeam.ParentId)]));

                return;
            }
        }
    }

    private static void Tidy(WorkforceTeam team)
    {
        team.Name = team.Name?.Trim();
        team.ParentId = string.IsNullOrWhiteSpace(team.ParentId) ? null : team.ParentId;
        team.LinkedRoleName = string.IsNullOrWhiteSpace(team.LinkedRoleName) ? null : team.LinkedRoleName.Trim();
        team.TimeZoneId = CalendarTime.NormalizeZoneId(team.TimeZoneId);
    }
}
