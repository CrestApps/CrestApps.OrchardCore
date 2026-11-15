using CrestApps.OrchardCore.Learning.Programs.Core;
using CrestApps.OrchardCore.Learning.Programs.Models;

namespace CrestApps.OrchardCore.Tests.Learning.Programs;

/// <summary>
/// The rules of a learning path: then starts a stage, and binds tighter than or inside a stage, a set is complete with
/// every course, any one or at least its minimum, and the open stage is the first that is not complete.
/// </summary>
public sealed class LearningPathRulesTests
{
    private static readonly LearningPathPart _path = new()
    {
        Sets =
        [
            new() { Id = "s1", Join = LearningPathSetJoin.All, CourseIds = ["a", "b"] },
            new() { Id = "s2", Connector = LearningPathConnector.Then, Join = LearningPathSetJoin.Any, CourseIds = ["c", "d"] },
            new() { Id = "s3", Connector = LearningPathConnector.Or, Join = LearningPathSetJoin.AtLeast, MinimumCount = 2, CourseIds = ["e", "f", "g"] },
            new() { Id = "s4", Connector = LearningPathConnector.Then, Join = LearningPathSetJoin.All, CourseIds = ["h"] },
        ],
    };

    [Theory]
    [InlineData("", 0)]
    [InlineData("a", 0)]
    [InlineData("a,b", 1)]
    [InlineData("a,b,e", 1)]
    [InlineData("a,b,c", 2)]
    [InlineData("a,b,e,f", 2)]
    [InlineData("a,b,d,h", 3)]
    public void OpenStage_IsTheFirstStageNotComplete(string completed, int expected)
    {
        // Arrange
        var done = completed.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);

        // Act
        var open = LearningPathRules.OpenStage(_path, done);

        // Assert
        Assert.Equal(3, LearningPathRules.Stages(_path).Count);
        Assert.Equal(expected, open);
    }

    [Theory]
    [InlineData("c", true)]
    [InlineData("a,b", true)]
    [InlineData("a", false)]
    [InlineData("b,c", true)]
    [InlineData("", false)]
    public void IsStageComplete_AndBindsTighterThanOr(string completed, bool expected)
    {
        // Arrange: (a and b) or c.
        var stage = new List<LearningPathCourseSet>
        {
            new() { CourseIds = ["a"] },
            new() { Connector = LearningPathConnector.And, CourseIds = ["b"] },
            new() { Connector = LearningPathConnector.Or, CourseIds = ["c"] },
        };
        var done = completed.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);

        // Act
        var complete = LearningPathRules.IsStageComplete(stage, done);

        // Assert
        Assert.Equal(expected, complete);
    }

    [Fact]
    public void CourseIdsThrough_ListsTheCoursesOfTheOpenStages()
    {
        // Act
        var first = LearningPathRules.CourseIdsThrough(_path, 0);
        var second = LearningPathRules.CourseIdsThrough(_path, 1);

        // Assert
        Assert.Equal(["a", "b"], first);
        Assert.Equal(["a", "b", "c", "d", "e", "f", "g"], second);
    }
}
