using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Guards the caller's hangup that never reached the Contact Center. The telephony call-history projection runs
/// first and failed on a locked database; because the fan-out stopped at the first failure, the Contact Center
/// projection never saw the hangup, so the call and its interaction stayed up until a reconciliation sweep ended them
/// half a minute later with inflated talk and hold time.
/// </summary>
public sealed class NormalizedVoiceEventIngestorFailureTests
{
    [Fact]
    public async Task IngestAsync_WhenAnEarlierProjectionFails_StillRunsTheLaterOnesAndThenReportsTheFailure()
    {
        // Arrange
        var failure = new InvalidOperationException("database is locked");
        var failing = new StubHandler(order: 0, failure);
        var contactCenter = new StubHandler(order: 100);
        var ingestor = CreateIngestor(failing, contactCenter);

        // Act
        var exception = await Record.ExceptionAsync(() => ingestor.IngestAsync(Hangup(), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(1, contactCenter.Calls);

        // The delivery still fails, so the durable webhook inbox schedules it again for the projection that failed.
        Assert.Same(failure, exception);
    }

    [Fact]
    public async Task IngestAsync_WhenEveryProjectionSucceeds_ReportsWhetherAnyHandledTheEvent()
    {
        // Arrange
        var first = new StubHandler(order: 0, handled: false);
        var second = new StubHandler(order: 100, handled: true);
        var ingestor = CreateIngestor(first, second);

        // Act
        var handled = await ingestor.IngestAsync(Hangup(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(handled);
        Assert.Equal(1, first.Calls);
        Assert.Equal(1, second.Calls);
    }

    [Fact]
    public async Task IngestAsync_WhenCanceled_DoesNotRunTheRemainingProjections()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var canceling = new StubHandler(order: 0, onHandle: cancellation.Cancel, failure: null, throwCanceled: true);
        var later = new StubHandler(order: 100);
        var ingestor = CreateIngestor(canceling, later);

        // Act
        var exception = await Record.ExceptionAsync(() => ingestor.IngestAsync(Hangup(), cancellation.Token));

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        Assert.Equal(0, later.Calls);
    }

    private static NormalizedVoiceEventIngestor CreateIngestor(params INormalizedVoiceEventHandler[] handlers)
    {
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((null, true));

        return new NormalizedVoiceEventIngestor(
            handlers,
            new ProviderIdentityResolver([]),
            new VoiceIngressGate(distributedLock.Object),
            NullLogger<NormalizedVoiceEventIngestor>.Instance);
    }

    private static ProviderVoiceEvent Hangup()
        => new()
        {
            ProviderName = "ProviderA",
            ProviderCallId = "caller-1",
            State = VoiceCallState.Ended,
            HangupCause = HangupCause.NormalClearing,
            OccurredUtc = new DateTime(2026, 9, 24, 14, 6, 18, DateTimeKind.Utc),
            IdempotencyKey = "evt-hangup",
        };

    private sealed class StubHandler : INormalizedVoiceEventHandler
    {
        private readonly Exception _failure;
        private readonly bool _handled;
        private readonly Action _onHandle;
        private readonly bool _throwCanceled;

        public StubHandler(int order, Exception failure = null, bool handled = true, Action onHandle = null, bool throwCanceled = false)
        {
            Order = order;
            _failure = failure;
            _handled = handled;
            _onHandle = onHandle;
            _throwCanceled = throwCanceled;
        }

        public int Order { get; }

        public int Calls { get; private set; }

        public Task<bool> HandleAsync(ProviderVoiceEvent providerEvent, CancellationToken cancellationToken = default)
        {
            Calls++;
            _onHandle?.Invoke();

            if (_throwCanceled)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (_failure is not null)
            {
                throw _failure;
            }

            return Task.FromResult(_handled);
        }
    }
}
