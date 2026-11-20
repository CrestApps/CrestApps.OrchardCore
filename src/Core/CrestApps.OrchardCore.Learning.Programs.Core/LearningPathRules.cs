using CrestApps.OrchardCore.Learning.Programs.Models;

namespace CrestApps.OrchardCore.Learning.Programs.Core;

/// <summary>
/// The rules of a learning path: its stages (sets joined by then), when a set and a stage are complete, and which
/// stage is open to a person.
/// </summary>
public static class LearningPathRules
{
    /// <summary>
    /// Splits the sets into stages: a set joined by then starts a new stage.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<LearningPathCourseSet>> Stages(LearningPathPart path)
    {
        var stages = new List<IReadOnlyList<LearningPathCourseSet>>();
        List<LearningPathCourseSet> current = null;

        foreach (var set in path?.Sets ?? [])
        {
            if (current is null || set.Connector == LearningPathConnector.Then)
            {
                current = [];
                stages.Add(current);
            }

            current.Add(set);
        }

        return stages;
    }

    /// <summary>
    /// Decides whether a set is complete: every course, any one, or at least its minimum count. When the available
    /// courses are given, a course that is not available (hidden or deleted) is not waited for in an every-course set,
    /// but it never completes a set by being left out: a set with no available course waits for its courses, and an
    /// at-least minimum is not lowered.
    /// </summary>
    /// <param name="set">The set.</param>
    /// <param name="completedCourseIds">The courses the person completed.</param>
    /// <param name="availableCourseIds">The courses that can be taken, or <see langword="null"/> to read every course as available.</param>
    /// <returns><see langword="true"/> when the set is complete.</returns>
    public static bool IsSetComplete(LearningPathCourseSet set, IReadOnlySet<string> completedCourseIds, IReadOnlySet<string> availableCourseIds = null)
    {
        var courses = (set.CourseIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (courses.Length == 0)
        {
            return true;
        }

        // An every-course set does not wait for a course that cannot be taken, unless none of its courses can: a set
        // whose courses are all hidden is complete only when the person completed them.
        var waitFor = availableCourseIds is null ? courses : courses.Where(availableCourseIds.Contains).ToArray();

        if (waitFor.Length == 0)
        {
            waitFor = courses;
        }

        var done = courses.Count(completedCourseIds.Contains);

        return set.Join switch
        {
            LearningPathSetJoin.Any => done >= 1,
            LearningPathSetJoin.AtLeast => done >= Math.Clamp(set.MinimumCount, 1, courses.Length),
            _ => waitFor.All(completedCourseIds.Contains),
        };
    }

    /// <summary>
    /// Decides whether a stage is complete: its sets joined by and all complete, in any group the ors separate.
    /// </summary>
    /// <param name="stage">The sets of the stage.</param>
    /// <param name="completedCourseIds">The courses the person completed.</param>
    /// <param name="availableCourseIds">The courses that can be taken, or <see langword="null"/> to read every course as available.</param>
    /// <returns><see langword="true"/> when the stage is complete.</returns>
    public static bool IsStageComplete(IReadOnlyList<LearningPathCourseSet> stage, IReadOnlySet<string> completedCourseIds, IReadOnlySet<string> availableCourseIds = null)
    {
        var groupComplete = true;

        for (var index = 0; index < stage.Count; index++)
        {
            var set = stage[index];

            if (index > 0 && set.Connector == LearningPathConnector.Or)
            {
                if (groupComplete)
                {
                    return true;
                }

                groupComplete = true;
            }

            groupComplete &= IsSetComplete(set, completedCourseIds, availableCourseIds);
        }

        return groupComplete;
    }

    /// <summary>
    /// Works out the stage open to a person: the first stage not complete, or the stage count when all are.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="completedCourseIds">The courses the person completed.</param>
    /// <param name="availableCourseIds">The courses that can be taken, or <see langword="null"/> to read every course as available.</param>
    /// <returns>The index of the open stage.</returns>
    public static int OpenStage(LearningPathPart path, IReadOnlySet<string> completedCourseIds, IReadOnlySet<string> availableCourseIds = null)
    {
        var stages = Stages(path);

        for (var index = 0; index < stages.Count; index++)
        {
            if (!IsStageComplete(stages[index], completedCourseIds, availableCourseIds))
            {
                return index;
            }
        }

        return stages.Count;
    }

    /// <summary>
    /// Lists every course of a path.
    /// </summary>
    public static IReadOnlyList<string> CourseIds(LearningPathPart path)
        => (path?.Sets ?? []).SelectMany(set => set.CourseIds).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

    /// <summary>
    /// Lists the courses of the stages up to and including one.
    /// </summary>
    public static IReadOnlyList<string> CourseIdsThrough(LearningPathPart path, int stageIndex)
        => Stages(path).Take(stageIndex + 1).SelectMany(stage => stage).SelectMany(set => set.CourseIds).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();
}
