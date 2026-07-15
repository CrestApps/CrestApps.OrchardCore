using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ProviderCommandStateServiceTests
{
    private static readonly DateTime _now = new(2026, 7, 14, 15, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan _lease = TimeSpan.FromMinutes(2);

    [Fact]
    public async Task RegisterAsync_WhenCommandIdIsNew_PersistsPendingWithZeroFence()
    {
        await WithStoreAsync(async store =>
        {
            await using var session = store.CreateSession();
            var now = _now;
            var service = CreateService(session, () => now);
            var registration = CreateRegistration();
            registration.RemoveReservationFromQueueOnFailure = false;

            // Act
            var command = await service.RegisterAsync(registration, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(ProviderCommandStatus.Pending, command.Status);
            Assert.Equal(0, command.FenceToken);
            Assert.Null(command.OwnerToken);
            Assert.Equal("command-1", command.CommandId);
            Assert.Equal(ProviderCommandType.Dial, command.CommandType);
            Assert.Equal("reservation-1", command.ReservationId);
            Assert.False(command.RemoveReservationFromQueueOnFailure);
            Assert.Equal("profile-1", command.DialerProfileId);
            Assert.Equal(_now, command.CreatedUtc);
            Assert.Equal(_now.AddMinutes(5), command.NextAttemptUtc);
        });
    }

    [Fact]
    public async Task RegisterAsync_WhenCommandIdExists_IsIdempotentAndDoesNotDuplicate()
    {
        await WithStoreAsync(async store =>
        {
            await using var session = store.CreateSession();
            var now = _now;
            var service = CreateService(session, () => now);
            var first = await service.RegisterAsync(CreateRegistration(), TestContext.Current.CancellationToken);

            // Act
            var second = await service.RegisterAsync(CreateRegistration(), TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(first.ItemId, second.ItemId);

            await using var verificationSession = store.CreateSession();
            var all = await verificationSession
                .Query<ProviderCommand, ProviderCommandIndex>(
                    index => index.CommandId == "command-1",
                    collection: ContactCenterConstants.CollectionName)
                .ListAsync(TestContext.Current.CancellationToken);
            Assert.Single(all);
        });
    }

    [Fact]
    public async Task TryClaimAsync_FromPending_TransitionsToClaimedAndIncrementsFence()
    {
        await WithStoreAsync(async store =>
        {
            await using var session = store.CreateSession();
            var now = _now;
            var service = CreateService(session, () => now);
            await service.RegisterAsync(CreateRegistration(), TestContext.Current.CancellationToken);

            // Act
            var claim = await service.TryClaimAsync("command-1", _lease, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(claim);
            Assert.Equal(1, claim.FenceToken);
            Assert.False(string.IsNullOrEmpty(claim.OwnerToken));
            Assert.Equal(_now.Add(_lease), claim.LeaseExpiresUtc);

            var command = await service.EscalateExpiredLeaseAsync("command-1", TestContext.Current.CancellationToken);
            Assert.Equal(ProviderCommandStatus.Claimed, command.Status);
            Assert.Equal(1, command.AttemptCount);
        });
    }

    [Fact]
    public async Task TryClaimAsync_WhenLeaseIsActive_ReturnsNull()
    {
        await WithStoreAsync(async store =>
        {
            await using var session = store.CreateSession();
            var service = CreateService(session, () => _now);
            await service.RegisterAsync(CreateRegistration(), TestContext.Current.CancellationToken);
            await service.TryClaimAsync("command-1", _lease, TestContext.Current.CancellationToken);

            // Act
            var secondClaim = await service.TryClaimAsync("command-1", _lease, TestContext.Current.CancellationToken);

            // Assert
            Assert.Null(secondClaim);
        });
    }

    [Fact]
    public async Task TryClaimAsync_WhenDatabaseCompareAndSetIsLost_ReturnsNull()
    {
        // Arrange
        var command = new ProviderCommand
        {
            CommandId = "command-1",
            Status = ProviderCommandStatus.Pending,
        };
        var manager = new Mock<IProviderCommandManager>();
        manager
            .Setup(value => value.FindByCommandIdAsync("command-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(command);
        manager
            .Setup(value => value.UpdateAsync(
                command,
                It.IsAny<JsonNode>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        var session = new Mock<ISession>();
        session
            .Setup(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException(new Document()));
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);
        var service = new ProviderCommandStateService(
            manager.Object,
            session.Object,
            new Mock<IDistributedLock>().Object,
            clock.Object);

        // Act
        var claim = await service.TryClaimAsync(
            "command-1",
            _lease,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(claim);
    }

    [Fact]
    public async Task TryClaimAsync_WhenLeaseExpired_ReclaimsAndFenceIsMonotonic()
    {
        await WithStoreAsync(async store =>
        {
            await using var session = store.CreateSession();
            var clockTime = _now;
            var service = CreateService(session, () => clockTime);
            await service.RegisterAsync(CreateRegistration(), TestContext.Current.CancellationToken);
            var firstClaim = await service.TryClaimAsync("command-1", _lease, TestContext.Current.CancellationToken);

            // Act
            clockTime = _now.Add(_lease).AddSeconds(1);
            var secondClaim = await service.TryClaimAsync("command-1", _lease, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(secondClaim);
            Assert.Equal(1, firstClaim.FenceToken);
            Assert.Equal(2, secondClaim.FenceToken);
            Assert.NotEqual(firstClaim.OwnerToken, secondClaim.OwnerToken);
        });
    }

    [Fact]
    public async Task MarkSentAsync_WithValidClaim_TransitionsToSent()
    {
        await WithStoreAsync(async store =>
        {
            await using var session = store.CreateSession();
            var service = CreateService(session, () => _now);
            await service.RegisterAsync(CreateRegistration(), TestContext.Current.CancellationToken);
            var claim = await service.TryClaimAsync("command-1", _lease, TestContext.Current.CancellationToken);

            // Act
            var command = await service.MarkSentAsync("command-1", claim, "provider-call-1", TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(ProviderCommandStatus.Sent, command.Status);
            Assert.Equal(_now, command.SentUtc);
            Assert.Equal("provider-call-1", command.ProviderReference);
        });
    }

    [Fact]
    public async Task MarkSentAsync_WithStaleFence_ThrowsFenceException_AndDoesNotMutate()
    {
        await WithStoreAsync(async store =>
        {
            await using var session = store.CreateSession();
            var service = CreateService(session, () => _now);
            await service.RegisterAsync(CreateRegistration(), TestContext.Current.CancellationToken);
            var claim = await service.TryClaimAsync("command-1", _lease, TestContext.Current.CancellationToken);
            var staleClaim = new ProviderCommandClaim
            {
                CommandId = claim.CommandId,
                FenceToken = claim.FenceToken - 1,
                OwnerToken = claim.OwnerToken,
                LeaseExpiresUtc = claim.LeaseExpiresUtc,
            };

            // Act
            var exception = await Record.ExceptionAsync(() =>
                service.MarkSentAsync("command-1", staleClaim, "provider-call-1", TestContext.Current.CancellationToken));

            // Assert
            Assert.IsType<ProviderCommandFenceException>(exception);

            await using var verificationSession = store.CreateSession();
            var persisted = await new ProviderCommandStore(verificationSession)
                .FindByCommandIdAsync("command-1", TestContext.Current.CancellationToken);
            Assert.Equal(ProviderCommandStatus.Claimed, persisted.Status);
            Assert.Equal(1, persisted.FenceToken);
            Assert.Null(persisted.SentUtc);
        });
    }

    [Fact]
    public async Task MarkSentAsync_WithWrongOwner_ThrowsFenceException()
    {
        await WithStoreAsync(async store =>
        {
            await using var session = store.CreateSession();
            var service = CreateService(session, () => _now);
            await service.RegisterAsync(CreateRegistration(), TestContext.Current.CancellationToken);
            var claim = await service.TryClaimAsync("command-1", _lease, TestContext.Current.CancellationToken);
            var otherOwnerClaim = new ProviderCommandClaim
            {
                CommandId = claim.CommandId,
                FenceToken = claim.FenceToken,
                OwnerToken = "another-owner",
                LeaseExpiresUtc = claim.LeaseExpiresUtc,
            };

            // Act
            var exception = await Record.ExceptionAsync(() =>
                service.MarkSentAsync("command-1", otherOwnerClaim, cancellationToken: TestContext.Current.CancellationToken));

            // Assert
            Assert.IsType<ProviderCommandFenceException>(exception);
        });
    }

    [Fact]
    public async Task MarkSentAsync_FromOutcomeUnknown_ThrowsTransitionException()
    {
        await WithStoreAsync(async store =>
        {
            await using var session = store.CreateSession();
            var service = CreateService(session, () => _now);
            await service.RegisterAsync(CreateRegistration(), TestContext.Current.CancellationToken);
            var claim = await service.TryClaimAsync("command-1", _lease, TestContext.Current.CancellationToken);
            await service.MarkSentAsync("command-1", claim, "provider-call-1", TestContext.Current.CancellationToken);
            var unknown = await service.MarkOutcomeUnknownAsync("command-1", claim, "lost response", TestContext.Current.CancellationToken);

            // The claim still matches the command's fence and owner, so a rejection here proves the state
            // machine forbids a direct re-send, not merely a fence mismatch.
            var currentClaim = new ProviderCommandClaim
            {
                CommandId = unknown.CommandId,
                FenceToken = unknown.FenceToken,
                OwnerToken = unknown.OwnerToken,
                LeaseExpiresUtc = unknown.LeaseExpiresUtc,
            };

            // Act
            var exception = await Record.ExceptionAsync(() =>
                service.MarkSentAsync("command-1", currentClaim, "provider-call-2", TestContext.Current.CancellationToken));

            // Assert
            var transitionException = Assert.IsType<ProviderCommandTransitionException>(exception);
            Assert.Equal(ProviderCommandStatus.OutcomeUnknown, transitionException.From);
            Assert.Equal(ProviderCommandStatus.Sent, transitionException.To);
        });
    }

    [Fact]
    public async Task ConfirmSentAsync_FromSent_ReachesTerminalConfirmed()
    {
        await WithStoreAsync(async store =>
        {
            await using var session = store.CreateSession();
            var service = CreateService(session, () => _now);
            await service.RegisterAsync(CreateRegistration(), TestContext.Current.CancellationToken);
            var claim = await service.TryClaimAsync("command-1", _lease, TestContext.Current.CancellationToken);
            await service.MarkSentAsync("command-1", claim, cancellationToken: TestContext.Current.CancellationToken);

            // Act
            var command = await service.ConfirmSentAsync("command-1", claim, "provider-call-9", TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(ProviderCommandStatus.Confirmed, command.Status);
            Assert.True(command.IsTerminal);
            Assert.Equal("provider-call-9", command.ProviderReference);
            Assert.Equal(_now, command.CompletedUtc);
        });
    }

    [Fact]
    public async Task StageConfirmSentAsync_DoesNotCommitUntilCallerSavesTenantSession()
    {
        await WithStoreAsync(async store =>
        {
            await using var session = store.CreateSession();
            var service = CreateService(session, () => _now);
            await service.RegisterAsync(CreateRegistration(), TestContext.Current.CancellationToken);
            var claim = await service.TryClaimAsync("command-1", _lease, TestContext.Current.CancellationToken);
            await service.MarkSentAsync("command-1", claim, cancellationToken: TestContext.Current.CancellationToken);

            // Act
            var command = await service.StageConfirmSentAsync(
                "command-1",
                claim,
                "provider-call-9",
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(ProviderCommandStatus.Confirmed, command.Status);

            await using (var verificationSession = store.CreateSession())
            {
                var persisted = await verificationSession.Query<ProviderCommand>(
                    collection: ContactCenterConstants.CollectionName)
                    .FirstOrDefaultAsync();

                Assert.Equal(ProviderCommandStatus.Sent, persisted.Status);
            }

            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            await using var committedSession = store.CreateSession();
            var committed = await committedSession.Query<ProviderCommand>(
                collection: ContactCenterConstants.CollectionName)
                .FirstOrDefaultAsync();

            Assert.Equal(ProviderCommandStatus.Confirmed, committed.Status);
        });
    }

    [Fact]
    public async Task PauseAsync_FromOutcomeUnknown_PausesAndIsNotDue()
    {
        await WithStoreAsync(async store =>
        {
            await using var session = store.CreateSession();
            var now = _now;
            var service = CreateService(session, () => now);
            await service.RegisterAsync(CreateRegistration(), TestContext.Current.CancellationToken);
            var claim = await service.TryClaimAsync("command-1", _lease, TestContext.Current.CancellationToken);
            await service.MarkSentAsync("command-1", claim, cancellationToken: TestContext.Current.CancellationToken);
            await service.MarkOutcomeUnknownAsync("command-1", claim, "lost response", TestContext.Current.CancellationToken);
            now = claim.LeaseExpiresUtc;
            var reconciliationClaim = await service.TryClaimReconciliationAsync(
                "command-1",
                _lease,
                TestContext.Current.CancellationToken);

            // Act
            var command = await service.PauseAsync(
                "command-1",
                reconciliationClaim,
                "outcome cannot be proven",
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(ProviderCommandStatus.Paused, command.Status);

            await using var verificationSession = store.CreateSession();
            var due = await new ProviderCommandStore(verificationSession)
                .ListDueAsync(_now.AddHours(1), 10, TestContext.Current.CancellationToken);
            Assert.Empty(due);
        });
    }

    [Fact]
    public async Task Compensation_FromOutcomeUnknown_ReachesTerminalCompensated()
    {
        await WithStoreAsync(async store =>
        {
            await using var session = store.CreateSession();
            var now = _now;
            var service = CreateService(session, () => now);
            await service.RegisterAsync(CreateRegistration(), TestContext.Current.CancellationToken);
            var claim = await service.TryClaimAsync("command-1", _lease, TestContext.Current.CancellationToken);
            await service.MarkSentAsync("command-1", claim, cancellationToken: TestContext.Current.CancellationToken);
            await service.MarkOutcomeUnknownAsync("command-1", claim, "lost response", TestContext.Current.CancellationToken);
            now = claim.LeaseExpiresUtc;
            var reconciliationClaim = await service.TryClaimReconciliationAsync(
                "command-1",
                _lease,
                TestContext.Current.CancellationToken);

            // Act
            await service.BeginCompensationAsync(
                "command-1",
                reconciliationClaim,
                "reconcile proved not executed",
                TestContext.Current.CancellationToken);
            var compensationClaim = await service.TryClaimCompensationAsync(
                "command-1",
                _lease,
                TestContext.Current.CancellationToken);
            var command = await service.CompleteCompensationAsync(
                "command-1",
                compensationClaim,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(ProviderCommandStatus.Compensated, command.Status);
            Assert.True(command.IsTerminal);
        });
    }

    [Fact]
    public async Task Compensation_FromPendingInvalidIntent_ReachesTerminalCompensated()
    {
        await WithStoreAsync(async store =>
        {
            await using var session = store.CreateSession();
            var service = CreateService(session, () => _now);
            await service.RegisterAsync(CreateRegistration(), TestContext.Current.CancellationToken);

            // Act
            await service.BeginPendingCompensationAsync(
                "command-1",
                "invalid request payload",
                TestContext.Current.CancellationToken);
            var compensationClaim = await service.TryClaimCompensationAsync(
                "command-1",
                _lease,
                TestContext.Current.CancellationToken);
            var command = await service.CompleteCompensationAsync(
                "command-1",
                compensationClaim,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(ProviderCommandStatus.Compensated, command.Status);
            Assert.True(command.IsTerminal);
        });
    }

    [Fact]
    public async Task TryClaimCompensationAsync_WhenCompensationAlreadyOwned_ReturnsNull()
    {
        await WithStoreAsync(async store =>
        {
            await using var session = store.CreateSession();
            var service = CreateService(session, () => _now);
            await service.RegisterAsync(CreateRegistration(), TestContext.Current.CancellationToken);
            await service.BeginPendingCompensationAsync(
                "command-1",
                "provider rejected the request",
                TestContext.Current.CancellationToken);

            // Act
            var first = await service.TryClaimCompensationAsync(
                "command-1",
                _lease,
                TestContext.Current.CancellationToken);
            var second = await service.TryClaimCompensationAsync(
                "command-1",
                _lease,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(first);
            Assert.Null(second);
        });
    }

    [Fact]
    public async Task ConfirmFromReconciliationAsync_FromOutcomeUnknown_RequiresOwnedClaim()
    {
        await WithStoreAsync(async store =>
        {
            await using var session = store.CreateSession();
            var now = _now;
            var service = CreateService(session, () => now);
            await service.RegisterAsync(CreateRegistration(), TestContext.Current.CancellationToken);
            var claim = await service.TryClaimAsync("command-1", _lease, TestContext.Current.CancellationToken);
            await service.MarkSentAsync("command-1", claim, cancellationToken: TestContext.Current.CancellationToken);
            await service.MarkOutcomeUnknownAsync("command-1", claim, "lost response", TestContext.Current.CancellationToken);
            now = claim.LeaseExpiresUtc;
            var reconciliationClaim = await service.TryClaimReconciliationAsync(
                "command-1",
                _lease,
                TestContext.Current.CancellationToken);

            // Act
            var command = await service.ConfirmFromReconciliationAsync(
                "command-1",
                reconciliationClaim,
                "provider-call-5",
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(ProviderCommandStatus.Confirmed, command.Status);
            Assert.Equal("provider-call-5", command.ProviderReference);
            Assert.Equal(1, command.ReconcileCount);
        });
    }

    [Fact]
    public async Task TryClaimReconciliationAsync_WhenLeaseIsOwned_AllowsOnlyOneWorker()
    {
        await WithStoreAsync(async store =>
        {
            var now = _now;
            await using var session = store.CreateSession();
            var service = CreateService(session, () => now);
            await service.RegisterAsync(CreateRegistration(), TestContext.Current.CancellationToken);
            var dispatchClaim = await service.TryClaimAsync("command-1", _lease, TestContext.Current.CancellationToken);
            await service.MarkSentAsync("command-1", dispatchClaim, cancellationToken: TestContext.Current.CancellationToken);
            await service.MarkOutcomeUnknownAsync("command-1", dispatchClaim, "lost response", TestContext.Current.CancellationToken);
            now = dispatchClaim.LeaseExpiresUtc;

            // Act
            var first = await service.TryClaimReconciliationAsync(
                "command-1",
                _lease,
                TestContext.Current.CancellationToken);
            var second = await service.TryClaimReconciliationAsync(
                "command-1",
                _lease,
                TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(first);
            Assert.Null(second);
        });
    }

    [Fact]
    public async Task BeginCompensationAsync_WhenDispatchClaimIsStale_DoesNotOverrideRecovery()
    {
        await WithStoreAsync(async store =>
        {
            var now = _now;
            await using var session = store.CreateSession();
            var service = CreateService(session, () => now);
            await service.RegisterAsync(CreateRegistration(), TestContext.Current.CancellationToken);
            var dispatchClaim = await service.TryClaimAsync("command-1", _lease, TestContext.Current.CancellationToken);
            await service.MarkSentAsync("command-1", dispatchClaim, cancellationToken: TestContext.Current.CancellationToken);
            now = dispatchClaim.LeaseExpiresUtc;
            await service.EscalateExpiredLeaseAsync("command-1", TestContext.Current.CancellationToken);

            // Act
            var exception = await Assert.ThrowsAsync<ProviderCommandFenceException>(() =>
                service.BeginCompensationAsync(
                    "command-1",
                    dispatchClaim,
                    "late provider rejection",
                    TestContext.Current.CancellationToken));

            // Assert
            Assert.Equal("command-1", exception.CommandId);

            await using var verificationSession = store.CreateSession();
            var persisted = await verificationSession.Query<ProviderCommand>(
                collection: ContactCenterConstants.CollectionName)
                .FirstOrDefaultAsync();

            Assert.Equal(ProviderCommandStatus.OutcomeUnknown, persisted.Status);
        });
    }

    [Fact]
    public async Task StageConfirmFromReconciliationAsync_DoesNotCommitUntilCallerSavesTenantSession()
    {
        await WithStoreAsync(async store =>
        {
            await using var session = store.CreateSession();
            var now = _now;
            var service = CreateService(session, () => now);
            await service.RegisterAsync(CreateRegistration(), TestContext.Current.CancellationToken);
            var claim = await service.TryClaimAsync("command-1", _lease, TestContext.Current.CancellationToken);
            await service.MarkSentAsync("command-1", claim, cancellationToken: TestContext.Current.CancellationToken);
            await service.MarkOutcomeUnknownAsync("command-1", claim, "lost response", TestContext.Current.CancellationToken);
            now = claim.LeaseExpiresUtc;
            var reconciliationClaim = await service.TryClaimReconciliationAsync(
                "command-1",
                _lease,
                TestContext.Current.CancellationToken);

            // Act
            var command = await service.StageConfirmFromReconciliationAsync(
                "command-1",
                reconciliationClaim,
                "provider-call-5",
                TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(ProviderCommandStatus.Confirmed, command.Status);

            await using (var verificationSession = store.CreateSession())
            {
                var persisted = await verificationSession.Query<ProviderCommand>(
                    collection: ContactCenterConstants.CollectionName)
                    .FirstOrDefaultAsync();

                Assert.Equal(ProviderCommandStatus.OutcomeUnknown, persisted.Status);
            }

            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            await using var committedSession = store.CreateSession();
            var committed = await committedSession.Query<ProviderCommand>(
                collection: ContactCenterConstants.CollectionName)
                .FirstOrDefaultAsync();

            Assert.Equal(ProviderCommandStatus.Confirmed, committed.Status);
        });
    }

    [Fact]
    public async Task EscalateExpiredLeaseAsync_FromSent_MovesToOutcomeUnknownAndReleasesOwner()
    {
        await WithStoreAsync(async store =>
        {
            await using var session = store.CreateSession();
            var clockTime = _now;
            var service = CreateService(session, () => clockTime);
            await service.RegisterAsync(CreateRegistration(), TestContext.Current.CancellationToken);
            var claim = await service.TryClaimAsync("command-1", _lease, TestContext.Current.CancellationToken);
            await service.MarkSentAsync("command-1", claim, cancellationToken: TestContext.Current.CancellationToken);

            // Act
            clockTime = _now.Add(_lease).AddSeconds(1);
            var command = await service.EscalateExpiredLeaseAsync("command-1", TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(ProviderCommandStatus.OutcomeUnknown, command.Status);
            Assert.Null(command.OwnerToken);
        });
    }

    [Fact]
    public async Task EscalateExpiredLeaseAsync_FromClaimed_MovesToPending()
    {
        await WithStoreAsync(async store =>
        {
            await using var session = store.CreateSession();
            var clockTime = _now;
            var service = CreateService(session, () => clockTime);
            await service.RegisterAsync(CreateRegistration(), TestContext.Current.CancellationToken);
            await service.TryClaimAsync("command-1", _lease, TestContext.Current.CancellationToken);

            // Act
            clockTime = _now.Add(_lease).AddSeconds(1);
            var command = await service.EscalateExpiredLeaseAsync("command-1", TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(ProviderCommandStatus.Pending, command.Status);
            Assert.Null(command.OwnerToken);
        });
    }

    [Fact]
    public async Task Transition_FromTerminalConfirmed_ThrowsTransitionException()
    {
        await WithStoreAsync(async store =>
        {
            await using var session = store.CreateSession();
            var now = _now;
            var service = CreateService(session, () => now);
            await service.RegisterAsync(CreateRegistration(), TestContext.Current.CancellationToken);
            var claim = await service.TryClaimAsync("command-1", _lease, TestContext.Current.CancellationToken);
            await service.MarkSentAsync("command-1", claim, cancellationToken: TestContext.Current.CancellationToken);
            await service.ConfirmSentAsync("command-1", claim, cancellationToken: TestContext.Current.CancellationToken);

            // Act
            var exception = await Record.ExceptionAsync(() =>
                service.FailAsync("command-1", "too late", TestContext.Current.CancellationToken));

            // Assert
            Assert.IsType<ProviderCommandTransitionException>(exception);
        });
    }

    [Fact]
    public async Task MarkSentAsync_WhenLeaseExpired_ThrowsFenceException_AndDoesNotMutate()
    {
        await WithStoreAsync(async store =>
        {
            await using var session = store.CreateSession();
            var clockTime = _now;
            var service = CreateService(session, () => clockTime);
            await service.RegisterAsync(CreateRegistration(), TestContext.Current.CancellationToken);
            var claim = await service.TryClaimAsync("command-1", _lease, TestContext.Current.CancellationToken);

            // The claim's fence and owner still match the command, but the lease has since expired, so the
            // worker no longer owns it and the send must be rejected without mutating durable state.
            clockTime = claim.LeaseExpiresUtc.AddSeconds(1);

            // Act
            var exception = await Record.ExceptionAsync(() =>
                service.MarkSentAsync("command-1", claim, "provider-call-1", TestContext.Current.CancellationToken));

            // Assert
            Assert.IsType<ProviderCommandFenceException>(exception);

            await using var verificationSession = store.CreateSession();
            var persisted = await new ProviderCommandStore(verificationSession)
                .FindByCommandIdAsync("command-1", TestContext.Current.CancellationToken);
            Assert.Equal(ProviderCommandStatus.Claimed, persisted.Status);
            Assert.Equal(1, persisted.FenceToken);
            Assert.Null(persisted.SentUtc);
        });
    }

    [Fact]
    public async Task ConfirmSentAsync_WhenLeaseExpired_ThrowsFenceException_AndDoesNotMutate()
    {
        await WithStoreAsync(async store =>
        {
            await using var session = store.CreateSession();
            var clockTime = _now;
            var service = CreateService(session, () => clockTime);
            await service.RegisterAsync(CreateRegistration(), TestContext.Current.CancellationToken);
            var claim = await service.TryClaimAsync("command-1", _lease, TestContext.Current.CancellationToken);
            await service.MarkSentAsync("command-1", claim, cancellationToken: TestContext.Current.CancellationToken);

            // Act
            clockTime = claim.LeaseExpiresUtc.AddSeconds(1);
            var exception = await Record.ExceptionAsync(() =>
                service.ConfirmSentAsync("command-1", claim, cancellationToken: TestContext.Current.CancellationToken));

            // Assert
            Assert.IsType<ProviderCommandFenceException>(exception);

            await using var verificationSession = store.CreateSession();
            var persisted = await new ProviderCommandStore(verificationSession)
                .FindByCommandIdAsync("command-1", TestContext.Current.CancellationToken);
            Assert.Equal(ProviderCommandStatus.Sent, persisted.Status);
            Assert.Null(persisted.CompletedUtc);
        });
    }

    [Fact]
    public async Task MarkOutcomeUnknownAsync_WhenLeaseExpired_ThrowsFenceException_AndDoesNotMutate()
    {
        await WithStoreAsync(async store =>
        {
            await using var session = store.CreateSession();
            var clockTime = _now;
            var service = CreateService(session, () => clockTime);
            await service.RegisterAsync(CreateRegistration(), TestContext.Current.CancellationToken);
            var claim = await service.TryClaimAsync("command-1", _lease, TestContext.Current.CancellationToken);
            await service.MarkSentAsync("command-1", claim, cancellationToken: TestContext.Current.CancellationToken);

            // Act
            clockTime = claim.LeaseExpiresUtc.AddSeconds(1);
            var exception = await Record.ExceptionAsync(() =>
                service.MarkOutcomeUnknownAsync("command-1", claim, "lost response", TestContext.Current.CancellationToken));

            // Assert
            Assert.IsType<ProviderCommandFenceException>(exception);

            await using var verificationSession = store.CreateSession();
            var persisted = await new ProviderCommandStore(verificationSession)
                .FindByCommandIdAsync("command-1", TestContext.Current.CancellationToken);
            Assert.Equal(ProviderCommandStatus.Sent, persisted.Status);
        });
    }

    [Fact]
    public async Task BeginCompensationAsync_MakesCommandDueForCrashRecovery()
    {
        await WithStoreAsync(async store =>
        {
            await using var session = store.CreateSession();
            var now = _now;
            var service = CreateService(session, () => now);
            await service.RegisterAsync(CreateRegistration(), TestContext.Current.CancellationToken);
            var claim = await service.TryClaimAsync("command-1", _lease, TestContext.Current.CancellationToken);
            await service.MarkSentAsync("command-1", claim, cancellationToken: TestContext.Current.CancellationToken);
            await service.MarkOutcomeUnknownAsync("command-1", claim, "lost response", TestContext.Current.CancellationToken);
            now = claim.LeaseExpiresUtc;
            var reconciliationClaim = await service.TryClaimReconciliationAsync(
                "command-1",
                _lease,
                TestContext.Current.CancellationToken);

            // Act
            await service.BeginCompensationAsync(
                "command-1",
                reconciliationClaim,
                "reconcile proved executed",
                TestContext.Current.CancellationToken);

            // Assert
            await using var verificationSession = store.CreateSession();
            var due = await new ProviderCommandStore(verificationSession)
                .ListDueAsync(now, 10, TestContext.Current.CancellationToken);
            var recovered = Assert.Single(due);
            Assert.Equal("command-1", recovered.CommandId);
            Assert.Equal(ProviderCommandStatus.Compensating, recovered.Status);
        });
    }

    private static ProviderCommandStateService CreateService(ISession session, Func<DateTime> nowProvider)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(service => service.UtcNow).Returns(nowProvider);

        var store = new ProviderCommandStore(session);
        var manager = new ProviderCommandManager(
            store,
            [],
            NullLogger<CatalogManager<ProviderCommand>>.Instance);

        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(value => value.TryAcquireLockAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan?>()))
            .ReturnsAsync((null, true));

        return new ProviderCommandStateService(manager, session, distributedLock.Object, clock.Object);
    }

    private static ProviderCommandRegistration CreateRegistration(string commandId = "command-1")
    {
        return new ProviderCommandRegistration
        {
            CommandId = commandId,
            ProviderName = "provider",
            CommandType = ProviderCommandType.Dial,
            ActivityItemId = "activity-1",
            InteractionId = "interaction-1",
            ReservationId = "reservation-1",
            DialerProfileId = "profile-1",
            RequestPayload = "{}",
        };
    }

    private static async Task WithStoreAsync(Func<IStore, Task> body)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"contact-center-provider-command-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes([new ProviderCommandIndexProvider()]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, TestContext.Current.CancellationToken);
        await CreateIndexSchemaAsync(store);

        try
        {
            await body(store);
        }
        finally
        {
            store.Dispose();
            File.Delete(databasePath);
        }
    }

    private static async Task CreateIndexSchemaAsync(IStore store)
    {
        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<ProviderCommandIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("CommandId", column => column.NotNull().Unique().WithLength(26))
            .Column<string>("ProviderName", column => column.WithLength(100))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<long>("FenceToken", column => column.NotNull().WithDefault(0L))
            .Column<string>("InteractionId", column => column.WithLength(26))
            .Column<DateTime>("NextAttemptUtc", column => column.NotNull())
            .Column<DateTime>("LeaseExpiresUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }
}
