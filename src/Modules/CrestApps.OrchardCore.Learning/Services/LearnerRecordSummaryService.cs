using System.Globalization;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.ViewModels;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// Builds the Learning section that the user editor and the staff profile show: a person's enrollments, newest first,
/// with times in the viewer's time zone.
/// </summary>
internal sealed class LearnerRecordSummaryService
{
    private const int MaxRows = 50;

    private readonly EnrollmentStore _enrollmentStore;
    private readonly ICourseStructureService _courseStructure;
    private readonly ILocalClock _localClock;

    public LearnerRecordSummaryService(
        EnrollmentStore enrollmentStore,
        ICourseStructureService courseStructure,
        ILocalClock localClock)
    {
        _enrollmentStore = enrollmentStore;
        _courseStructure = courseStructure;
        _localClock = localClock;
    }

    /// <summary>
    /// Builds the section of a user.
    /// </summary>
    /// <param name="model">The view model to fill.</param>
    /// <param name="userId">The user id.</param>
    /// <returns>A task.</returns>
    public async Task FillAsync(LearnerRecordSectionViewModel model, string userId)
    {
        model.UserId = userId;

        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        var enrollments = (await _enrollmentStore.ListByUserAsync(userId))
            .OrderByDescending(enrollment => enrollment.CreatedUtc)
            .ToArray();

        model.Total = enrollments.Length;
        model.Completed = enrollments.Count(enrollment => enrollment.Status == EnrollmentStatus.Completed);
        model.Open = enrollments.Count(enrollment => enrollment.IsOpen);

        foreach (var enrollment in enrollments.Take(MaxRows))
        {
            model.Rows.Add(new LearnerRecordRowViewModel
            {
                EnrollmentId = enrollment.ItemId,
                Number = enrollment.Number,
                CourseTitle = (await _courseStructure.GetCourseAsync(enrollment.CourseId))?.DisplayText ?? enrollment.Completion?.CourseTitle ?? enrollment.CourseId,
                Status = enrollment.Status,
                ProgressPercent = enrollment.ProgressPercent,
                Due = await FormatAsync(enrollment.DueUtc),
                Completed = await FormatAsync(enrollment.CompletedUtc),
            });
        }
    }

    private async Task<string> FormatAsync(DateTime? utc)
    {
        if (utc is not { } value)
        {
            return null;
        }

        return (await _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)))).ToString("d", CultureInfo.CurrentCulture);
    }
}
