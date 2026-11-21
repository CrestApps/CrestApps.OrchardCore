using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;

namespace CrestApps.OrchardCore.Tests.Learning.Surveys;

/// <summary>
/// Removing a course item removes the surveys it owns: deleted when they have no responses, closed and archived when
/// they do, so the results are kept. Removing one version of an item that keeps others changes nothing.
/// </summary>
public sealed class LearningOwnedSurveyContentHandlerTests
{
    [Fact]
    public async Task Removed_ASurveyWithoutResponses_IsDeleted()
    {
        // Arrange
        var manager = Manager(new Survey { ItemId = "s1", Status = SurveyStatus.Open }, SurveyErrorCode.None);

        // Act
        await new LearningOwnedSurveyContentHandler(manager.Object).RemovedAsync(Removal(noActiveVersionLeft: true));

        // Assert
        manager.Verify(service => service.DeleteSurveyAsync("s1", It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(service => service.ChangeStatusAsync(It.IsAny<string>(), It.IsAny<SurveyStatus>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Removed_ASurveyWithResponses_IsClosedAndArchived()
    {
        // Arrange
        var manager = Manager(new Survey { ItemId = "s1", Status = SurveyStatus.Open }, SurveyErrorCode.HasResponses);

        // Act
        await new LearningOwnedSurveyContentHandler(manager.Object).RemovedAsync(Removal(noActiveVersionLeft: true));

        // Assert
        manager.Verify(service => service.ChangeStatusAsync("s1", SurveyStatus.Closed, It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(service => service.ChangeStatusAsync("s1", SurveyStatus.Archived, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Removed_OneVersionOfAnItemThatKeepsOthers_LeavesItsSurvey()
    {
        // Arrange
        var manager = Manager(new Survey { ItemId = "s1", Status = SurveyStatus.Open }, SurveyErrorCode.None);

        // Act
        await new LearningOwnedSurveyContentHandler(manager.Object).RemovedAsync(Removal(noActiveVersionLeft: false));

        // Assert
        manager.Verify(service => service.DeleteSurveyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<ISurveyManager> Manager(Survey survey, SurveyErrorCode deleteResult)
    {
        var manager = new Mock<ISurveyManager>();
        manager.Setup(service => service.ListByOwnerAsync(It.Is<RecordRef>(owner => owner.Id == "item1"), It.IsAny<CancellationToken>())).ReturnsAsync([survey]);
        manager.Setup(service => service.DeleteSurveyAsync(survey.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(deleteResult);
        manager.Setup(service => service.ChangeStatusAsync(It.IsAny<string>(), It.IsAny<SurveyStatus>(), It.IsAny<CancellationToken>())).ReturnsAsync(SurveyErrorCode.None);

        return manager;
    }

    private static RemoveContentContext Removal(bool noActiveVersionLeft)
        => new(new ContentItem { ContentItemId = "item1", ContentType = "QuizLesson" }, noActiveVersionLeft, RemoveContentReason.Deletion);
}
