using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
        harness.Executor
            .Setup(exec => exec.ExecuteAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<ProviderCommandClaim>(),
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
        var harness = CreateHarness();
        harness.Executor
            .Setup(exec => exec.ExecuteAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<CancellationToken>()))
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
    }

    [Fact]
    public async Task DispatchAsync_WhenCallerCancelsAfterProviderContact_SettlesWithServerOwnedToken()
    {
        // Arrange
        using var callerCancellation = new CancellationTokenSource();
        var harness = CreateHarness();
        harness.Executor
            .Setup(exec => exec.ExecuteAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<CancellationToken>()))
            .Returns<ProviderCommand, ProviderCommandClaim, CancellationToken>((_, _, cancellationToken) =>
            {
                Assert.False(cancellationToken.IsCancellationRequested);
                callerCancellation.Cancel();

                return Task.FromResult(Success("call-1"));
            });
        harness.StateService
            .Setup(service => service.StageConfirmSentAsync(
                "command-1",
                It.IsAny<ProviderCommandClaim>(),
                "call-1",
                It.IsAny<CancellationToken>()))
            .Callback<string, ProviderCommandClaim, string, CancellationToken>(
                (_, _, _, cancellationToken) => Assert.False(cancellationToken.IsCancellationRequested))
            .ReturnsAsync(harness.Command);

        // Act
        var result = await harness.Processor.DispatchAsync("command-1", callerCancellation.Token);

        // Assert
        Assert.Same(harness.Command, result);
        Assert.True(callerCancellation.IsCancellationRequested);
    }

    [Fact]
    public async Task DispatchAsync_WhenServerDeadlineExpires_SettlesOutcomeUnknownWithoutProviderRetry()
    {
        // Arrange
        var harness = CreateHarness(commandExecutor: new TimeoutTelephonyCommandExecutor());

        // Act
        var result = await harness.Processor.DispatchAsync(
            "command-1",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderCommandStatus.OutcomeUnknown, result.Status);
        harness.Executor.Verify(
            exec => exec.ProjectOutcomeUnknownAsync(
                It.IsAny<ProviderCommand>(),
                "provider_dispatch_timeout",
                It.IsAny<CancellationToken>()),
            Times.Once);
        harness.Executor.Verify(
            exec => exec.ExecuteAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WhenProviderSucceeds_StagesConfirmationAndProjectionsBeforeSingleCommit()
    {
        // Arrange
        var order = new List<string>();
        var harness = CreateHarness();
        harness.Executor
            .Setup(exec => exec.ProjectSuccessAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<ContactCenterVoiceProviderResult>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("project"))
            .Returns(Task.CompletedTask);
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
        harness.Executor
            .Setup(exec => exec.ExecuteAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success("call-1"));

        // Act
        await harness.Processor.DispatchAsync("command-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["confirm", "project", "commit"], order);
        harness.Session.Verify(
            session => session.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WhenServerExecutionIsInterrupted_SettlesCancelledOutcomeUnknown()
    {
        // Arrange
        var harness = CreateHarness(commandExecutor: new CancelledTelephonyCommandExecutor());

        // Act
        var result = await harness.Processor.DispatchAsync(
            "command-1",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderCommandStatus.OutcomeUnknown, result.Status);
        harness.Executor.Verify(
            executor => executor.ProjectOutcomeUnknownAsync(
                It.IsAny<ProviderCommand>(),
                "provider_dispatch_cancelled",
                It.IsAny<CancellationToken>()),
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
            .ReturnsAsync(authoritative)
            .ReturnsAsync(authoritative);
        harness.Executor
            .Setup(value => value.ExecuteAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<ProviderCommandClaim>(),
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
        harness.Executor.Verify(
            exec => exec.ProjectSuccessAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<ContactCenterVoiceProviderResult>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
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
        harness.Executor.Verify(
            exec => exec.ProjectFailureAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        harness.Executor.Verify(
            exec => exec.ProjectSuccessAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<ContactCenterVoiceProviderResult>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
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
        harness.Executor.Verify(
            exec => exec.ExecuteAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WhenExecutorDeclinesDispatch_CompensatesWithoutCallingProvider()
    {
        // Arrange
        var harness = CreateHarness(canDispatch: false);

        // Act
        var command = await harness.Processor.DispatchAsync("command-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderCommandStatus.Compensated, command.Status);
        harness.ReservationService.Verify(
            service => service.CompensateAsync("reservation-1", true, It.IsAny<CancellationToken>()),
            Times.Once);
        harness.Executor.Verify(
            exec => exec.ExecuteAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_WhenProviderFails_CompensatesExactlyOnce()
    {
        // Arrange
        var harness = CreateHarness();
        harness.Executor
            .Setup(exec => exec.ExecuteAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<ProviderCommandClaim>(),
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
        harness.Executor
            .Setup(exec => exec.ExecuteAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult { OutcomeUnknown = true });

        // Act
        await harness.Processor.DispatchAsync("command-1", TestContext.Current.CancellationToken);
        await harness.Processor.DispatchAsync("command-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderCommandStatus.Paused, harness.Command.Status);
        harness.Executor.Verify(
            exec => exec.ExecuteAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<ProviderCommandClaim>(),
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
        harness.Executor.Verify(
            exec => exec.ExecuteAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<ProviderCommandClaim>(),
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
        harness.Executor.Verify(
            exec => exec.ExecuteAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        harness.ReservationService.Verify(
            value => value.CompensateAsync("reservation-1", true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WhenRemoveReservationFromQueueOnFailureIsFalse_PassesFalseToReservationService()
    {
        // Arrange
        var harness = CreateHarness(canDispatch: false);
        harness.Command.RemoveReservationFromQueueOnFailure = false;

        // Act
        var command = await harness.Processor.DispatchAsync("command-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderCommandStatus.Compensated, command.Status);
        harness.ReservationService.Verify(
            service => service.CompensateAsync("reservation-1", false, It.IsAny<CancellationToken>()),
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
        harness.Executor.Verify(
            exec => exec.ExecuteAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<ProviderCommandClaim>(),
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
        harness.Executor.Verify(
            exec => exec.ExecuteAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<ProviderCommandClaim>(),
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
        harness.Executor
            .Setup(exec => exec.ProjectOutcomeUnknownAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("project"))
            .Returns(Task.CompletedTask);
        harness.Session
            .Setup(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("commit"))
            .Returns(Task.CompletedTask);
        harness.Executor
            .Setup(exec => exec.ExecuteAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult
            {
                OutcomeUnknown = true,
                ErrorCode = "provider_outcome_unknown",
            });

        // Act
        await harness.Processor.DispatchAsync("command-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["unknown", "project", "commit"], order);
        harness.Session.Verify(
            value => value.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WhenProviderFeatureIsQuiescing_DefersWithoutCompensation()
    {
        // Arrange
        var harness = CreateHarness();
        harness.Executor
            .Setup(exec => exec.ExecuteAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactCenterVoiceProviderResult
            {
                ErrorCode = "feature_quiescing",
            });

        // Act
        var command = await harness.Processor.DispatchAsync(
            "command-1",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderCommandStatus.Pending, command.Status);
        harness.StateService.Verify(
            service => service.DeferSentAsync(
                "command-1",
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        harness.StateService.Verify(
            service => service.BeginCompensationAsync(
                It.IsAny<string>(),
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
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
        harness.Executor
            .Setup(exec => exec.ExecuteAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Success());

        // Act
        var recovered = await harness.Processor.RecoverDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, recovered);
        harness.Executor.Verify(
            exec => exec.ExecuteAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WhenNoExecutorRegisteredForCommandType_CompensatesWithoutCallingProvider()
    {
        // Arrange
        var harness = CreateHarness(executorsOverride: []);

        // Act
        var command = await harness.Processor.DispatchAsync("command-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderCommandStatus.Compensated, command.Status);
        harness.ReservationService.Verify(
            service => service.CompensateAsync("reservation-1", true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_WhenDuplicateExecutorsRegisteredForCommandType_CompensatesWithoutCallingProvider()
    {
        // Arrange
        var duplicate1 = new Mock<IProviderCommandTypeExecutor>();
        duplicate1.SetupGet(exec => exec.CommandType).Returns(ProviderCommandType.Dial);
        var duplicate2 = new Mock<IProviderCommandTypeExecutor>();
        duplicate2.SetupGet(exec => exec.CommandType).Returns(ProviderCommandType.Dial);
        var harness = CreateHarness(executorsOverride: [duplicate1.Object, duplicate2.Object]);

        // Act
        var command = await harness.Processor.DispatchAsync("command-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderCommandStatus.Compensated, command.Status);
        harness.ReservationService.Verify(
            service => service.CompensateAsync("reservation-1", true, It.IsAny<CancellationToken>()),
            Times.Once);
        duplicate1.Verify(
            exec => exec.ExecuteAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        duplicate2.Verify(
            exec => exec.ExecuteAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<ProviderCommandClaim>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static TestHarness CreateHarness(
        ProviderCommandStatus status = ProviderCommandStatus.Pending,
        bool supportsReconciliation = false,
        bool canDispatch = true,
        IList<IProviderCommandTypeExecutor> executorsOverride = null,
        ITelephonyCommandExecutor commandExecutor = null)
    {
        var command = new ProviderCommand
        {
            CommandId = "command-1",
            CommandType = ProviderCommandType.Dial,
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
            .Setup(value => value.DeferSentAsync("command-1", claim, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => command.Status = ProviderCommandStatus.Pending)
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
        var activity = new OmnichannelActivity { ItemId = command.ActivityItemId };
        var provider = new Mock<IContactCenterVoiceProvider>();

        if (supportsReconciliation)
        {
            provider.As<IContactCenterVoiceCommandReconciler>();
        }

        var providerResolver = new Mock<IContactCenterVoiceProviderResolver>();
        providerResolver.Setup(value => value.Get("provider")).Returns(provider.Object);
        var executor = new Mock<IProviderCommandTypeExecutor>();
        executor.SetupGet(exec => exec.CommandType).Returns(ProviderCommandType.Dial);
        executor
            .Setup(exec => exec.CanDispatchAsync(It.IsAny<ProviderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(canDispatch);
        executor
            .Setup(exec => exec.ProjectSuccessAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<ContactCenterVoiceProviderResult>(),
                It.IsAny<CancellationToken>()))
            .Callback<ProviderCommand, ContactCenterVoiceProviderResult, CancellationToken>((cmd, res, _) =>
            {
                interaction.Status = InteractionStatus.Ringing;
                interaction.ProviderName = string.IsNullOrWhiteSpace(res.ProviderName) ? cmd.ProviderName : res.ProviderName;
                interaction.ProviderInteractionId = res.ProviderCallId;
                interaction.StartedUtc = _now;
                activity.Status = ActivityStatus.Dialing;
            })
            .Returns(Task.CompletedTask);
        executor
            .Setup(exec => exec.ProjectFailureAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                interaction.Status = InteractionStatus.Failed;
                activity.Status = ActivityStatus.Failed;
            })
            .Returns(Task.CompletedTask);
        executor
            .Setup(exec => exec.ProjectOutcomeUnknownAsync(
                It.IsAny<ProviderCommand>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var session = new Mock<ISession>();
        session
            .Setup(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);
        var scopeExecutor = new Mock<IContactCenterScopeExecutor>();
        var actualExecutors = executorsOverride ?? [executor.Object];
        var processor = new ProviderCommandProcessor(
            manager.Object,
            stateService.Object,
            reservationService.Object,
            providerResolver.Object,
            actualExecutors,
            commandExecutor ?? new DefaultTelephonyCommandExecutor(
                Options.Create(new TelephonyCommandOptions()),
                Mock.Of<IHostApplicationLifetime>()),
            scopeExecutor.Object,
            new TestContactCenterFeatureWorkManager(),
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
            activity,
            provider,
            executor,
            session,
            processor);
    }

    private sealed class TimeoutTelephonyCommandExecutor : ITelephonyCommandExecutor
    {
        public Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> operation)
        {
            throw new TimeoutException("The test command exceeded its server-owned timeout.");
        }
    }

    private sealed class CancelledTelephonyCommandExecutor : ITelephonyCommandExecutor
    {
        public Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> operation)
        {
            throw new OperationCanceledException("The test command was interrupted by server shutdown.");
        }
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
        OmnichannelActivity Activity,
        Mock<IContactCenterVoiceProvider> Provider,
        Mock<IProviderCommandTypeExecutor> Executor,
        Mock<ISession> Session,
        ProviderCommandProcessor Processor);
}
