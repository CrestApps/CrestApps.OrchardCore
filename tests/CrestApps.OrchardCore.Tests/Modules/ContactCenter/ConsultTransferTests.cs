using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// An attended transfer is the one an agent uses when the handover matters: they put the customer on hold, talk
/// to the person they are handing to, and only then connect the two. The consult model existed but nothing drove
/// it, so agents had blind transfer and nothing else — the customer was dropped on somebody who had not agreed
/// to take them, and if that person did not answer, the customer was simply gone.
/// </summary>
public sealed class ConsultTransferTests
{
    private static readonly DateTime _now = new(2026, 3, 4, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Starting_AConsult_RecordsItAgainstTheCall_SoASupervisorCanSeeTheCustomerIsHeld()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var consult = await harness.Service.StartAsync(
            new ConsultTransferRequest
            {
                CallSessionId = "call-1",
                InitiatedByAgentId = "a1",
                TargetType = InteractionTransferTargetType.Agent,
                TargetId = "a2",
                TargetAddress = "sip:a2@example.com",
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(consult);
        Assert.Equal(ConsultCallStatus.Initiated, consult.Status);
        Assert.Equal(_now, consult.StartedUtc);
        Assert.Single(harness.Session.Consults);
    }

    [Fact]
    public async Task Starting_ASecondConsult_IsRefusedWhileOneIsLive()
    {
        // Arrange
        // An agent cannot be in two private conversations at once, and a second consult would leave the first
        // destination talking to nobody while still believing they are on a call.
        var harness = new Harness();
        var request = Request();

        await harness.Service.StartAsync(request, TestContext.Current.CancellationToken);

        // Act
        var second = await harness.Service.StartAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(second);
        Assert.Single(harness.Session.Consults);
    }

    [Fact]
    public async Task Completing_AConnectedConsult_TransfersTheCustomer()
    {
        // Arrange
        var harness = new Harness();
        var consult = await harness.Service.StartAsync(Request(), TestContext.Current.CancellationToken);
        await harness.Service.MarkConnectedAsync("call-1", consult.ConsultId, TestContext.Current.CancellationToken);

        // Act
        var completed = await harness.Service.CompleteAsync("call-1", consult.ConsultId, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(completed);
        Assert.Equal(ConsultCallStatus.Completed, harness.Session.Consults[0].Status);
        Assert.Equal(_now, harness.Session.Consults[0].EndedUtc);
        harness.TransferProvider.Verify(
            provider => provider.CompleteConsultAsync(It.IsAny<ContactCenterVoiceAttendedTransferRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Completing_AConsultNobodyAnswered_IsRefused()
    {
        // Arrange
        // Completing an unanswered consult hands the customer to a ringing phone and hangs up on them if it is
        // never picked up. The agent must take the customer back instead.
        var harness = new Harness();
        var consult = await harness.Service.StartAsync(Request(), TestContext.Current.CancellationToken);

        // Act
        var completed = await harness.Service.CompleteAsync("call-1", consult.ConsultId, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(completed);
        harness.TransferProvider.Verify(
            provider => provider.CompleteConsultAsync(It.IsAny<ContactCenterVoiceAttendedTransferRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Cancelling_ReturnsTheAgentToTheCustomer()
    {
        // Arrange
        // The whole point of consulting first is that the agent can change their mind; cancelling must leave the
        // customer with the agent they already had rather than in limbo.
        var harness = new Harness();
        var consult = await harness.Service.StartAsync(Request(), TestContext.Current.CancellationToken);
        await harness.Service.MarkConnectedAsync("call-1", consult.ConsultId, TestContext.Current.CancellationToken);

        // Act
        var cancelled = await harness.Service.CancelAsync("call-1", consult.ConsultId, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(cancelled);
        Assert.Equal(ConsultCallStatus.Cancelled, harness.Session.Consults[0].Status);
        harness.TransferProvider.Verify(
            provider => provider.CancelConsultAsync(It.IsAny<ContactCenterVoiceAttendedTransferRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Completing_AConsultTwice_TransfersOnce()
    {
        // Arrange
        // A double-clicked button or a redelivered command must not issue two transfers on one call.
        var harness = new Harness();
        var consult = await harness.Service.StartAsync(Request(), TestContext.Current.CancellationToken);
        await harness.Service.MarkConnectedAsync("call-1", consult.ConsultId, TestContext.Current.CancellationToken);

        // Act
        await harness.Service.CompleteAsync("call-1", consult.ConsultId, TestContext.Current.CancellationToken);
        var second = await harness.Service.CompleteAsync("call-1", consult.ConsultId, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(second);
        harness.TransferProvider.Verify(
            provider => provider.CompleteConsultAsync(It.IsAny<ContactCenterVoiceAttendedTransferRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartingAConsult_ToADestinationThePolicyRefuses_IsRefused()
    {
        // Arrange
        // A consult is a call the platform places on the agent's behalf, so it is bound by the same destination
        // rules as any other; otherwise the consult field is a way around the catalog.
        var harness = new Harness(transferAllowed: false);

        // Act
        var consult = await harness.Service.StartAsync(Request(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(consult);
        Assert.Empty(harness.Session.Consults);
    }

    private static ConsultTransferRequest Request()
        => new()
        {
            CallSessionId = "call-1",
            InitiatedByAgentId = "a1",
            TargetType = InteractionTransferTargetType.Agent,
            TargetId = "a2",
            TargetAddress = "sip:a2@example.com",
        };

    private sealed class Harness
    {
        public CallSession Session { get; } = new()
        {
            ItemId = "call-1",
            ProviderCallId = "provider-call-1",
        };

        public Mock<IContactCenterVoiceAttendedTransferProvider> TransferProvider { get; } = new();

        public ConsultTransferService Service { get; }

        public Harness(bool transferAllowed = true)
        {
            var callSessionManager = new Mock<ICallSessionManager>();
            callSessionManager
                .Setup(manager => manager.FindByIdAsync("call-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Session);
            callSessionManager
                .Setup(manager => manager.UpdateAsync(It.IsAny<CallSession>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);

            TransferProvider
                .Setup(provider => provider.BeginConsultAsync(It.IsAny<ContactCenterVoiceAttendedTransferRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = transferAllowed, ProviderLegId = "consult-leg-1" });
            TransferProvider
                .Setup(provider => provider.CompleteConsultAsync(It.IsAny<ContactCenterVoiceAttendedTransferRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true });
            TransferProvider
                .Setup(provider => provider.CancelConsultAsync(It.IsAny<ContactCenterVoiceAttendedTransferRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContactCenterVoiceProviderResult { Succeeded = true });

            // The provider is resolved as a capability of the tenant's voice provider, because only a
            // provider that can hold a customer and ring a third party privately implements it.
            var voiceProviderResolver = new Mock<IContactCenterVoiceProviderResolver>();
            voiceProviderResolver
                .Setup(resolver => resolver.Get(It.IsAny<string>()))
                .Returns(TransferProvider.As<IContactCenterVoiceProvider>().Object);

            Service = new ConsultTransferService(
                callSessionManager.Object,
                voiceProviderResolver.Object,
                new StubClock(_now),
                NullLogger<ConsultTransferService>.Instance);
        }
    }
}
