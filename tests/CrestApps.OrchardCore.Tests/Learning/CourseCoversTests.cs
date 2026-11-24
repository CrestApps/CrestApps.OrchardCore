using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// The catalog shows a course's cover: the first file of the Cover media field of the course type.
/// </summary>
public sealed class CourseCoversTests
{
    [Fact]
    public void TheCover_IsTheFirstFileOfTheCoverField()
    {
        // Arrange
        var course = new ContentItem { ContentType = LearningConstants.ContentTypes.Course };
        ((JsonObject)course.Content)[LearningConstants.ContentTypes.Course] = new JsonObject
        {
            ["Cover"] = new JsonObject { ["Paths"] = new JsonArray("mediafields/Course/c1/cover.png", "other.png") },
        };

        // Act
        var path = CourseCovers.GetPath(course);

        // Assert
        Assert.Equal("mediafields/Course/c1/cover.png", path);
    }

    [Fact]
    public void ACourseWithoutACover_HasNone()
    {
        Assert.Null(CourseCovers.GetPath(new ContentItem { ContentType = LearningConstants.ContentTypes.Course }));
        Assert.Null(CourseCovers.GetPath(null));
    }
}
