using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Decides whether a course item is open to a learner: the course must have started (for a learner who has not completed
/// it), with "Learners take the items in order" every required item before it must be complete, and every rule of its
/// section and every rule of the item must be met. A rule whose name no registered rule
/// answers keeps the item locked. Every path that opens an item or records progress on it (the player, the progress
/// service, package runtime, surveys, coursework, classes) asks it, so none of them opens an item before the start.
/// </summary>
public sealed class CourseReleaseEvaluator
{
    private readonly IEnumerable<ICourseItemReleaseRule> _rules;
    private readonly ICourseStructureService _courseStructure;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CourseReleaseEvaluator"/> class.
    /// </summary>
    /// <param name="rules">The registered release rules.</param>
    /// <param name="courseStructure">The course structure, to read the course's start date.</param>
    /// <param name="clock">The clock.</param>
    public CourseReleaseEvaluator(
        IEnumerable<ICourseItemReleaseRule> rules,
        ICourseStructureService courseStructure,
        IClock clock)
    {
        _rules = rules;
        _courseStructure = courseStructure;
        _clock = clock;
    }

    /// <summary>
    /// Decides whether an outline entry is open to a learner.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="enrollment">The enrollment.</param>
    /// <param name="progress">The progress of the enrollment.</param>
    /// <returns>The decision of the first rule not met, or <see cref="ReleaseDecision.Released"/>.</returns>
    public async Task<ReleaseDecision> EvaluateAsync(CourseOutlineEntry entry, Enrollment enrollment, EnrollmentProgress progress)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var now = _clock.UtcNow;
        var course = enrollment is not null && enrollment.Status != EnrollmentStatus.Completed && !string.IsNullOrEmpty(enrollment.CourseId)
            ? await _courseStructure.GetCourseAsync(enrollment.CourseId)
            : null;
        var coursePart = course?.Get<CoursePart>(nameof(CoursePart));

        // Before the course starts, an enrolled learner sees it but opens none of its items; a learner who completed it
        // may review it.
        if (coursePart is not null && CourseSchedules.Of(coursePart, now) == CourseSchedule.NotStarted)
        {
            return ReleaseDecision.Locked(LearningRefusals.CourseNotStarted, coursePart.StartsUtc);
        }

        // With "Learners take the items in order", an item waits for every required item before it in the outline.
        if (coursePart?.ItemsInOrder == true && await FirstUnfinishedBeforeAsync(course, entry, progress) is { } blocking)
        {
            return ReleaseDecision.WaitsFor(LearningRefusals.CompleteEarlierItem, blocking);
        }

        var settings = (entry.Section?.ReleaseRules ?? []).Concat(entry.Part?.ReleaseRules ?? []);

        foreach (var setting in settings)
        {
            if (setting is null || string.IsNullOrEmpty(setting.Name))
            {
                continue;
            }

            var rule = _rules.FirstOrDefault(candidate => string.Equals(candidate.Name, setting.Name, StringComparison.OrdinalIgnoreCase));

            if (rule is null)
            {
                return ReleaseDecision.Locked(LearningRefusals.NotAvailable);
            }

            var decision = await rule.EvaluateAsync(new ReleaseRuleContext
            {
                Settings = setting,
                Enrollment = enrollment,
                Progress = progress ?? new EnrollmentProgress(),
                Item = entry.Item,
                UtcNow = now,
            });

            if (!decision.IsReleased)
            {
                return decision;
            }
        }

        return ReleaseDecision.Released;
    }

    private async Task<string> FirstUnfinishedBeforeAsync(ContentItem course, CourseOutlineEntry entry, EnrollmentProgress progress)
    {
        var outline = await _courseStructure.GetOutlineAsync(course);
        var itemId = entry.Item?.ContentItemId;

        foreach (var earlier in outline.Entries)
        {
            if (string.Equals(earlier.Item.ContentItemId, itemId, StringComparison.Ordinal))
            {
                return null;
            }

            if (earlier.Required && !earlier.Inline && progress?.Find(earlier.Item.ContentItemId)?.State != CourseItemState.Completed)
            {
                return earlier.Item.ContentItemId;
            }
        }

        // An entry that is not in the outline is not decided here.
        return null;
    }
}
