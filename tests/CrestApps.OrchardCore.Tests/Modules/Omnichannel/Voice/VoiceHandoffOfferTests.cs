using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// Whether an automated call's model is offered a transfer to a person, and what it is told about one.
/// </summary>
/// <remarks>
/// Offered the transfer regardless of the queue's hours, the model told an after-hours caller it was connecting them,
/// and the call then ended with a callback: nobody was there to take it.
/// </remarks>
public sealed class VoiceHandoffOfferTests
{
    private static readonly SubjectFlowSettings _flowSettings = new()
    {
        EnableAgentHandoff = true,
        HandoffQueueId = "queue-1",
    };

    [Fact]
    public async Task OfferHandoffAsync_WithoutAHandoffService_ShouldOfferNothing()
    {
        // Act
        var (transferOffered, guidance) = await VoiceAgentConversationLoop.OfferHandoffAsync(null, _flowSettings, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(transferOffered);
        Assert.Null(guidance);
    }

    [Fact]
    public async Task OfferHandoffAsync_WhenTheServiceCannotTellIfTheQueueIsOpen_ShouldOfferTheTransferAsBefore()
    {
        // Arrange
        var service = new Mock<IOmnichannelHandoffService>();

        // Act
        var (transferOffered, guidance) = await VoiceAgentConversationLoop.OfferHandoffAsync(service.Object, _flowSettings, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(transferOffered);
        Assert.Equal(OmnichannelHandoffHelper.BuildHandoffInstructions(_flowSettings), guidance);
    }

    [Fact]
    public async Task OfferHandoffAsync_WhenTheQueueIsOpen_ShouldOfferTheTransfer()
    {
        // Arrange
        var service = HandoffService(HandoffAvailability.Open);

        // Act
        var (transferOffered, guidance) = await VoiceAgentConversationLoop.OfferHandoffAsync(service.Object, _flowSettings, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(transferOffered);
        Assert.Contains(OmnichannelHandoffHelper.TransferToAgentToolName, guidance, StringComparison.Ordinal);
        Assert.DoesNotContain(VoiceCallGuidance.CallbackInsteadOfPerson, guidance, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OfferHandoffAsync_WhenTheQueueIsClosedAndACallbackIsBooked_ShouldOfferTheTransferAsACallback()
    {
        // Arrange
        // With the Dialer the handoff books a real callback after hours, so the transfer stays; the model is told it
        // books a callback, to say so rather than "connecting you".
        var service = HandoffService(HandoffAvailability.CallbackOnly);

        // Act
        var (transferOffered, guidance) = await VoiceAgentConversationLoop.OfferHandoffAsync(service.Object, _flowSettings, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(transferOffered);
        Assert.StartsWith(OmnichannelHandoffHelper.BuildHandoffInstructions(_flowSettings), guidance, StringComparison.Ordinal);
        Assert.EndsWith(VoiceCallGuidance.CallbackInsteadOfPerson, guidance, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OfferHandoffAsync_WhenTheQueueIsClosedAndNothingWouldBeBooked_ShouldOfferNoTransfer_AndSayNobodyIsThere()
    {
        // Arrange
        // Without the Dialer the after-hours handoff books nothing and ends the call: the caller is told nobody is there
        // and offered a message instead.
        var service = HandoffService(HandoffAvailability.Unavailable);

        // Act
        var (transferOffered, guidance) = await VoiceAgentConversationLoop.OfferHandoffAsync(service.Object, _flowSettings, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(transferOffered);
        Assert.Equal(VoiceCallGuidance.NobodyToHandTo, guidance);
        service.As<IOmnichannelHandoffAvailability>()
            .Verify(availability => availability.GetAvailabilityAsync("queue-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<IOmnichannelHandoffService> HandoffService(HandoffAvailability availability)
    {
        var service = new Mock<IOmnichannelHandoffService>();
        service.As<IOmnichannelHandoffAvailability>()
            .Setup(seam => seam.GetAvailabilityAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(availability);

        return service;
    }
}
