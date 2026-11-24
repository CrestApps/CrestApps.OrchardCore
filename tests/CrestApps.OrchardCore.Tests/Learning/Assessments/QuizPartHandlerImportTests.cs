using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Surveys.Core;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Tests.Learning.Assessments;

/// <summary>
/// Review round 7, R7-Mi25: a deployment whose content step runs before its Surveys step updates a course item the target
/// has, while the survey the item names is not there yet. The import keeps that survey id (the Surveys step brings the
/// survey), instead of making a blank survey that the item would then point at; a normal save of an item whose survey is
/// gone still gets a new one.
/// </summary>
public sealed class QuizPartHandlerImportTests
{
    [Fact]
    public async Task R7Mi25_AQuizUpdatedByAnImport_KeepsTheSurveyIdItBrings_EvenWhenTheSurveyIsNotThereYet()
    {
        // Arrange
        var (learningSurveys, manager) = Services();
        var handler = new QuizPartHandler(learningSurveys.Object, manager.Object);
        var item = Item<QuizPart>("quiz1", part => part.SurveyId = "incoming");
        var part = item.Get<QuizPart>(nameof(QuizPart));

        // Act: Orchard's import of an existing version runs Importing, then Updating, then Imported.
        await handler.ImportingAsync(new ImportContentContext(item), part);
        await handler.UpdatingAsync(new UpdateContentContext(item), part);
        await handler.ImportedAsync(new ImportContentContext(item), part);

        // Assert
        Assert.Equal("incoming", item.Get<QuizPart>(nameof(QuizPart)).SurveyId);
        learningSurveys.Verify(service => service.CreateOwnedSurveyAsync(It.IsAny<ContentItem>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task R7Mi25_AQuizSavedOutsideAnImport_WhoseSurveyIsGone_GetsANewOne()
    {
        // Arrange
        var (learningSurveys, manager) = Services();
        var handler = new QuizPartHandler(learningSurveys.Object, manager.Object);
        var item = Item<QuizPart>("quiz1", part => part.SurveyId = "gone");
        var part = item.Get<QuizPart>(nameof(QuizPart));

        // Act
        await handler.UpdatingAsync(new UpdateContentContext(item), part);

        // Assert
        Assert.Equal("new", item.Get<QuizPart>(nameof(QuizPart)).SurveyId);
    }

    [Fact]
    public async Task R7Mi25_ACourseSurveyUpdatedByAnImport_KeepsTheSurveyIdItBrings()
    {
        // Arrange
        var (learningSurveys, manager) = Services();
        var handler = new CourseSurveyPartHandler(learningSurveys.Object, Mock.Of<IContentDefinitionManager>(), manager.Object);
        var item = Item<CourseSurveyPart>("feedback1", part => part.SurveyId = "incoming");
        var part = item.Get<CourseSurveyPart>(nameof(CourseSurveyPart));

        // Act
        await handler.ImportingAsync(new ImportContentContext(item), part);
        await handler.UpdatingAsync(new UpdateContentContext(item), part);
        await handler.ImportedAsync(new ImportContentContext(item), part);

        // Assert
        Assert.Equal("incoming", item.Get<CourseSurveyPart>(nameof(CourseSurveyPart)).SurveyId);
        learningSurveys.Verify(service => service.CreateOwnedSurveyAsync(It.IsAny<ContentItem>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (Mock<ILearningSurveyService> LearningSurveys, Mock<ISurveyManager> Manager) Services()
    {
        var learningSurveys = new Mock<ILearningSurveyService>();
        learningSurveys
            .Setup(service => service.CreateOwnedSurveyAsync(It.IsAny<ContentItem>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Survey { ItemId = "new" });

        // No survey is found: the incoming one is not imported yet, or the item's survey is gone.
        var manager = new Mock<ISurveyManager>();
        manager.Setup(service => service.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Survey)null);

        return (learningSurveys, manager);
    }

    private static ContentItem Item<TPart>(string id, Action<TPart> configure)
        where TPart : ContentPart, new()
    {
        var item = new ContentItem { ContentItemId = id, ContentType = "Test" };
        item.Alter(configure);

        return item;
    }
}
