using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

public sealed class AutomatedActivityCompletionServiceTests
{
    [Fact]
    public async Task CompleteAsync_WhenRequestIsValid_ShouldStoreSessionAndDelegateDisposition()
    {
        // Arrange
        var activity = new OmnichannelActivity();
        ActivityDispositionRequest dispositionRequest = null;
        var dispositionService = new Mock<IActivityDispositionService>();
        dispositionService
            .Setup(service => service.ApplyAsync(It.IsAny<ActivityDispositionRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ActivityDispositionRequest, CancellationToken>((request, _) => dispositionRequest = request)
            .ReturnsAsync(ActivityDispositionResult.Success(activity));
        var service = new AutomatedActivityCompletionService(dispositionService.Object);
        var request = new AutomatedActivityCompletionRequest
        {
            Activity = activity,
            AISessionId = " session-id ",
            Summary = " summary ",
            DispositionId = "confirmed",
            ActorId = "ai",
            ActorDisplayName = "AI agent",
        };

        // Act
        var result = await service.CompleteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("session-id", activity.AISessionId);
        Assert.NotNull(dispositionRequest);
        Assert.Same(activity, dispositionRequest.Activity);
        Assert.Equal(ActivityDispositionSource.AI, dispositionRequest.Source);
        Assert.Equal("summary", dispositionRequest.Notes);
        Assert.Equal("confirmed", dispositionRequest.DispositionId);
        Assert.Equal("ai", dispositionRequest.ActorId);
        Assert.Equal("AI agent", dispositionRequest.ActorDisplayName);
    }

    [Fact]
    public async Task CompleteAsync_WhenSessionIdIsMissing_ShouldReturnFailureWithoutDisposition()
    {
        // Arrange
        var dispositionService = new Mock<IActivityDispositionService>();
        var service = new AutomatedActivityCompletionService(dispositionService.Object);
        var request = new AutomatedActivityCompletionRequest
        {
            Activity = new OmnichannelActivity(),
        };

        // Act
        var result = await service.CompleteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        dispositionService.Verify(
            current => current.ApplyAsync(It.IsAny<ActivityDispositionRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
