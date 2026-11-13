using System.Globalization;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.Workforce.Models;
using IClock = OrchardCore.Modules.IClock;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Dispatch.Core.Services;

/// <summary>
/// Finds the workers who can take a visit and ranks them by the dispatch objective.
/// </summary>
public sealed class DefaultDispatchCandidateFinder : IDispatchCandidateFinder
{
    /// <summary>
    /// The name of the travel part of a score.
    /// </summary>
    public const string TravelPart = "Travel";

    /// <summary>
    /// The name of the urgency part of a score.
    /// </summary>
    public const string UrgencyPart = "Urgency";

    /// <summary>
    /// The name of the proficiency part of a score.
    /// </summary>
    public const string ProficiencyPart = "Proficiency";

    /// <summary>
    /// The name of the preferred worker part of a score.
    /// </summary>
    public const string PreferredPart = "Preferred";

    /// <summary>
    /// The name of the workload balance part of a score.
    /// </summary>
    public const string BalancePart = "Balance";

    private readonly DispatchResources _resources;
    private readonly SlotFitter _fitter;
    private readonly WorkOrderStore _workOrderStore;
    private readonly WorkOrderSettingManager<WorkOrderType> _types;
    private readonly ISkillAssignmentService _skillAssignments;
    private readonly ISkillManager _skills;
    private readonly ICertificationRecordService _certificationRecords;
    private readonly ICertificationManager _certifications;
    private readonly Geo.Core.Services.ServiceTerritoryStore _territories;
    private readonly IDispatchSettingsProvider _settingsProvider;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultDispatchCandidateFinder"/> class.
    /// </summary>
    public DefaultDispatchCandidateFinder(
        DispatchResources resources,
        SlotFitter fitter,
        WorkOrderStore workOrderStore,
        WorkOrderSettingManager<WorkOrderType> types,
        ISkillAssignmentService skillAssignments,
        ISkillManager skills,
        ICertificationRecordService certificationRecords,
        ICertificationManager certifications,
        Geo.Core.Services.ServiceTerritoryStore territories,
        IDispatchSettingsProvider settingsProvider,
        IClock clock,
        IStringLocalizer<DefaultDispatchCandidateFinder> stringLocalizer)
    {
        _resources = resources;
        _fitter = fitter;
        _workOrderStore = workOrderStore;
        _types = types;
        _skillAssignments = skillAssignments;
        _skills = skills;
        _certificationRecords = certificationRecords;
        _certifications = certifications;
        _territories = territories;
        _settingsProvider = settingsProvider;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<DispatchCandidateResult> FindAsync(ServiceVisit visit, DateTime fromUtc, DateTime toUtc, DispatchObjective objective = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(visit);

        return await FindAsync(visit, await _workOrderStore.FindByIdAsync(visit.WorkOrderId, cancellationToken), fromUtc, toUtc, objective, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<DispatchCandidateResult> FindAsync(ServiceVisit visit, WorkOrder workOrder, DateTime fromUtc, DateTime toUtc, DispatchObjective objective = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(visit);

        var settings = await _settingsProvider.GetAsync();

        objective ??= settings.Objective ?? new DispatchObjective();

        var type = string.IsNullOrEmpty(visit.TypeId) ? null : await _types.FindByIdAsync(visit.TypeId, cancellationToken);
        var from = Max(Max(fromUtc, _clock.UtcNow), workOrder?.EarliestUtc ?? DateTime.MinValue);
        var to = workOrder?.LatestUtc is { } latest && latest < toUtc ? latest : toUtc;
        var candidates = new List<DispatchCandidate>();
        var excluded = new List<DispatchExclusion>();

        if (to <= from)
        {
            var zone = await _settingsProvider.GetTimeZoneIdAsync();

            zone = Calendars.CalendarTime.IsValidZone(zone) ? zone : "UTC";

            return new DispatchCandidateResult
            {
                Message = workOrder?.EarliestUtc is { } earliest && workOrder.LatestUtc is { } window
                    ? S["The work order is to be done from {0} to {1}.", Calendars.CalendarTime.ToLocal(earliest, zone).ToString("ddd d MMM HH:mm", CultureInfo.CurrentCulture), Calendars.CalendarTime.ToLocal(window, zone).ToString("ddd d MMM HH:mm", CultureInfo.CurrentCulture)]
                    : S["That time has passed."],
            };
        }

        var here = GeoPoint.From(workOrder?.ServiceAddress?.Latitude, workOrder?.ServiceAddress?.Longitude);
        var requiredSkills = workOrder?.RequiredSkills ?? [];
        var requiredCertifications = type?.RequiredCertificationIds ?? [];
        var territory = string.IsNullOrEmpty(visit.TerritoryId) ? null : await _territories.FindByIdAsync(visit.TerritoryId, cancellationToken);
        var territoryHasMembers = territory?.Members.Count > 0;

        foreach (var worker in await _resources.ListWorkersAsync(cancellationToken))
        {
            var reason = await RuleOutAsync(worker, workOrder, requiredSkills, requiredCertifications, territory, territoryHasMembers && settings.StrictTerritory, from, cancellationToken);

            if (reason is not null)
            {
                excluded.Add(new DispatchExclusion(worker.ItemId, worker.DisplayName, reason));

                continue;
            }

            var availability = await _resources.GetAvailabilityAsync(worker, from, to, visit.ItemId, cancellationToken);

            if (!availability.HasShift)
            {
                excluded.Add(new DispatchExclusion(worker.ItemId, worker.DisplayName, S["Has no shift then."]));

                continue;
            }

            var fit = await _fitter.FindEarliestAsync(availability, here, visit.DurationMinutes, from, to, settings.BufferMinutes, cancellationToken);

            if (fit is null)
            {
                excluded.Add(new DispatchExclusion(
                    worker.ItemId,
                    worker.DisplayName,
                    availability.Open.Count == 0 && availability.TimeOff.Count > 0 ? S["Is on time off then."] : S["Isn't free for {0} minutes on shift then.", visit.DurationMinutes]));

                continue;
            }

            var candidate = new DispatchCandidate
            {
                WorkerId = worker.ItemId,
                Name = worker.DisplayName,
                StartUtc = fit.StartUtc,
                EndUtc = fit.EndUtc,
                TravelMinutes = fit.TravelMinutes,
                TravelKm = fit.TravelKm,
            };

            await ScoreAsync(candidate, availability, workOrder, requiredSkills, objective, from, to, cancellationToken);

            if (territoryHasMembers && !IsMember(territory, worker))
            {
                candidate.Warnings.Add(S["Works outside the {0} territory.", territory.Name]);
            }

            if (visit.DueUtc is { } due && candidate.EndUtc > due)
            {
                candidate.Warnings.Add(S["Ends after the work order is due, {0}.", CalendarTime.ToLocal(due, availability.TimeZoneId).ToString("ddd d MMM HH:mm", CultureInfo.CurrentCulture)]);
            }

            if (here is null)
            {
                candidate.Warnings.Add(S["The address isn't on the map yet, so travel isn't counted."]);
            }
            else if (!fit.TravelKnown)
            {
                candidate.Warnings.Add(S["Travel isn't counted: where {0} comes from isn't on the map. Give them a home base, or their team a place.", worker.DisplayName]);
            }

            candidates.Add(candidate);
        }

        return new DispatchCandidateResult
        {
            Candidates = candidates.OrderByDescending(candidate => candidate.Score).ThenBy(candidate => candidate.StartUtc).ThenBy(candidate => candidate.Name, StringComparer.CurrentCulture).ToArray(),
            Excluded = excluded.OrderBy(exclusion => exclusion.Name, StringComparer.CurrentCulture).ToArray(),
        };
    }

    /// <inheritdoc/>
    public async Task<string> CheckWorkerAsync(ServiceVisit visit, string workerId, DateOnly date, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(visit);

        var worker = await _resources.FindWorkerAsync(workerId, cancellationToken);

        if (worker is null || worker.Status != StaffStatus.Active)
        {
            return S["The worker doesn't exist or doesn't work here any more."];
        }

        var settings = await _settingsProvider.GetAsync();
        var workOrder = await _workOrderStore.FindByIdAsync(visit.WorkOrderId, cancellationToken);
        var type = string.IsNullOrEmpty(visit.TypeId) ? null : await _types.FindByIdAsync(visit.TypeId, cancellationToken);
        var territory = string.IsNullOrEmpty(visit.TerritoryId) ? null : await _territories.FindByIdAsync(visit.TerritoryId, cancellationToken);

        return await RuleOutAsync(worker, workOrder, workOrder?.RequiredSkills ?? [], type?.RequiredCertificationIds ?? [], territory, territory?.Members.Count > 0 && settings.StrictTerritory, date.ToDateTime(TimeOnly.MinValue), cancellationToken);
    }

    private async Task<string> RuleOutAsync(StaffMember worker, WorkOrder workOrder, List<RequiredSkill> requiredSkills, List<string> requiredCertifications, Geo.Models.ServiceTerritory territory, bool strictTerritory, DateTime fromUtc, CancellationToken cancellationToken)
    {
        if (workOrder?.ExcludedWorkerIds.Contains(worker.ItemId) == true)
        {
            return S["The work order leaves them out."];
        }

        var subject = SkillSubject.Staff(worker.ItemId);
        var date = DateOnly.FromDateTime(fromUtc);

        foreach (var required in requiredSkills.Where(required => !string.IsNullOrEmpty(required.SkillId)))
        {
            if (!await _skillAssignments.HoldsAsync(subject, required.SkillId, required.MinProficiency, date, cancellationToken))
            {
                var skill = await _skills.FindByIdAsync(required.SkillId, cancellationToken);
                var held = (await _skillAssignments.GetAssignmentsAsync(subject, cancellationToken))
                    .Any(assignment => assignment.SkillId == required.SkillId && assignment.Proficiency >= required.MinProficiency && assignment.IsEffectiveOn(date));

                // The skill is held but only counts with a certificate that isn't valid that day.
                if (held && !string.IsNullOrEmpty(skill?.RequiredCertificationId))
                {
                    var certification = await _certifications.FindByIdAsync(skill.RequiredCertificationId, cancellationToken);

                    return S["{0} needs {1}, which isn't valid on {2}.", skill.Name, certification?.Name ?? skill.RequiredCertificationId, date.ToString("d MMM yyyy", CultureInfo.CurrentCulture)];
                }

                return S["Doesn't have {0} at level {1}.", skill?.Name ?? required.SkillId, required.MinProficiency];
            }
        }

        foreach (var certificationId in requiredCertifications.Where(id => !string.IsNullOrEmpty(id)))
        {
            if (!await _certificationRecords.IsValidOnAsync(subject, certificationId, date, cancellationToken))
            {
                var certification = await _certifications.FindByIdAsync(certificationId, cancellationToken);

                return S["{0} isn't valid on {1}.", certification?.Name ?? certificationId, date.ToString("d MMM yyyy", CultureInfo.CurrentCulture)];
            }
        }

        if (strictTerritory && !IsMember(territory, worker))
        {
            return S["Doesn't work in the {0} territory.", territory.Name];
        }

        return null;
    }

    private async Task ScoreAsync(DispatchCandidate candidate, WorkerAvailability availability, WorkOrder workOrder, List<RequiredSkill> requiredSkills, DispatchObjective objective, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        // Each part is between 0 and its weight.
        var parts = candidate.ScoreParts;

        parts[TravelPart] = objective.TravelWeight * (1 - (Math.Min(candidate.TravelMinutes, 120) / 120d));

        var span = (toUtc - fromUtc).TotalMinutes;

        parts[UrgencyPart] = span <= 0 ? objective.UrgencyWeight : objective.UrgencyWeight * (1 - Math.Clamp((candidate.StartUtc - fromUtc).TotalMinutes / span, 0, 1));

        if (requiredSkills.Count > 0 && objective.ProficiencyWeight > 0)
        {
            var held = await _skillAssignments.GetEffectiveAsync(SkillSubject.Staff(candidate.WorkerId), DateOnly.FromDateTime(candidate.StartUtc), cancellationToken);
            var share = requiredSkills.Average(required =>
            {
                var level = held.Where(assignment => assignment.SkillId == required.SkillId).Select(assignment => assignment.Proficiency).DefaultIfEmpty(required.MinProficiency).Max();
                var room = SkillProficiency.Maximum - required.MinProficiency;

                return room <= 0 ? 1 : Math.Clamp((level - required.MinProficiency) / (double)room, 0, 1);
            });

            parts[ProficiencyPart] = objective.ProficiencyWeight * share;
        }
        else
        {
            parts[ProficiencyPart] = 0;
        }

        parts[PreferredPart] = workOrder?.PreferredWorkerIds.Contains(candidate.WorkerId) == true ? objective.PreferredWeight : 0;

        var sameDay = availability.Visits.Count(visit => visit.StartUtc?.Date == candidate.StartUtc.Date);

        parts[BalancePart] = objective.BalanceWeight * (1 - (Math.Min(sameDay, 8) / 8d));

        foreach (var key in parts.Keys.ToArray())
        {
            parts[key] = Math.Round(parts[key], 2);
        }

        candidate.Score = Math.Round(parts.Values.Sum(), 2);
    }

    private static bool IsMember(Geo.Models.ServiceTerritory territory, StaffMember worker)
    {
        if (territory is null)
        {
            return true;
        }

        var teams = worker.TeamMemberships.Select(membership => membership.TeamId).ToHashSet(StringComparer.Ordinal);

        return territory.Members.Any(member =>
            (string.Equals(member.Kind, DispatchConstants.ResourceKinds.Worker, StringComparison.OrdinalIgnoreCase) && member.Id == worker.ItemId) ||
            (string.Equals(member.Kind, "Team", StringComparison.OrdinalIgnoreCase) && teams.Contains(member.Id)));
    }

    private static DateTime Max(DateTime first, DateTime second) => first > second ? first : second;
}
