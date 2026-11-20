using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Builds the self-contained completion snapshot of an enrollment from the course and its outline as they are now.
/// </summary>
public static class CompletionSnapshotFactory
{
    /// <summary>
    /// Builds a snapshot.
    /// </summary>
    /// <param name="outline">The outline of the course.</param>
    /// <param name="progress">The progress of the enrollment, or <see langword="null"/>.</param>
    /// <param name="completedUtc">When the enrollment was completed, in UTC.</param>
    /// <param name="grade">The course grade.</param>
    /// <param name="passed">Whether the grade met the pass mark.</param>
    /// <param name="completedBy">The person who completed it by hand, or <see langword="null"/>.</param>
    /// <param name="reason">Why it was completed by hand.</param>
    /// <returns>The snapshot.</returns>
    public static CompletionSnapshot Create(CourseOutline outline, EnrollmentProgress progress, DateTime completedUtc, decimal? grade, bool? passed, string completedBy = null, string reason = null)
    {
        ArgumentNullException.ThrowIfNull(outline);

        var course = outline.Course;
        var snapshot = new CompletionSnapshot
        {
            CourseTitle = course?.DisplayText,
            CourseCode = course?.Get<CoursePart>(nameof(CoursePart))?.Code,
            CourseVersionId = course?.ContentItemVersionId,
            CompletedUtc = completedUtc,
            Grade = grade,
            Passed = passed,
            SecondsSpent = progress?.Items.Sum(item => item.SecondsSpent) ?? 0,
            CompletedBy = completedBy,
            Reason = reason,
        };

        // Text shown in place is not a step of the course, so the record leaves it out.
        foreach (var entry in outline.Entries.Where(entry => !entry.Inline))
        {
            var itemProgress = progress?.Find(entry.Item.ContentItemId);

            snapshot.Items.Add(new CompletionSnapshotItem
            {
                ContentItemId = entry.Item.ContentItemId,
                Title = entry.Item.DisplayText,
                Kind = entry.Part?.Kind,
                Section = entry.Section?.Title,
                Required = entry.Required,
                State = itemProgress?.State ?? CourseItemState.NotStarted,
                Score = itemProgress?.Score,
                SelfReported = itemProgress?.SelfReported ?? false,
                ResultSelfReported = itemProgress?.ResultSelfReported ?? false,
                AllowSelfReported = entry.Part?.AllowSelfReported ?? false,
                CompletedUtc = itemProgress?.CompletedUtc,
            });
        }

        return snapshot;
    }
}
