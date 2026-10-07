using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.WebSockets;
using CrestApps.OrchardCore.WebSockets.Services;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// A Telnyx media stream that breaks mid-call. Live, Telnyx dialed the stream back in and was refused because the
/// token had been used, and the assistant then talked on to nobody until the caller hung up.
/// </summary>
public sealed class TelnyxMediaStreamReconnectTests
{
    private const string Token = "stream-token";
    private const string CallId = "call-1";

    private static readonly TimeSpan _grace = TimeSpan.FromMilliseconds(600);

    [Fact]
    public async Task ASocketThatCloses_IsReplacedByTheOneTelnyxDialsBackIn_AndTheCallCarriesOn()
    {
        // Arrange
        var registry = new InMemoryWebSocketConnectionRegistry();
        using var first = new FakeWebSocket();
        first.EnqueueText(MediaEvent([0x01]));
        first.EnqueueClose();
        using var second = new FakeWebSocket();
        second.EnqueueText(MediaEvent([0x02]));
        second.EnqueueText("""{"event":"stop","stop":{}}""");
        var (session, connection) = CreateSession(first, registry);

        // Act
        var reading = ReadAllAsync(session);
        var reconnect = await ClaimWhenRegisteredAsync(registry);
        Assert.True(reconnect.TryComplete(second));
        var frames = await reading;

        // Assert
        Assert.Equal([new byte[] { 0x01 }, new byte[] { 0x02 }], frames);
        Assert.True(first.Aborted);
        Assert.True(connection.ReleasedTask.IsCompletedSuccessfully);

        await session.DisposeAsync();
    }

    [Fact]
    public async Task ASocketThatNeverCloses_IsReplacedByTheOneTelnyxDialsBackIn()
    {
        // Arrange: the broken socket stays open on this side, as one dropped by a proxy does.
        var registry = new InMemoryWebSocketConnectionRegistry();
        using var first = new FakeWebSocket();
        using var second = new FakeWebSocket();
        second.EnqueueText(MediaEvent([0x07]));
        second.EnqueueText("""{"event":"stop","stop":{}}""");
        var (session, _) = CreateSession(first, registry);

        // Act
        var reading = ReadAllAsync(session);
        var reconnect = await ClaimWhenRegisteredAsync(registry);
        Assert.True(reconnect.TryComplete(second));
        var frames = await reading;

        // Assert
        Assert.Equal([new byte[] { 0x07 }], frames);
        Assert.True(first.Aborted);

        await session.DisposeAsync();
    }

    [Fact]
    public async Task AReportedFailure_StartsTheStreamAgain_AndTheRestartedStreamCarriesTheCall()
    {
        // Arrange
        var registry = new InMemoryWebSocketConnectionRegistry();
        var tracker = new TelnyxMediaStreamTracker();
        using var first = new FakeWebSocket();
        using var second = new FakeWebSocket();
        second.EnqueueText(MediaEvent([0x09]));
        second.EnqueueText("""{"event":"stop","stop":{}}""");
        var restarts = 0;

        var (session, _) = CreateSession(first, registry, tracker, async _ =>
        {
            restarts++;

            // What Telnyx does with a restarted stream: dial the same URL again.
            var reconnect = await registry.TryClaimAsync(Token, TestContext.Current.CancellationToken);
            reconnect.TryComplete(second);
        });

        // Act
        var reading = ReadAllAsync(session);
        await WaitUntilAsync(() => IsRegistered(registry));
        Assert.True(tracker.NotifyStreamFailed(CallId));
        var frames = await reading;

        // Assert
        Assert.Equal(1, restarts);
        Assert.Equal([new byte[] { 0x09 }], frames);

        await session.DisposeAsync();
    }

    [Fact]
    public async Task AReportedFailure_ThatDoesNotComeBack_EndsTheReadAsLost()
    {
        // Arrange: the socket never closes and the restart never connects.
        var registry = new InMemoryWebSocketConnectionRegistry();
        var tracker = new TelnyxMediaStreamTracker();
        using var first = new FakeWebSocket();
        var (session, _) = CreateSession(first, registry, tracker, _ => Task.CompletedTask);

        // Act
        var reading = ReadAllAsync(session);
        await WaitUntilAsync(() => IsRegistered(registry));
        tracker.NotifyStreamFailed(CallId);

        // Assert
        await Assert.ThrowsAsync<ContactCenterVoiceMediaLostException>(() => reading);
        Assert.True(first.Aborted);

        await session.DisposeAsync();
    }

    [Fact]
    public async Task ASocketThatCloses_WithNoFailureReported_EndsQuietly()
    {
        // Arrange: the call ended without a stop message, and nothing dials back in.
        var registry = new InMemoryWebSocketConnectionRegistry();
        using var first = new FakeWebSocket();
        first.EnqueueText(MediaEvent([0x03]));
        first.EnqueueClose();
        var (session, _) = CreateSession(first, registry);

        // Act
        var frames = await ReadAllAsync(session);

        // Assert
        Assert.Equal([new byte[] { 0x03 }], frames);

        await session.DisposeAsync();
    }

    [Fact]
    public async Task StoppingTheSession_WithdrawsTheToken_SoNothingCanDialBackIn()
    {
        // Arrange
        var registry = new InMemoryWebSocketConnectionRegistry();
        var tracker = new TelnyxMediaStreamTracker();
        using var first = new FakeWebSocket();
        var (session, _) = CreateSession(first, registry, tracker);
        await WaitUntilAsync(() => IsRegistered(registry));

        // Act
        await session.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(await registry.TryClaimAsync(Token, TestContext.Current.CancellationToken));
        Assert.False(tracker.NotifyStreamFailed(CallId));
    }

    private static (TelnyxContactCenterVoiceMediaSession Session, WebSocketRendezvous Connection) CreateSession(
        FakeWebSocket socket,
        InMemoryWebSocketConnectionRegistry registry,
        TelnyxMediaStreamTracker tracker = null,
        Func<CancellationToken, Task> restart = null)
    {
        var workManager = new TestContactCenterFeatureWorkManager();
        var connection = new WebSocketRendezvous();

        var session = new TelnyxContactCenterVoiceMediaSession(
            "session-1",
            CallId,
            socket,
            workManager.TryEnter("media"),
            connection,
            _ => Task.CompletedTask,
            new TelnyxMediaStreamReconnect
            {
                Registry = registry,
                Token = Token,
                RestartStreaming = restart ?? (_ => Task.CompletedTask),
                Tracker = tracker,
                Grace = _grace,
            });

        return (session, connection);
    }

    private static async Task<List<byte[]>> ReadAllAsync(TelnyxContactCenterVoiceMediaSession session)
    {
        var frames = new List<byte[]>();

        await foreach (var frame in session.ReadIncomingAsync(TestContext.Current.CancellationToken))
        {
            frames.Add(frame.Data.ToArray());
        }

        return frames;
    }

    // The session keeps the token registered from the start; a test plays Telnyx by claiming it.
    private static async Task<WebSocketRendezvous> ClaimWhenRegisteredAsync(IWebSocketConnectionRegistry registry)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (DateTime.UtcNow < deadline)
        {
            var rendezvous = await registry.TryClaimAsync(Token, TestContext.Current.CancellationToken);

            if (rendezvous is not null)
            {
                return rendezvous;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("The session never made its token claimable.");
    }

    // Claiming would consume the standby, so registration is detected by trying to register the token again.
    private static bool IsRegistered(InMemoryWebSocketConnectionRegistry registry)
    {
        try
        {
            registry.RegisterAsync(Token).GetAwaiter().GetResult();
            registry.RemoveAsync(Token).GetAwaiter().GetResult();

            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The condition was never met.");
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    private static string MediaEvent(byte[] payload)
        => "{\"event\":\"media\",\"media\":{\"payload\":\"" + Convert.ToBase64String(payload) + "\"}}";
}
