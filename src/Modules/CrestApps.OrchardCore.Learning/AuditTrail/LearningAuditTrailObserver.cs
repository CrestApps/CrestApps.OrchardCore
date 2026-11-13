using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;

namespace CrestApps.OrchardCore.Learning.AuditTrail;

/// <summary>
/// Writes the changes people make to enrollments by hand, and every export of learner records, to the Audit Trail.
/// Changes Learning makes on its own (a learner's progress, expiring access) are not audit events.
/// </summary>
internal sealed class LearningAuditTrailObserver : IEnrollmentObserver, ILearnerRecordExportObserver
{
    private readonly IAuditTrailManager _auditTrailManager;

    public LearningAuditTrailObserver(IAuditTrailManager auditTrailManager)
    {
        _auditTrailManager = auditTrailManager;
    }

    public Task StatusChangedAsync(Enrollment enrollment, EnrollmentStatus previous, CancellationToken cancellationToken = default)
    {
        var change = enrollment.History.Count > 0 ? enrollment.History[enrollment.History.Count - 1] : null;

        // Only changes made by a person with a reason are audit events; a learner completing a course is not.
        if (change is null || string.IsNullOrEmpty(change.Reason))
        {
            return Task.CompletedTask;
        }

        return RecordAsync(LearningAuditTrailEvents.StatusChanged, enrollment.ItemId, change.Actor, new LearningAuditData
        {
            EnrollmentId = enrollment.ItemId,
            Number = enrollment.Number,
            UserId = enrollment.UserId,
            CourseId = enrollment.CourseId,
            From = previous.ToString(),
            To = enrollment.Status.ToString(),
            Reason = change.Reason,
        });
    }

    public Task ExtendedAsync(Enrollment enrollment, string actor, string reason, CancellationToken cancellationToken = default)
        => RecordAsync(LearningAuditTrailEvents.AccessChanged, enrollment.ItemId, actor, new LearningAuditData
        {
            EnrollmentId = enrollment.ItemId,
            Number = enrollment.Number,
            UserId = enrollment.UserId,
            CourseId = enrollment.CourseId,
            To = $"{enrollment.AccessEndsUtc:O} / {enrollment.DueUtc:O}",
            Reason = reason,
        });

    public Task ItemCompletedAsync(Enrollment enrollment, CourseItemProgress item, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(item.CompletedBy))
        {
            return Task.CompletedTask;
        }

        return RecordAsync(LearningAuditTrailEvents.ItemCompletedByHand, enrollment.ItemId, item.CompletedBy, new LearningAuditData
        {
            EnrollmentId = enrollment.ItemId,
            Number = enrollment.Number,
            UserId = enrollment.UserId,
            CourseId = enrollment.CourseId,
            ItemId = item.ContentItemId,
            Reason = item.CompletionReason,
        });
    }

    public Task ExportedAsync(string kind, int count, string actor, string filter, CancellationToken cancellationToken = default)
        => RecordAsync(LearningAuditTrailEvents.LearnerRecordsExported, kind, actor, new LearningAuditData
        {
            Reason = $"{kind}: {count} records. {filter}".Trim(),
        });

    private Task RecordAsync(string name, string correlationId, string actor, LearningAuditData data)
        => _auditTrailManager.RecordEventAsync(new AuditTrailContext<LearningAuditData>(name, LearningConstants.AuditTrailCategory, correlationId, actor, actor, data));
}
