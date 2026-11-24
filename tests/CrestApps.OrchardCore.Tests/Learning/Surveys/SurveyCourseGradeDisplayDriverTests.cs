using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Drivers;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Models;
using Moq;
using OrchardCore.DisplayManagement.Handlers;

namespace CrestApps.OrchardCore.Tests.Learning.Surveys;

public sealed class SurveyCourseGradeDisplayDriverTests
{
    [Fact]
    public void R8Mi32_ASurveyNoCourseItemOwns_ShowsNothing_AndRunsNoContentQuery()
    {
        // Arrange
        var learningSurveys = new Mock<ILearningSurveyService>(MockBehavior.Strict);
        var driver = new SurveyCourseGradeDisplayDriver(learningSurveys.Object);

        // Act
        var result = driver.Edit(new Survey { ItemId = "survey1" }, (BuildEditorContext)null);
        var otherOwner = driver.Edit(new Survey { ItemId = "survey2", Owner = new RecordRef("Ticket", "ticket1") }, (BuildEditorContext)null);

        // Assert
        Assert.Null(result);
        Assert.Null(otherOwner);
        learningSurveys.VerifyNoOtherCalls();
    }

    [Fact]
    public void R8Mi32_ASurveyACourseItemOwns_ShowsTheWarningShape()
    {
        // Arrange
        var driver = new SurveyCourseGradeDisplayDriver(Mock.Of<ILearningSurveyService>());

        // Act
        var result = driver.Edit(new Survey { ItemId = "survey1", Owner = LearningSurveyContexts.Owner("quiz1") }, (BuildEditorContext)null);

        // Assert
        Assert.NotNull(result);
    }
}
