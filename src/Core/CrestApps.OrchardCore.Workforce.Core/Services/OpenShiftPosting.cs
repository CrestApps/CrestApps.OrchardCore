using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Keeps a schedule's open shifts in step with its draft shifts that have nobody, each time it is published.
/// </summary>
internal sealed class OpenShiftPosting
{
    private readonly OpenShiftStore _openShiftStore;
    private readonly ShiftTemplateStore _templateStore;
    private readonly TeamPlanningPolicy _policy;
    private readonly ShiftPlanningOptions _options;
    private readonly IClock _clock;

    public OpenShiftPosting(
        OpenShiftStore openShiftStore,
        ShiftTemplateStore templateStore,
        TeamPlanningPolicy policy,
        IOptions<ShiftPlanningOptions> options,
        IClock clock)
    {
        _openShiftStore = openShiftStore;
        _templateStore = templateStore;
        _policy = policy;
        _options = options.Value;
        _clock = clock;
    }

    /// <summary>
    /// Posts the open drafts that aren't posted yet, updates the posted ones that changed, and cancels the posted ones
    /// whose draft is gone or now has a worker. Filled open shifts are left alone.
    /// </summary>
    public async Task<int> SyncAsync(SchedulePeriod period, IReadOnlyList<DraftShift> openDrafts, string timeZoneId, string actor, CancellationToken cancellationToken = default)
    {
        var existing = (await _openShiftStore.ListForPeriodAsync(period.ItemId, cancellationToken))
            .Where(shift => !string.IsNullOrEmpty(shift.ShiftKey))
            .GroupBy(shift => shift.ShiftKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var policy = await _policy.ClaimPolicyAsync(period.TeamId, cancellationToken);
        var now = _clock.UtcNow;
        var posted = 0;

        foreach (var draft in openDrafts)
        {
            var template = string.IsNullOrEmpty(draft.TemplateId) ? null : await _templateStore.FindByIdAsync(draft.TemplateId, cancellationToken);
            var skills = template?.RequiredSkillIds.ToList() ?? [];

            if (existing.Remove(draft.ShiftKey, out var shift))
            {
                if (shift.Status is OpenShiftStatus.Filled or OpenShiftStatus.Canceled)
                {
                    // The shift has nobody again: offer it afresh.
                    shift.Status = OpenShiftStatus.Open;
                    shift.Claims = [];
                    shift.FilledWorkerId = null;
                    shift.FilledUtc = null;
                    shift.CalendarEventId = null;
                }

                Copy(draft, shift, skills, timeZoneId);
                await _openShiftStore.UpdateAsync(shift, cancellationToken);
                posted++;

                continue;
            }

            var created = new OpenShift
            {
                ItemId = UniqueId.GenerateId(),
                TeamId = period.TeamId,
                PeriodId = period.ItemId,
                ShiftKey = draft.ShiftKey,
                ClaimPolicy = policy,
                Status = OpenShiftStatus.Open,
                CreatedBy = actor,
                CreatedUtc = now,
            };

            Copy(draft, created, skills, timeZoneId);
            created.ClaimDeadlineUtc = Deadline(created, now);
            await _openShiftStore.CreateAsync(created, cancellationToken);
            posted++;
        }

        // What is left is no longer open in the schedule.
        foreach (var shift in existing.Values.Where(shift => shift.Status is OpenShiftStatus.Open or OpenShiftStatus.Claimed))
        {
            Cancel(shift, actor, now);
            await _openShiftStore.UpdateAsync(shift, cancellationToken);
        }

        return posted;
    }

    /// <summary>
    /// Returns when an open shift given by seniority is awarded: after the claim window, but before it starts.
    /// </summary>
    public DateTime? Deadline(OpenShift shift, DateTime nowUtc)
    {
        if (shift.ClaimPolicy != OpenShiftClaimPolicy.BySeniority)
        {
            return null;
        }

        var start = CalendarTime.ToUtc(shift.Start, shift.TimeZoneId);
        var deadline = nowUtc.AddHours(Math.Max(0, _options.SeniorityClaimHours));

        return deadline < start ? deadline : start;
    }

    public static void Cancel(OpenShift shift, string actor, DateTime nowUtc)
    {
        shift.Status = OpenShiftStatus.Canceled;

        foreach (var claim in shift.Claims.Where(claim => claim.Status == OpenShiftClaimStatus.Pending))
        {
            claim.Status = OpenShiftClaimStatus.Declined;
            claim.DecidedBy = actor;
            claim.DecidedUtc = nowUtc;
        }
    }

    private static void Copy(DraftShift draft, OpenShift shift, IList<string> skills, string timeZoneId)
    {
        shift.Date = draft.Date;
        shift.Start = draft.Start;
        shift.End = draft.End;
        shift.TimeZoneId = timeZoneId;
        shift.TemplateId = draft.TemplateId;
        shift.Segments = draft.Segments.Select(segment => new DraftShiftSegment { ActivityCodeId = segment.ActivityCodeId, Start = segment.Start, End = segment.End }).ToList();
        shift.RequiredSkillIds = skills;
        shift.Notes = draft.Notes;
    }
}
