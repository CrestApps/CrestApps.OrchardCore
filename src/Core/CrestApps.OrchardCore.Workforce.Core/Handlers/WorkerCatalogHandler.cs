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
/// Stamps workers, tidies their fields, and keeps their employee number and user their own.
/// </summary>
internal sealed class WorkerCatalogHandler : CatalogEntryHandlerBase<WorkerProfile>
{
    private readonly WorkerStore _store;
    private readonly TeamStore _teamStore;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public WorkerCatalogHandler(
        WorkerStore store,
        TeamStore teamStore,
        IClock clock,
        IStringLocalizer<WorkerCatalogHandler> stringLocalizer)
    {
        _store = store;
        _teamStore = teamStore;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializedAsync(InitializedContext<WorkerProfile> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override Task CreatingAsync(CreatingContext<WorkerProfile> context, CancellationToken cancellationToken = default)
    {
        Tidy(context.Model);

        return Task.CompletedTask;
    }

    public override Task UpdatingAsync(UpdatingContext<WorkerProfile> context, CancellationToken cancellationToken = default)
    {
        Tidy(context.Model);
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override async Task ValidatingAsync(ValidatingContext<WorkerProfile> context, CancellationToken cancellationToken = default)
    {
        var worker = context.Model;

        Tidy(worker);

        if (string.IsNullOrEmpty(worker.DisplayName))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(WorkerProfile.DisplayName)]));
        }

        if (!string.IsNullOrEmpty(worker.EmployeeNumber) && await _store.FindByEmployeeNumberAsync(worker.EmployeeNumber, cancellationToken) is { } numbered && numbered.ItemId != worker.ItemId)
        {
            context.Result.Fail(new ValidationResult(S["Another worker has the employee number '{0}'.", worker.EmployeeNumber], [nameof(WorkerProfile.EmployeeNumber)]));
        }

        if (!string.IsNullOrEmpty(worker.UserId) && await _store.FindByUserIdAsync(worker.UserId, cancellationToken) is { } linked && linked.ItemId != worker.ItemId)
        {
            context.Result.Fail(new ValidationResult(S["That user is already the worker '{0}'.", linked.DisplayName], [nameof(WorkerProfile.UserId)]));
        }

        if (!string.IsNullOrEmpty(worker.TimeZoneId) && !CalendarTime.IsValidZone(worker.TimeZoneId))
        {
            context.Result.Fail(new ValidationResult(S["'{0}' is not a time zone.", worker.TimeZoneId], [nameof(WorkerProfile.TimeZoneId)]));
        }

        if (worker.TeamMemberships.Any(membership => membership.EffectiveFrom is not null && membership.EffectiveTo is not null && membership.EffectiveTo < membership.EffectiveFrom))
        {
            context.Result.Fail(new ValidationResult(S["A team membership ends before it starts."], [nameof(WorkerProfile.TeamMemberships)]));
        }

        var teamIds = worker.TeamMemberships.Select(membership => membership.TeamId).Append(worker.PrimaryTeamId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (teamIds.Length > 0)
        {
            var known = (await _teamStore.GetAsync(teamIds, cancellationToken)).Select(team => team.ItemId).ToHashSet(StringComparer.Ordinal);

            if (teamIds.Any(id => !known.Contains(id)))
            {
                context.Result.Fail(new ValidationResult(S["A team the worker belongs to doesn't exist."], [nameof(WorkerProfile.TeamMemberships)]));
            }
        }

        if (worker.MinHoursPerWeek is { } min && worker.MaxHoursPerWeek is { } max && min > max)
        {
            context.Result.Fail(new ValidationResult(S["The fewest hours a week are more than the most."], [nameof(WorkerProfile.MinHoursPerWeek)]));
        }
    }

    private static void Tidy(WorkerProfile worker)
    {
        worker.DisplayName = worker.DisplayName?.Trim();
        worker.EmployeeNumber = string.IsNullOrWhiteSpace(worker.EmployeeNumber) ? null : worker.EmployeeNumber.Trim();
        worker.Email = string.IsNullOrWhiteSpace(worker.Email) ? null : worker.Email.Trim();
        worker.UserId = string.IsNullOrWhiteSpace(worker.UserId) ? null : worker.UserId.Trim();
        worker.TimeZoneId = CalendarTime.NormalizeZoneId(worker.TimeZoneId);
        worker.TeamMemberships = (worker.TeamMemberships ?? []).Where(membership => !string.IsNullOrEmpty(membership?.TeamId)).ToList();
        worker.Availability ??= [];

        if (string.IsNullOrEmpty(worker.PrimaryTeamId))
        {
            worker.PrimaryTeamId = worker.TeamMemberships.FirstOrDefault()?.TeamId;
        }
    }
}
