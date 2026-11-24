using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Surveys.Core;
using CrestApps.OrchardCore.Learning.Surveys.Core.Upgrades;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
        var handler = new QuizPartHandler(learningSurveys.Object, manager.Object, NullLogger<QuizPartHandler>.Instance);
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
        var handler = new QuizPartHandler(learningSurveys.Object, manager.Object, NullLogger<QuizPartHandler>.Instance);
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
        var handler = new CourseSurveyPartHandler(learningSurveys.Object, Mock.Of<IContentDefinitionManager>(), manager.Object, NullLogger<CourseSurveyPartHandler>.Instance);
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

    [Fact]
    public async Task R8Mi30_ALegacyCourseSurveyImportedBeforeItsSurvey_KeepsTheSurveyIdAndTheLegacyPart()
    {
        // Arrange: an item of the first Learning release, imported before the Surveys step brings its survey.
        var (learningSurveys, manager) = Services();
        var handler = new CourseSurveyPartHandler(learningSurveys.Object, Mock.Of<IContentDefinitionManager>(), manager.Object, NullLogger<CourseSurveyPartHandler>.Instance);
        var item = new ContentItem { ContentItemId = "feedback1", ContentType = "CourseSurvey" };
        ((JsonObject)item.Content)["SurveyPart"] = new JsonObject { ["Questions"] = new JsonArray(new JsonObject { ["Text"] = "Pace?", ["Type"] = 3 }) };
        LegacyCourseSurveyUpgrade.SetSurveyId(item, "incoming");
        var part = item.GetOrCreate<CourseSurveyPart>();

        // Act: a new item is created by the import; an item the site has is updated.
        await handler.ImportingAsync(new ImportContentContext(item), part);
        await handler.CreatingAsync(new CreateContentContext(item), part);
        await handler.UpdatingAsync(new UpdateContentContext(item), part);
        await handler.ImportedAsync(new ImportContentContext(item), part);

        // Assert: no blank survey takes the legacy questions; the item waits for the imported survey.
        Assert.Equal("incoming", item.Get<CourseSurveyPart>(nameof(CourseSurveyPart)).SurveyId);
        Assert.NotNull(((JsonObject)item.Content)["SurveyPart"]);
        learningSurveys.Verify(service => service.CreateOwnedSurveyAsync(It.IsAny<ContentItem>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task R8Mi30_ACourseSurveyHealedOnASave_LogsAWarning()
    {
        // Arrange
        var (learningSurveys, manager) = Services();
        var logger = new Mock<ILogger<CourseSurveyPartHandler>>();
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var handler = new CourseSurveyPartHandler(learningSurveys.Object, Mock.Of<IContentDefinitionManager>(), manager.Object, logger.Object);
        var item = Item<CourseSurveyPart>("feedback1", part => part.SurveyId = "gone");

        // Act
        await handler.UpdatingAsync(new UpdateContentContext(item), item.Get<CourseSurveyPart>(nameof(CourseSurveyPart)));

        // Assert
        Assert.Equal("new", item.Get<CourseSurveyPart>(nameof(CourseSurveyPart)).SurveyId);
        logger.Verify(log => log.Log(LogLevel.Warning, It.IsAny<EventId>(), It.Is<It.IsAnyType>((state, _) => state.ToString().Contains("gone", StringComparison.Ordinal)), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
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
