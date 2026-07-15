using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ProviderCommandProcessorTests
{
    private static readonly DateTime _now = new(2026, 7, 14, 23, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DispatchAsync_PersistsSentBeforeCallingProvider()
    {
        // Arrange
        var order = new List<string>();
        var harness = CreateHarness();
        harness.StateService
            .Setup(service => service.MarkSentAsync("command-1", It.IsAny<ProviderCommandClaim>(), null, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("sent"))
            .ReturnsAsync(harness.Command);
        harness.Router
            .Setup(router => router.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                "provider",
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("route"))
            .ReturnsAsync(Success());

        // Act
        await harness.Processor.DispatchAsync("command-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["sent", "route"], order);
    }

    [Fact]
    public async Task DispatchAsync_WhenProviderSucceeds_ConfirmsAndProjectsRingingState()
    {
        // Arrange
        ContactCenterDialRequest request = null;
        var harness = CreateHarness();
        harness.Router
            .Setup(router => router.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                "provider",
                It.IsAny<CancellationToken>()))
            .Callback<ContactCenterDialRequest, string, CancellationToken>((value, _, _) => request = value)
            .ReturnsAsync(Success("call-1"));

        // Act
        var command = await harness.Processor.DispatchAsync("command-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderCommandStatus.Confirmed, command.Status);
        Assert.Equal(InteractionStatus.Ringing, harness.Interaction.Status);
        Assert.Equal("call-1", harness.Interaction.ProviderInteractionId);
        Assert.Equal("provider", harness.Interaction.ProviderName);
        Assert.Equal(_now, harness.Interaction.StartedUtc);
        Assert.Equal(ActivityStatus.Dialing, harness.Activity.Status);
        Assert.Equal("command-1", request.CommandId);
        Assert.Equal("command-1", request.Metadata[ContactCenterConstants.CommandMetadata.CommandId]);
        Assert.Equal("command-1", request.Metadata[TelephonyConstants.RequestMetadata.IdempotencyKey]);
        Assert.Equal("1", request.Metadata[ContactCenterConstants.CommandMetadata.FenceToken]);
        Assert.Equal("1", request.Metadata[TelephonyConstants.RequestMetadata.FenceToken]);
    }

    [Fact]
    public async Task DispatchAsync_WhenProviderSucceeds_StagesConfirmationAndProjectionsBeforeSingleCommit()
    {
        // Arrange
        var order = new List<string>();
        var harness = CreateHarness();
        harness.InteractionManager
            .Setup(manager => manager.UpdateAsync(
                It.IsAny<Interaction>(),
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("interaction"))
            .Returns(ValueTask.CompletedTask);
        harness.ActivityManager
            .Setup(manager => manager.UpdateAsync(
                It.IsAny<OmnichannelActivity>(),
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("activity"))
            .Returns(ValueTask.CompletedTask);
        harness.StateService
            .Setup(service => service.StageConfirmSentAsync(
                "command-1",
                It.IsAny<ProviderCommandClaim>(),
                "call-1",
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("confirm"))
            .ReturnsAsync(harness.Command);
        harness.Session
            .Setup(session => session.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("commit"))
            .Returns(Task.CompletedTask);
        harness.Router
            .Setup(router => router.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                "provider",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success("call-1"));

        // Act
        await harness.Processor.DispatchAsync("command-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["confirm", "interaction", "activity", "commit"], order);
        harness.Session.Verify(
            session => session.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WhenSuccessSettlementLosesFence_ReturnsAuthoritativeCommandWithoutProjecting()
    {
        // Arrange
        var harness = CreateHarness(ProviderCommandStatus.Pending);
        var authoritative = new ProviderCommand
        {
            CommandId = harness.Command.CommandId,
            Status = ProviderCommandStatus.OutcomeUnknown,
            FenceToken = 2,
        };
        harness.Manager
            .SetupSequence(value => value.FindByCommandIdAsync(
                harness.Command.CommandId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(harness.Command)
            .ReturnsAsync(authoritative);
        harness.Router
            .Setup(value => value.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                harness.Command.ProviderName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success());
        harness.StateService
            .Setup(value => value.StageConfirmSentAsync(
                harness.Command.CommandId,
                It.IsAny<ProviderCommandClaim>(),
                "call-1",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProviderCommandFenceException(harness.Command.CommandId, 2, 1));

        // Act
        var result = await harness.Processor.DispatchAsync(
            harness.Command.CommandId,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(authoritative, result);
        Assert.DoesNotContain(
            harness.InteractionManager.Invocations,
            invocation => invocation.Method.Name == nameof(IInteractionManager.UpdateAsync));
        Assert.DoesNotContain(
            harness.ActivityManager.Invocations,
            invocation => invocation.Method.Name == nameof(IOmnichannelActivityManager.UpdateAsync));
    }

    [Fact]
    public async Task DispatchAsync_WhenCompensationAlreadyOwned_DoesNotRepeatSideEffects()
    {
        // Arrange
        var harness = CreateHarness(ProviderCommandStatus.Compensating);
        harness.StateService
            .Setup(value => value.TryClaimCompensationAsync(
                harness.Command.CommandId,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProviderCommandClaim)null);

        // Act
        await harness.Processor.DispatchAsync(
            harness.Command.CommandId,
            TestContext.Current.CancellationToken);

        // Assert
        harness.ReservationService.Verify(
            value => value.CompensateAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.DoesNotContain(
            harness.InteractionManager.Invocations,
            invocation => invocation.Method.Name == nameof(IInteractionManager.UpdateAsync));
        Assert.DoesNotContain(
            harness.ActivityManager.Invocations,
            invocation => invocation.Method.Name == nameof(IOmnichannelActivityManager.UpdateAsync));
    }

    [Fact]
    public async Task DispatchAsync_WhenMarkSentLosesDatabaseRace_DoesNotInvokeProvider()
    {
        // Arrange
        var harness = CreateHarness(ProviderCommandStatus.Pending);
        harness.StateService
            .Setup(value => value.MarkSentAsync(
                harness.Command.CommandId,
                It.IsAny<ProviderCommandClaim>(),
                null,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException(new Document()));

        // Act
        var command = await harness.Processor.DispatchAsync(
            harness.Command.CommandId,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(harness.Command, command);
        harness.Router.Verify(
            value => value.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WhenRequestPayloadIsMissing_CompensatesWithoutCallingProvider()
    {
        // Arrange
        var harness = CreateHarness();
        harness.Command.RequestPayload = null;

        // Act
        var command = await harness.Processor.DispatchAsync("command-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderCommandStatus.Compensated, command.Status);
        harness.ReservationService.Verify(
            service => service.CompensateAsync("reservation-1", true, It.IsAny<CancellationToken>()),
            Times.Once);
        harness.Router.Verify(
            router => router.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WhenProviderFails_CompensatesExactlyOnce()
    {
        // Arrange
        var harness = CreateHarness();
        harness.Router
            .Setup(router => router.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                "provider",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Failure());

        // Act
        var command = await harness.Processor.DispatchAsync("command-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderCommandStatus.Compensated, command.Status);
        Assert.Equal(InteractionStatus.Failed, harness.Interaction.Status);
        Assert.Equal(ActivityStatus.Failed, harness.Activity.Status);
        harness.ReservationService.Verify(
            service => service.CompensateAsync("reservation-1", true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WhenOutcomeIsUnknown_DoesNotCompensateOrRedial()
    {
        // Arrange
        var harness = CreateHarness();
        harness.Router
            .Setup(router => router.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                "provider",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult { OutcomeUnknown = true });

        // Act
        await harness.Processor.DispatchAsync("command-1", TestContext.Current.CancellationToken);
        await harness.Processor.DispatchAsync("command-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderCommandStatus.Paused, harness.Command.Status);
        harness.Router.Verify(
            router => router.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                "provider",
                It.IsAny<CancellationToken>()),
            Times.Once);
        harness.ReservationService.Verify(
            service => service.CompensateAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WhenReconciliationIsUnsupported_PausesCommand()
    {
        // Arrange
        var harness = CreateHarness(ProviderCommandStatus.OutcomeUnknown);

        // Act
        var command = await harness.Processor.DispatchAsync("command-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderCommandStatus.Paused, command.Status);
        harness.Router.Verify(
            router => router.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WhenRecoveredPolicyIsNoLongerEligible_CompensatesWithoutProviderCall()
    {
        // Arrange
        var harness = CreateHarness(canDispatch: false);

        // Act
        var command = await harness.Processor.DispatchAsync("command-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderCommandStatus.Compensated, command.Status);
        harness.Router.Verify(
            value => value.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        harness.ReservationService.Verify(
            value => value.CompensateAsync("reservation-1", true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WhenAnotherWorkerOwnsReconciliation_DoesNotQueryProvider()
    {
        // Arrange
        var harness = CreateHarness(ProviderCommandStatus.OutcomeUnknown, supportsReconciliation: true);
        harness.StateService
            .Setup(value => value.TryClaimReconciliationAsync(
                "command-1",
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProviderCommandClaim)null);
        var reconciler = harness.Provider.As<IContactCenterVoiceCommandReconciler>();

        // Act
        var command = await harness.Processor.DispatchAsync("command-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(harness.Command, command);
        reconciler.Verify(
            value => value.ReconcileCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WhenReconciliationConfirms_ProjectsSuccessWithoutRedialing()
    {
        // Arrange
        var harness = CreateHarness(ProviderCommandStatus.OutcomeUnknown, supportsReconciliation: true);
        var reconciler = harness.Provider.As<IContactCenterVoiceCommandReconciler>();
        reconciler
            .Setup(value => value.ReconcileCommandAsync("command-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceCommandReconciliationResult
            {
                Outcome = ContactCenterVoiceCommandReconciliationOutcome.Confirmed,
                ProviderCallId = "call-1",
            });

        // Act
        var command = await harness.Processor.DispatchAsync("command-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderCommandStatus.Confirmed, command.Status);
        Assert.Equal(InteractionStatus.Ringing, harness.Interaction.Status);
        Assert.Equal("call-1", harness.Interaction.ProviderInteractionId);
        Assert.Equal(ActivityStatus.Dialing, harness.Activity.Status);
        harness.Router.Verify(
            router => router.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WhenReconciliationProvesNotExecuted_Compensates()
    {
        // Arrange
        var harness = CreateHarness(ProviderCommandStatus.OutcomeUnknown, supportsReconciliation: true);
        var reconciler = harness.Provider.As<IContactCenterVoiceCommandReconciler>();
        reconciler
            .Setup(value => value.ReconcileCommandAsync("command-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceCommandReconciliationResult
            {
                Outcome = ContactCenterVoiceCommandReconciliationOutcome.NotExecuted,
            });

        // Act
        var command = await harness.Processor.DispatchAsync("command-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderCommandStatus.Compensated, command.Status);
        Assert.Equal(InteractionStatus.Failed, harness.Interaction.Status);
        Assert.Equal(ActivityStatus.Failed, harness.Activity.Status);
        harness.ReservationService.Verify(
            service => service.CompensateAsync("reservation-1", true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RecoverDueAsync_WhenSentLeaseExpired_ReconcilesBeforeAnyRetry()
    {
        // Arrange
        var harness = CreateHarness(ProviderCommandStatus.Sent, supportsReconciliation: true);
        harness.Command.LeaseExpiresUtc = _now.AddMinutes(-1);
        harness.Manager
            .Setup(manager => manager.ListReclaimableAsync(_now, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([harness.Command]);
        harness.Manager
            .Setup(manager => manager.ListDueAsync(_now, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        harness.StateService
            .Setup(service => service.EscalateExpiredLeaseAsync("command-1", It.IsAny<CancellationToken>()))
            .Callback(() => harness.Command.Status = ProviderCommandStatus.OutcomeUnknown)
            .ReturnsAsync(harness.Command);
        var reconciler = harness.Provider.As<IContactCenterVoiceCommandReconciler>();
        reconciler
            .Setup(value => value.ReconcileCommandAsync("command-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceCommandReconciliationResult
            {
                Outcome = ContactCenterVoiceCommandReconciliationOutcome.Confirmed,
                ProviderCallId = "call-1",
            });

        // Act
        var recovered = await harness.Processor.RecoverDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, recovered);
        Assert.Equal(ProviderCommandStatus.Confirmed, harness.Command.Status);
        harness.Router.Verify(
            router => router.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RecoverDueAsync_WhenOneEscalationLosesConcurrency_ContinuesRemainingBatch()
    {
        // Arrange
        var harness = CreateHarness();
        var second = new ProviderCommand
        {
            CommandId = "command-2",
            Status = ProviderCommandStatus.Sent,
            LeaseExpiresUtc = _now.AddMinutes(-1),
        };
        harness.Manager
            .Setup(value => value.ListReclaimableAsync(
                _now,
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([harness.Command, second]);
        harness.Manager
            .Setup(value => value.ListDueAsync(
                _now,
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        harness.StateService
            .SetupSequence(value => value.EscalateExpiredLeaseAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException(new Document()))
            .ReturnsAsync((ProviderCommand)null);

        // Act
        var recovered = await harness.Processor.RecoverDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, recovered);
        harness.StateService.Verify(
            value => value.EscalateExpiredLeaseAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task DispatchAsync_WhenOutcomeIsUnknown_StagesCommandAndProjectionsBeforeSingleCommit()
    {
        // Arrange
        var order = new List<string>();
        var harness = CreateHarness();
        harness.StateService
            .Setup(value => value.StageOutcomeUnknownAsync(
                "command-1",
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("unknown"))
            .ReturnsAsync(harness.Command);
        harness.InteractionManager
            .Setup(value => value.UpdateAsync(
                It.IsAny<Interaction>(),
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("interaction"))
            .Returns(ValueTask.CompletedTask);
        harness.ActivityManager
            .Setup(value => value.UpdateAsync(
                It.IsAny<OmnichannelActivity>(),
                It.IsAny<System.Text.Json.Nodes.JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("activity"))
            .Returns(ValueTask.CompletedTask);
        harness.Session
            .Setup(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("commit"))
            .Returns(Task.CompletedTask);
        harness.Router
            .Setup(value => value.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                "provider",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult
            {
                OutcomeUnknown = true,
                ErrorCode = "provider_outcome_unknown",
            });

        // Act
        await harness.Processor.DispatchAsync("command-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["unknown", "interaction", "activity", "commit"], order);
        harness.Session.Verify(
            value => value.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RecoverDueAsync_WhenCommandIsPending_DispatchesItOnce()
    {
        // Arrange
        var harness = CreateHarness();
        harness.Manager
            .Setup(manager => manager.ListReclaimableAsync(_now, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        harness.Manager
            .Setup(manager => manager.ListDueAsync(_now, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([harness.Command]);
        harness.Router
            .Setup(router => router.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                "provider",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success());

        // Act
        var recovered = await harness.Processor.RecoverDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, recovered);
        harness.Router.Verify(
            router => router.RouteOutboundAsync(
                It.IsAny<ContactCenterDialRequest>(),
                "provider",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static TestHarness CreateHarness(
        ProviderCommandStatus status = ProviderCommandStatus.Pending,
        bool supportsReconciliation = false,
        bool canDispatch = true)
    {
        var command = new ProviderCommand
        {
            CommandId = "command-1",
            ProviderName = "provider",
            Status = status,
            RequestPayload = """{"ActivityId":"activity-1","InteractionId":"interaction-1","Destination":"+15551112222"}""",
            ActivityItemId = "activity-1",
            InteractionId = "interaction-1",
            ReservationId = "reservation-1",
            DialerProfileId = "profile-1",
            LeaseExpiresUtc = _now.AddMinutes(1),
        };
        var claim = new ProviderCommandClaim
        {
            CommandId = command.CommandId,
            FenceToken = 1,
            OwnerToken = "owner-1",
            LeaseExpiresUtc = _now.AddMinutes(1),
        };
        var manager = new Mock<IProviderCommandManager>();
        manager
            .Setup(value => value.FindByCommandIdAsync("command-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(command);
        var stateService = new Mock<IProviderCommandStateService>();
        stateService
            .Setup(value => value.TryClaimAsync("command-1", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                command.Status = ProviderCommandStatus.Claimed;
                command.FenceToken = claim.FenceToken;
            })
            .ReturnsAsync(claim);
        stateService
            .Setup(value => value.MarkSentAsync("command-1", claim, null, It.IsAny<CancellationToken>()))
            .Callback(() => command.Status = ProviderCommandStatus.Sent)
            .ReturnsAsync(command);
        stateService
            .Setup(value => value.StageConfirmSentAsync("command-1", claim, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, ProviderCommandClaim, string, CancellationToken>((_, _, providerReference, _) =>
            {
                command.Status = ProviderCommandStatus.Confirmed;
                command.ProviderReference = providerReference;
            })
            .ReturnsAsync(command);
        stateService
            .Setup(value => value.StageOutcomeUnknownAsync("command-1", claim, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => command.Status = ProviderCommandStatus.OutcomeUnknown)
            .ReturnsAsync(command);
        stateService
            .Setup(value => value.TryClaimReconciliationAsync(
                "command-1",
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(claim);
        stateService
            .Setup(value => value.StageConfirmFromReconciliationAsync(
                "command-1",
                claim,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, ProviderCommandClaim, string, CancellationToken>((_, _, providerReference, _) =>
            {
                command.Status = ProviderCommandStatus.Confirmed;
                command.ProviderReference = providerReference;
            })
            .ReturnsAsync(command);
        stateService
            .Setup(value => value.BeginPendingCompensationAsync("command-1", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => command.Status = ProviderCommandStatus.Compensating)
            .ReturnsAsync(command);
        stateService
            .Setup(value => value.BeginCompensationAsync(
                "command-1",
                claim,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => command.Status = ProviderCommandStatus.Compensating)
            .ReturnsAsync(command);
        stateService
            .Setup(value => value.TryClaimCompensationAsync(
                "command-1",
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(claim);
        stateService
            .Setup(value => value.CompleteCompensationAsync(
                "command-1",
                claim,
                It.IsAny<CancellationToken>()))
            .Callback(() => command.Status = ProviderCommandStatus.Compensated)
            .ReturnsAsync(command);
        stateService
            .Setup(value => value.PauseAsync(
                "command-1",
                claim,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => command.Status = ProviderCommandStatus.Paused)
            .ReturnsAsync(command);
        var reservationService = new Mock<IActivityReservationService>();
        var interaction = new Interaction { ItemId = command.InteractionId };
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(value => value.FindByIdAsync(command.InteractionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);
        var activity = new OmnichannelActivity { ItemId = command.ActivityItemId };
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager
            .Setup(value => value.FindByIdAsync(command.ActivityItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activity);
        var provider = new Mock<IContactCenterVoiceProvider>();

        if (supportsReconciliation)
        {
            provider.As<IContactCenterVoiceCommandReconciler>();
        }

        var providerResolver = new Mock<IContactCenterVoiceProviderResolver>();
        providerResolver.Setup(value => value.Get("provider")).Returns(provider.Object);
        var dispatchValidator = new Mock<IProviderCommandDispatchValidator>();
        dispatchValidator
            .Setup(value => value.CanDispatchAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(canDispatch);
        var router = new Mock<IVoiceContactCenterCallRouter>();
        var session = new Mock<ISession>();
        session
            .Setup(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);
        var scopeExecutor = new Mock<IContactCenterScopeExecutor>();
        var processor = new ProviderCommandProcessor(
            manager.Object,
            stateService.Object,
            reservationService.Object,
            interactionManager.Object,
            activityManager.Object,
            router.Object,
            providerResolver.Object,
            [dispatchValidator.Object],
            scopeExecutor.Object,
            session.Object,
            clock.Object,
            NullLogger<ProviderCommandProcessor>.Instance);
        scopeExecutor
            .Setup(value => value.ExecuteAsync<IProviderCommandStateService>(
                It.IsAny<Func<IProviderCommandStateService, Task>>()))
            .Returns((Func<IProviderCommandStateService, Task> operation) =>
                operation(stateService.Object));
        scopeExecutor
            .Setup(value => value.ExecuteAsync<IProviderCommandProcessor>(
                It.IsAny<Func<IProviderCommandProcessor, Task>>()))
            .Returns((Func<IProviderCommandProcessor, Task> operation) =>
                operation(processor));

        return new TestHarness(
            command,
            manager,
            stateService,
            reservationService,
            interaction,
            interactionManager,
            activity,
            activityManager,
            provider,
            router,
            session,
            processor);
    }

    private static ContactCenterVoiceProviderResult Failure()
    {
        return new ContactCenterVoiceProviderResult
        {
            ErrorCode = "provider_failed",
            ErrorMessage = "Provider rejected the request.",
        };
    }

    private static ContactCenterVoiceProviderResult Success(string providerCallId = "call-1")
    {
        return new ContactCenterVoiceProviderResult
        {
            Succeeded = true,
            ProviderCallId = providerCallId,
            ProviderName = "provider",
        };
    }

    private sealed record TestHarness(
        ProviderCommand Command,
        Mock<IProviderCommandManager> Manager,
        Mock<IProviderCommandStateService> StateService,
        Mock<IActivityReservationService> ReservationService,
        Interaction Interaction,
        Mock<IInteractionManager> InteractionManager,
        OmnichannelActivity Activity,
        Mock<IOmnichannelActivityManager> ActivityManager,
        Mock<IContactCenterVoiceProvider> Provider,
        Mock<IVoiceContactCenterCallRouter> Router,
        Mock<ISession> Session,
        ProviderCommandProcessor Processor);
}
