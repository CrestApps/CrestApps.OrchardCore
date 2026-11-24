using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Surveys.Core;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Tests.Learning.Surveys;

/// <summary>
/// Review round 6, R6-L23: a quiz or course survey whose survey is gone (one restored from the Audit Trail after its
/// removal purged the survey) gets a new survey when it is saved, instead of pointing at nothing. A new item keeps the
/// survey id it comes with, since an import may bring the survey after the item.
/// </summary>
public sealed class OwnedSurveyRecreateTests
{
    [Fact]
    public async Task R6L23_SavingAQuizWhoseSurveyIsGone_GivesItANewSurvey()
    {
        // Arrange
        var (item, learningSurveys, manager) = Arrange<QuizPart>(part => part.SurveyId = "purged");
        var handler = new QuizPartHandler(learningSurveys.Object, manager.Object);

        // Act
        await handler.UpdatingAsync(new UpdateContentContext(item), item.GetOrCreate<QuizPart>());

        // Assert
        Assert.Equal("recreated", item.GetOrCreate<QuizPart>().SurveyId);
        learningSurveys.Verify(service => service.CreateOwnedSurveyAsync(item, SurveyKinds.Quiz, SurveyTemplateNames.Quiz, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task R6L23_CreatingAQuizWithASurveyIdNotYetImported_KeepsIt()
    {
        // Arrange
        var (item, learningSurveys, manager) = Arrange<QuizPart>(part => part.SurveyId = "imported-later");
        var handler = new QuizPartHandler(learningSurveys.Object, manager.Object);

        // Act
        await handler.CreatingAsync(new CreateContentContext(item), item.GetOrCreate<QuizPart>());

        // Assert
        Assert.Equal("imported-later", item.GetOrCreate<QuizPart>().SurveyId);
        learningSurveys.Verify(service => service.CreateOwnedSurveyAsync(It.IsAny<ContentItem>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task R6L23_SavingAQuizWhoseSurveyExists_KeepsIt()
    {
        // Arrange
        var (item, learningSurveys, manager) = Arrange<QuizPart>(part => part.SurveyId = "survey1");
        manager.Setup(service => service.FindByIdAsync("survey1", It.IsAny<CancellationToken>())).ReturnsAsync(new Survey { ItemId = "survey1" });
        var handler = new QuizPartHandler(learningSurveys.Object, manager.Object);

        // Act
        await handler.UpdatingAsync(new UpdateContentContext(item), item.GetOrCreate<QuizPart>());

        // Assert
        Assert.Equal("survey1", item.GetOrCreate<QuizPart>().SurveyId);
        learningSurveys.Verify(service => service.CreateOwnedSurveyAsync(It.IsAny<ContentItem>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task R6L23_SavingACourseSurveyWhoseSurveyIsGone_GivesItANewSurvey()
    {
        // Arrange
        var (item, learningSurveys, manager) = Arrange<CourseSurveyPart>(part => part.SurveyId = "purged");
        var handler = new CourseSurveyPartHandler(learningSurveys.Object, Mock.Of<IContentDefinitionManager>(), manager.Object);

        // Act
        await handler.UpdatingAsync(new UpdateContentContext(item), item.GetOrCreate<CourseSurveyPart>());

        // Assert
        Assert.Equal("recreated", item.GetOrCreate<CourseSurveyPart>().SurveyId);
    }

    private static (ContentItem Item, Mock<ILearningSurveyService> LearningSurveys, Mock<ISurveyManager> Manager) Arrange<TPart>(Action<TPart> configure)
        where TPart : ContentPart, new()
    {
        var item = new ContentItem { ContentItemId = "item1", ContentType = "Item" };
        item.Alter(configure);

        var learningSurveys = new Mock<ILearningSurveyService>();
        learningSurveys
            .Setup(service => service.CreateOwnedSurveyAsync(It.IsAny<ContentItem>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Survey { ItemId = "recreated" });

        return (item, learningSurveys, new Mock<ISurveyManager>());
    }
}
