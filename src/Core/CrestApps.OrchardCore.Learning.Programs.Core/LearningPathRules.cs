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
    /// Decides whether a set is complete: every course, any one, or at least its minimum count.
    /// </summary>
    public static bool IsSetComplete(LearningPathCourseSet set, IReadOnlySet<string> completedCourseIds)
    {
        var courses = set.CourseIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (courses.Length == 0)
        {
            return true;
        }

        var done = courses.Count(completedCourseIds.Contains);

        return set.Join switch
        {
            LearningPathSetJoin.Any => done >= 1,
            LearningPathSetJoin.AtLeast => done >= Math.Clamp(set.MinimumCount, 1, courses.Length),
            _ => done == courses.Length,
        };
    }

    /// <summary>
    /// Decides whether a stage is complete: its sets joined by and all complete, in any group the ors separate.
    /// </summary>
    public static bool IsStageComplete(IReadOnlyList<LearningPathCourseSet> stage, IReadOnlySet<string> completedCourseIds)
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

            groupComplete &= IsSetComplete(set, completedCourseIds);
        }

        return groupComplete;
    }

    /// <summary>
    /// Works out the stage open to a person: the first stage not complete, or the stage count when all are.
    /// </summary>
    public static int OpenStage(LearningPathPart path, IReadOnlySet<string> completedCourseIds)
    {
        var stages = Stages(path);

        for (var index = 0; index < stages.Count; index++)
        {
            if (!IsStageComplete(stages[index], completedCourseIds))
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
