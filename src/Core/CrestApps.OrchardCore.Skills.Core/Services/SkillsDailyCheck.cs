using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Skills.Core.Services;

/// <summary>
/// The once-a-day pass: raises each certificate reminder once per expiry date, marks certificates past their expiry
/// date as expired, and tells the assignment handlers about assignments that start or end that day.
/// </summary>
internal sealed class SkillsDailyCheck
{
    private readonly CertificationRecordStore _recordStore;
    private readonly CertificationStore _certificationStore;
    private readonly SkillAssignmentStore _assignmentStore;
    private readonly IEnumerable<ICertificationExpiryObserver> _observers;
    private readonly IEnumerable<ISkillAssignmentHandler> _handlers;
    private readonly ILogger _logger;

    public SkillsDailyCheck(
        CertificationRecordStore recordStore,
        CertificationStore certificationStore,
        SkillAssignmentStore assignmentStore,
        IEnumerable<ICertificationExpiryObserver> observers,
        IEnumerable<ISkillAssignmentHandler> handlers,
        ILogger<SkillsDailyCheck> logger)
    {
        _recordStore = recordStore;
        _certificationStore = certificationStore;
        _assignmentStore = assignmentStore;
        _observers = observers;
        _handlers = handlers;
        _logger = logger;
    }

    public async Task RunAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        await CheckCertificatesAsync(today, cancellationToken);
        await CheckAssignmentsAsync(today, cancellationToken);
    }

    private async Task CheckCertificatesAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var certifications = (await _certificationStore.GetAllAsync(cancellationToken)).ToDictionary(certification => certification.ItemId, StringComparer.Ordinal);
        var lookAhead = certifications.Values.SelectMany(certification => certification.ReminderDays).DefaultIfEmpty(0).Max();
        var records = await _recordStore.ListExpiringAsync(SkillsDates.ToKey(today.AddDays(lookAhead)), cancellationToken);

        foreach (var record in records)
        {
            if (record.ExpiresOn is not { } expiresOn || !certifications.TryGetValue(record.CertificationId, out var certification))
            {
                continue;
            }

            var changed = false;

            if (record.RemindersExpiresOn != expiresOn)
            {
                record.RemindersRaised = [];
                record.RemindersExpiresOn = expiresOn;
                changed = true;
            }

            var daysLeft = expiresOn.DayNumber - today.DayNumber;

            if (daysLeft < 0)
            {
                record.Status = CertificationStatus.Expired;
                await _recordStore.UpdateAsync(record, cancellationToken);

                foreach (var observer in _observers)
                {
                    await NotifyAsync(() => observer.ExpiredAsync(record, certification, cancellationToken), record);
                }

                continue;
            }

            // Every offset already reached is marked raised, and only the nearest one is announced, so a certificate
            // entered a week before it expires gets one reminder rather than one per offset.
            var due = certification.ReminderDays.Where(days => days >= daysLeft && !record.RemindersRaised.Contains(days)).ToArray();

            if (due.Length > 0)
            {
                foreach (var days in due)
                {
                    record.RemindersRaised.Add(days);
                }

                await _recordStore.UpdateAsync(record, cancellationToken);

                var nearest = due.Min();

                foreach (var observer in _observers)
                {
                    await NotifyAsync(() => observer.ExpiringAsync(record, certification, nearest, cancellationToken), record);
                }

                continue;
            }

            if (changed)
            {
                await _recordStore.UpdateAsync(record, cancellationToken);
            }
        }
    }

    private async Task CheckAssignmentsAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var moving = await _assignmentStore.ListStartingOrEndingAsync(SkillsDates.ToKey(today), SkillsDates.ToKey(today.AddDays(-1)), cancellationToken);

        foreach (var group in moving.GroupBy(assignment => assignment.Subject))
        {
            var all = await _assignmentStore.ListForSubjectAsync(group.Key, cancellationToken);
            var context = new SkillAssignmentsChangedContext
            {
                Subject = group.Key,
                Assignments = all,
                Changed = group.ToArray(),
                Actor = nameof(SkillsDailyCheck),
                EffectiveDatesOnly = true,
            };

            foreach (var handler in _handlers)
            {
                await handler.AssignmentsChangedAsync(context, cancellationToken);
            }
        }
    }

    private async Task NotifyAsync(Func<Task> notify, CertificationRecord record)
    {
        try
        {
            await notify();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "A certification expiry observer failed for the certification record '{RecordId}'.", record.ItemId);
        }
    }
}
