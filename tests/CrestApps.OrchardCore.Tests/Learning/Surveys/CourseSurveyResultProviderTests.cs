using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning.Surveys;

/// <summary>
/// A course survey item is submitted once the learner completed a response to the survey it owns, and has no result
/// before that.
/// </summary>
public sealed class CourseSurveyResultProviderTests
{
    [Fact]
    public void Build_ACompletedResponse_SubmitsTheItem()
    {
        // Arrange
        var item = new ContentItem { ContentItemId = "survey-item" };
        var responses = new[] { new SurveyResponse { ItemId = "r1", Status = SurveyResponseStatus.Completed } };

        // Act
        var result = new CourseSurveyResultProvider().Build(item, new Enrollment { ItemId = "e1" }, new SurveyDefinition(), responses);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("survey-item", result.ItemId);
        Assert.True(result.Submitted);
    }

    [Fact]
    public void Build_OnlyAResponseInProgress_GivesNoResult()
    {
        // Arrange
        var responses = new[] { new SurveyResponse { ItemId = "r1", Status = SurveyResponseStatus.InProgress } };

        // Act
        var result = new CourseSurveyResultProvider().Build(new ContentItem { ContentItemId = "survey-item" }, new Enrollment { ItemId = "e1" }, new SurveyDefinition(), responses);

        // Assert
        Assert.Null(result);
    }
}
