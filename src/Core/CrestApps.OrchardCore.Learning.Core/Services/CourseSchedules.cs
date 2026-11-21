using CrestApps.OrchardCore.Learning.Models;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// Reads a course's start and end dates. Before the start, learners see the course and may enroll but cannot open its
/// items; after the end the course stays open, as in Moodle, and is shown as ended instead of in progress.
/// </summary>
public static class CourseSchedules
{
    /// <summary>
    /// Gets where a course is between its start and end dates.
    /// </summary>
    /// <param name="part">The course part, or <see langword="null"/>.</param>
    /// <param name="utcNow">The time now, in UTC.</param>
    /// <returns>Whether the course has not started, is open or has ended.</returns>
    public static CourseSchedule Of(CoursePart part, DateTime utcNow)
    {
        if (part?.StartsUtc is { } starts && starts > utcNow)
        {
            return CourseSchedule.NotStarted;
        }

        if (part?.EndsUtc is { } ends && ends <= utcNow)
        {
            return CourseSchedule.Ended;
        }

        return CourseSchedule.Open;
    }
}
