using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

public sealed class DefaultActivityDispositionServiceTests
{
    private static readonly DateTime _now = new(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ApplyAsync_CompletesActivityAndRunsSubjectActions()
    {
        // Arrange
        var activity = new OmnichannelActivity { ItemId = "act1", DispositionId = "d1", ContactContentItemId = "c1", SubjectContentType = "S" };

        var activityManager = new Mock<IOmnichannelActivityManager>();
        var dispositionsCatalog = new Mock<INamedCatalog<OmnichannelDisposition>>();
        dispositionsCatalog
            .Setup(m => m.FindByIdAsync("d1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelDisposition { Name = "Sale" });
        var contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(m => m.GetAsync("c1", It.IsAny<VersionOptions>()))
            .ReturnsAsync(new ContentItem { ContentType = "Customer" });
        var executor = new Mock<ISubjectActionExecutor>();

        var service = CreateService(activityManager, dispositionsCatalog, contentManager, executor);

        // Act
        var result = await service.ApplyAsync(
            new ActivityDispositionRequest { Activity = activity, DispositionId = "d1", ActorId = "u1", ActorDisplayName = "Agent" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(ActivityStatus.Completed, activity.Status);
        Assert.Equal("u1", activity.CompletedById);
        Assert.Equal(_now, activity.CompletedUtc);

        executor.Verify(
            e => e.ExecuteAsync(It.Is<SubjectActionExecutionContext>(context => context.Activity == activity && context.Disposition.Name == "Sale"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ApplyAsync_WhenActivityIsNull_ReturnsFailure()
    {
        // Arrange
        var service = CreateService(
            new Mock<IOmnichannelActivityManager>(),
            new Mock<INamedCatalog<OmnichannelDisposition>>(),
            new Mock<IContentManager>(),
            new Mock<ISubjectActionExecutor>());

        // Act
        var result = await service.ApplyAsync(new ActivityDispositionRequest { Activity = null }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.NotNull(result.ErrorMessage);
    }

    private static DefaultActivityDispositionService CreateService(
        Mock<IOmnichannelActivityManager> activityManager,
        Mock<INamedCatalog<OmnichannelDisposition>> dispositionsCatalog,
        Mock<IContentManager> contentManager,
        Mock<ISubjectActionExecutor> executor)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new DefaultActivityDispositionService(
            activityManager.Object,
            dispositionsCatalog.Object,
            contentManager.Object,
            executor.Object,
            clock.Object);
    }
}
