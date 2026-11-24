using CrestApps.Core.Services;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Learning.Surveys;

/// <summary>
/// Review round 6, R6-M13: Orchard creates a copied course item before it copies the content into it, so the item gets a
/// blank survey first. The copy replaces it on the request's own session, never through the survey delete, whose steps
/// commit in child scopes that wait, on SQL Server, on the request's uncommitted row.
/// </summary>
public sealed class LearningSurveyServiceCopyTests
{
    [Fact]
    public async Task R6M13_TheBlankSurveyOfACopiedItem_IsRemovedOnTheRequestSession_AndTheCopyGetsItsOwnSurvey()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var copy = new ContentItem { ContentItemId = "quiz-copy", DisplayText = "Planets quiz (copy)" };
        var source = new Survey { ItemId = "source", Title = "Planets quiz", Kind = SurveyKinds.Quiz, Owner = LearningSurveyContexts.Owner("quiz1") };
        source.Draft.Scoring.Enabled = true;
        var blank = new Survey { ItemId = "blank", Kind = SurveyKinds.Quiz, Owner = LearningSurveyContexts.Owner(copy.ContentItemId) };
        blank.Draft.Scoring.Enabled = true;
        var copied = new Survey { ItemId = "copied", Kind = SurveyKinds.Quiz, Owner = LearningSurveyContexts.Owner(copy.ContentItemId) };

        var manager = new Mock<ISurveyManager>();
        manager.SetupSequence(service => service.CreateSurveyAsync(It.IsAny<SurveyCreateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(blank)
            .ReturnsAsync(copied);
        manager.Setup(service => service.ListByOwnerAsync(LearningSurveyContexts.Owner(copy.ContentItemId), It.IsAny<CancellationToken>())).ReturnsAsync([blank]);
        manager.Setup(service => service.FindByIdAsync("source", It.IsAny<CancellationToken>())).ReturnsAsync(source);

        var catalog = new Mock<ICatalog<Survey>>();
        var services = new Mock<IServiceProvider>();
        services.Setup(provider => provider.GetService(typeof(ICatalog<Survey>))).Returns(catalog.Object);

        var learningSurveys = new LearningSurveyService(
            manager.Object,
            services.Object,
            Mock.Of<ISurveyResponseQueries>(),
            Mock.Of<ICourseAccessService>(),
            Mock.Of<ICourseStructureService>(),
            Mock.Of<IContentManager>(),
            Mock.Of<IContentDefinitionManager>(),
            Mock.Of<ISession>());

        // Act: the item's creating handler makes the blank survey, then the cloning handler copies the original's.
        await learningSurveys.CreateOwnedSurveyAsync(copy, SurveyKinds.Quiz, SurveyTemplateNames.Quiz, cancellationToken);
        var result = await learningSurveys.CopyOwnedSurveyAsync(copy, "source", SurveyKinds.Quiz, SurveyTemplateNames.Quiz, cancellationToken);

        // Assert
        Assert.Same(copied, result);
        catalog.Verify(store => store.DeleteAsync(blank, It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(service => service.DeleteSurveyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        manager.Verify(service => service.CreateSurveyAsync(It.Is<SurveyCreateRequest>(request => request.Definition == source.Draft && request.Owner == LearningSurveyContexts.Owner(copy.ContentItemId)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task R6M13_ASurveyThisRequestDidNotCreate_IsStillDeletedThroughTheSurveyManager()
    {
        // Arrange: a survey of the item made by an earlier request (committed), which the copy replaces.
        var cancellationToken = TestContext.Current.CancellationToken;
        var item = new ContentItem { ContentItemId = "quiz-copy" };
        var earlier = new Survey { ItemId = "earlier", Owner = LearningSurveyContexts.Owner(item.ContentItemId) };
        var created = new Survey { ItemId = "new", Owner = LearningSurveyContexts.Owner(item.ContentItemId) };

        var manager = new Mock<ISurveyManager>();
        manager.Setup(service => service.ListByOwnerAsync(It.IsAny<RecordRef>(), It.IsAny<CancellationToken>())).ReturnsAsync([earlier]);
        manager.Setup(service => service.CreateSurveyAsync(It.IsAny<SurveyCreateRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(created);

        var catalog = new Mock<ICatalog<Survey>>();
        var services = new Mock<IServiceProvider>();
        services.Setup(provider => provider.GetService(typeof(ICatalog<Survey>))).Returns(catalog.Object);

        var learningSurveys = new LearningSurveyService(
            manager.Object,
            services.Object,
            Mock.Of<ISurveyResponseQueries>(),
            Mock.Of<ICourseAccessService>(),
            Mock.Of<ICourseStructureService>(),
            Mock.Of<IContentManager>(),
            Mock.Of<IContentDefinitionManager>(),
            Mock.Of<ISession>());

        // Act: the source survey is gone, so the copy gets a new survey from the template.
        var result = await learningSurveys.CopyOwnedSurveyAsync(item, "gone", SurveyKinds.Survey, template: null, cancellationToken);

        // Assert
        Assert.Same(created, result);
        manager.Verify(service => service.DeleteSurveyAsync("earlier", It.IsAny<CancellationToken>()), Times.Once);
        catalog.Verify(store => store.DeleteAsync(It.IsAny<Survey>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
