using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.WebSockets;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class TelnyxContactCenterVoiceMediaSessionTests
{
    [Fact]
    public async Task ReadIncomingAsync_YieldsMediaPayloads_IgnoresOtherEvents_EndsOnStop()
    {
        // Arrange
        var firstPayload = new byte[] { 0xFF, 0x7F, 0x10 };
        var secondPayload = new byte[] { 0x01, 0x02 };
        using var socket = new FakeWebSocket();
        socket.EnqueueText("""{"event":"connected","version":"1.0.0"}""");
        socket.EnqueueText("""{"event":"start","start":{"media_format":{"encoding":"PCMU"}}}""");
        socket.EnqueueText(MediaEvent(firstPayload));
        socket.EnqueueText("""{"event":"dtmf","dtmf":{"digit":"1"}}""");
        socket.EnqueueText(MediaEvent(secondPayload));
        socket.EnqueueText("""{"event":"stop","stop":{}}""");

        var (session, _, _) = CreateSession(socket);

        // Act
        var frames = new List<ContactCenterVoiceMediaFrame>();

        await foreach (var frame in session.ReadIncomingAsync(TestContext.Current.CancellationToken))
        {
            frames.Add(frame);
        }

        // Assert
        Assert.Collection(
            frames,
            frame =>
            {
                Assert.Equal(firstPayload, frame.Data.ToArray());
                Assert.Equal(0, frame.SequenceNumber);
            },
            frame =>
            {
                Assert.Equal(secondPayload, frame.Data.ToArray());
                Assert.Equal(1, frame.SequenceNumber);
            });
    }

    [Fact]
    public async Task WriteOutgoingAsync_SendsMediaMessageWithBase64Payload()
    {
        // Arrange
        using var socket = new FakeWebSocket();
        var (session, _, _) = CreateSession(socket);
        var payload = new byte[] { 0xAA, 0xBB, 0xCC };

        // Act
        await session.WriteOutgoingAsync(
            new ContactCenterVoiceMediaFrame { Data = payload },
            TestContext.Current.CancellationToken);

        // Assert
        var message = Assert.Single(socket.SentTextMessages);
        using var document = JsonDocument.Parse(message);
        Assert.Equal("media", document.RootElement.GetProperty("event").GetString());
        var sent = document.RootElement.GetProperty("media").GetProperty("payload").GetBytesFromBase64();
        Assert.Equal(payload, sent);
    }

    [Fact]
    public async Task StopAsync_StopsStreaming_ReleasesSocketConnectionAndLease()
    {
        // Arrange
        using var socket = new FakeWebSocket();
        var (session, workManager, connection) = CreateSession(socket, out var stopCalls);

        // Act
        await session.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(stopCalls);
        Assert.True(socket.Aborted);
        Assert.True(socket.Disposed);
        Assert.True(connection.ReleasedTask.IsCompletedSuccessfully);
        Assert.Equal(0, workManager.ActiveLeaseCount);
    }

    [Fact]
    public async Task StopAsync_StopsStreaming_BeforeTheSocketIsDropped()
    {
        // Arrange
        using var socket = new FakeWebSocket();
        var abortedWhenStopped = new List<bool>();
        var workManager = new TestContactCenterFeatureWorkManager();
        var session = new TelnyxContactCenterVoiceMediaSession(
            "session-1",
            "call-1",
            socket,
            workManager.TryEnter("media"),
            new WebSocketRendezvous(),
            _ =>
            {
                abortedWhenStopped.Add(socket.Aborted);

                return Task.CompletedTask;
            });

        // Act
        await session.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(Assert.Single(abortedWhenStopped));
        Assert.True(socket.Aborted);
    }

    [Fact]
    public async Task ReadIncomingAsync_Cancelled_StopsStreamingBeforeTheSocketIsDropped()
    {
        // Arrange
        using var socket = new FakeWebSocket();
        var abortedWhenStopped = new List<bool>();
        var workManager = new TestContactCenterFeatureWorkManager();
        var session = new TelnyxContactCenterVoiceMediaSession(
            "session-1",
            "call-1",
            socket,
            workManager.TryEnter("media"),
            new WebSocketRendezvous(),
            _ =>
            {
                abortedWhenStopped.Add(socket.Aborted);

                return Task.CompletedTask;
            });

        using var reading = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var frames = new List<ContactCenterVoiceMediaFrame>();

        // Act: the conversation ends while a receive is pending.
        var readLoop = Task.Run(async () =>
        {
            await foreach (var frame in session.ReadIncomingAsync(reading.Token))
            {
                frames.Add(frame);
            }
        }, TestContext.Current.CancellationToken);

        await Task.Delay(50, TestContext.Current.CancellationToken);
        await reading.CancelAsync();
        await readLoop.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(Assert.Single(abortedWhenStopped));
        Assert.True(socket.Aborted);
    }

    [Fact]
    public async Task WriteOutgoingAsync_AfterStop_Throws()
    {
        // Arrange
        using var socket = new FakeWebSocket();
        var (session, _, _) = CreateSession(socket);
        await session.StopAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await session.WriteOutgoingAsync(
                new ContactCenterVoiceMediaFrame { Data = new byte[] { 1 } },
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StopAsync_CalledTwice_StopsStreamingOnce()
    {
        // Arrange
        using var socket = new FakeWebSocket();
        var (session, _, _) = CreateSession(socket, out var stopCalls);

        // Act
        await session.StopAsync(TestContext.Current.CancellationToken);
        await session.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(stopCalls);
    }

    [Fact]
    public async Task ClearOutgoingAsync_TellsTelnyxToDiscardTheAudioItHasQueued()
    {
        // Arrange
        // The model speaks faster than the line plays, so seconds of it wait at Telnyx. A caller who talks over the
        // assistant hears all of it unless Telnyx is told to drop it.
        using var socket = new FakeWebSocket();
        var (session, _, _) = CreateSession(socket);
        await session.WriteOutgoingAsync(
            new ContactCenterVoiceMediaFrame { Data = new byte[] { 0xAA } },
            TestContext.Current.CancellationToken);

        // Act
        await ((IContactCenterVoiceMediaSession)session).ClearOutgoingAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, socket.SentTextMessages.Count);
        using var document = JsonDocument.Parse(socket.SentTextMessages[^1]);
        Assert.Equal("clear", document.RootElement.GetProperty("event").GetString());
    }

    [Fact]
    public async Task ClearOutgoingAsync_WaitsForASendAlreadyInFlight()
    {
        // Arrange
        // A WebSocket takes one send at a time, and the clear is issued while the assistant's audio and the room
        // bed are both being written. Overlapping them faults the socket and ends the call.
        using var socket = new FakeWebSocket();
        var (session, _, _) = CreateSession(socket);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        socket.SendGate = () =>
        {
            entered.TrySetResult();

            return release.Task;
        };

        var write = session.WriteOutgoingAsync(
            new ContactCenterVoiceMediaFrame { Data = new byte[] { 0xAA } },
            TestContext.Current.CancellationToken).AsTask();
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Act
        var clear = ((IContactCenterVoiceMediaSession)session).ClearOutgoingAsync(TestContext.Current.CancellationToken).AsTask();
        await Task.Delay(50, TestContext.Current.CancellationToken);
        var clearedWhileWriting = clear.IsCompleted;
        release.SetResult();
        await Task.WhenAll(write, clear);

        // Assert
        Assert.False(clearedWhileWriting);
        Assert.Equal(1, socket.MostConcurrentSends);
        Assert.Contains("clear", socket.SentTextMessages[^1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClearOutgoingAsync_AfterStop_DoesNothing()
    {
        // Arrange
        // A caller talking over the assistant as the call ends is not a fault; there is simply nothing left to clear.
        using var socket = new FakeWebSocket();
        var (session, _, _) = CreateSession(socket);
        await session.StopAsync(TestContext.Current.CancellationToken);

        // Act
        await ((IContactCenterVoiceMediaSession)session).ClearOutgoingAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(socket.SentTextMessages);
    }

    private static string MediaEvent(byte[] payload)
        => "{\"event\":\"media\",\"media\":{\"payload\":\"" + Convert.ToBase64String(payload) + "\"}}";

    private static (TelnyxContactCenterVoiceMediaSession Session, TestContactCenterFeatureWorkManager WorkManager, WebSocketRendezvous Connection) CreateSession(FakeWebSocket socket)
        => CreateSession(socket, out _);

    private static (TelnyxContactCenterVoiceMediaSession Session, TestContactCenterFeatureWorkManager WorkManager, WebSocketRendezvous Connection) CreateSession(
        FakeWebSocket socket,
        out List<int> stopCalls)
    {
        var workManager = new TestContactCenterFeatureWorkManager();
        var lease = workManager.TryEnter("media");
        var connection = new WebSocketRendezvous();
        var calls = new List<int>();
        stopCalls = calls;

        var session = new TelnyxContactCenterVoiceMediaSession(
            "session-1",
            "call-1",
            socket,
            lease,
            connection,
            _ =>
            {
                calls.Add(1);

                return Task.CompletedTask;
            });

        return (session, workManager, connection);
    }
}
