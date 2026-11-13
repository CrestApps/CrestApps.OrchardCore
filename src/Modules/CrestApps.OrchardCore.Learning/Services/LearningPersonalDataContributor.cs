using System.Globalization;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.PersonalData;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Exports a person's enrollments and progress, and erases them on request. A completed enrollment proves training and
/// is a compliance record, so erasure keeps it with its progress (the confirmations it holds are evidence) and reports
/// it as a warning; an administrator decides whether to remove it. Every other enrollment and its progress are deleted.
/// </summary>
internal sealed class LearningPersonalDataContributor : IPersonalDataContributor
{
    private const string EnrollmentKind = "LearningEnrollment";
    private const string ProgressKind = "LearningProgress";

    private readonly EnrollmentStore _enrollmentStore;
    private readonly EnrollmentProgressStore _progressStore;

    public LearningPersonalDataContributor(
        EnrollmentStore enrollmentStore,
        EnrollmentProgressStore progressStore)
    {
        _enrollmentStore = enrollmentStore;
        _progressStore = progressStore;
    }

    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        foreach (var userId in context.Subject.UserIds)
        {
            var enrollments = await _enrollmentStore.ListByUserAsync(userId, cancellationToken);
            var progress = (await _progressStore.ListByEnrollmentsAsync(enrollments.Select(enrollment => enrollment.ItemId).ToArray(), cancellationToken))
                .ToDictionary(item => item.EnrollmentId, StringComparer.Ordinal);

            foreach (var enrollment in enrollments)
            {
                var record = context.Add(EnrollmentKind, enrollment.ItemId, enrollment.Completion?.CourseTitle ?? enrollment.CourseId, enrollment.CreatedUtc);

                record.Fields["Number"] = enrollment.Number;
                record.Fields["Course"] = enrollment.CourseId;
                record.Fields["Status"] = enrollment.Status.ToString();
                record.Fields["Progress"] = enrollment.ProgressPercent.ToString(CultureInfo.InvariantCulture);
                record.Fields["Grade"] = enrollment.Grade?.ToString(CultureInfo.InvariantCulture);
                record.Fields["Completed"] = enrollment.CompletedUtc?.ToString("O", CultureInfo.InvariantCulture);

                if (progress.TryGetValue(enrollment.ItemId, out var items))
                {
                    foreach (var item in items.Items)
                    {
                        var itemRecord = context.Add(ProgressKind, enrollment.ItemId + ":" + item.ContentItemId, item.ContentItemId, item.FirstAccessUtc);

                        itemRecord.Fields["State"] = item.State.ToString();
                        itemRecord.Fields["SecondsSpent"] = item.SecondsSpent.ToString(CultureInfo.InvariantCulture);
                        itemRecord.Fields["Score"] = item.Score?.ToString(CultureInfo.InvariantCulture);
                        itemRecord.Fields["Confirmed"] = item.AttestedStatement;
                    }
                }
            }
        }
    }

    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        foreach (var userId in context.Subject.UserIds)
        {
            var enrollments = await _enrollmentStore.ListByUserAsync(userId, cancellationToken);
            var kept = 0;

            foreach (var enrollment in enrollments)
            {
                if (enrollment.Status == EnrollmentStatus.Completed)
                {
                    kept++;

                    continue;
                }

                var progress = await _progressStore.FindByEnrollmentAsync(enrollment.ItemId, cancellationToken);

                if (progress is not null)
                {
                    await _progressStore.DeleteAsync(progress, cancellationToken);
                    context.Result.Count(ProgressKind);
                }

                await _enrollmentStore.DeleteAsync(enrollment, cancellationToken);
                context.Result.Count(EnrollmentKind);
            }

            if (kept > 0)
            {
                context.Result.Warnings.Add($"{kept} completed enrollments were kept: they prove training and are compliance records. An administrator decides whether to remove them.");
            }
        }
    }
}
