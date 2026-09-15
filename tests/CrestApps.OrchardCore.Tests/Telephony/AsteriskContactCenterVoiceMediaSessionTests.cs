using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Doubles;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskContactCenterVoiceMediaSessionTests
{
    [Fact]
    public async Task Formats_AreMuLawEightKilohertzMono()
    {
        // Arrange
        using var sessionSocket = BindLoopback();
        using var asteriskSocket = BindLoopback();
        await using var session = CreateSession(sessionSocket, asteriskSocket);

        // Act
        var incoming = session.IncomingFormat;
        var outgoing = session.OutgoingFormat;

        // Assert
        Assert.Equal(ContactCenterVoiceMediaEncoding.MuLaw, incoming.Encoding);
        Assert.Equal(8_000, incoming.SampleRate);
        Assert.Equal(1, incoming.Channels);
        Assert.Equal(20, incoming.FrameDurationMilliseconds);
        Assert.Equal(incoming.Encoding, outgoing.Encoding);
        Assert.Equal(incoming.SampleRate, outgoing.SampleRate);
        Assert.Equal(incoming.Channels, outgoing.Channels);
        Assert.Equal(incoming.FrameDurationMilliseconds, outgoing.FrameDurationMilliseconds);
    }

    [Fact]
    public async Task ReadIncomingAsync_IgnoresUnexpectedSenderAndReturnsAsteriskFrame()
    {
        // Arrange
        using var sessionSocket = BindLoopback();
        using var asteriskSocket = BindLoopback();
        using var unexpectedSocket = BindLoopback();
        await using var session = CreateSession(sessionSocket, asteriskSocket);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var frames = session.ReadIncomingAsync(cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        var destination = LocalEndpoint(sessionSocket);
        var unexpectedPacket = AsteriskRtpPacketCodec.CreatePacket([1, 2], 10, 160, 1);
        var expectedPacket = AsteriskRtpPacketCodec.CreatePacket([3, 4, 5], 11, 320, 1);

        // Act
        await unexpectedSocket.SendAsync(unexpectedPacket, destination, cancellation.Token);
        await asteriskSocket.SendAsync(expectedPacket, destination, cancellation.Token);
        var moved = await frames.MoveNextAsync();

        // Assert
        Assert.True(moved);
        Assert.Equal(11, frames.Current.SequenceNumber);
        Assert.Equal(new byte[] { 3, 4, 5 }, frames.Current.Data.ToArray());
    }

    [Fact]
    public async Task ReadIncomingAsync_IgnoresMalformedPacket()
    {
        // Arrange
        using var sessionSocket = BindLoopback();
        using var asteriskSocket = BindLoopback();
        await using var session = CreateSession(sessionSocket, asteriskSocket);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var frames = session.ReadIncomingAsync(cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        var destination = LocalEndpoint(sessionSocket);
        var expectedPacket = AsteriskRtpPacketCodec.CreatePacket([6, 7], 12, 480, 1);

        // Act
        await asteriskSocket.SendAsync(new byte[] { 0, 1, 2 }.AsMemory(), destination, cancellation.Token);
        await asteriskSocket.SendAsync(expectedPacket, destination, cancellation.Token);
        var moved = await frames.MoveNextAsync();

        // Assert
        Assert.True(moved);
        Assert.Equal(12, frames.Current.SequenceNumber);
        Assert.Equal(new byte[] { 6, 7 }, frames.Current.Data.ToArray());
    }

    [Fact]
    public async Task WriteOutgoingAsync_SendsSequentialRtpPacketsWithContinuousTimestamps()
    {
        // Arrange
        using var sessionSocket = BindLoopback();
        using var asteriskSocket = BindLoopback();
        await using var session = CreateSession(sessionSocket, asteriskSocket);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act
        await session.WriteOutgoingAsync(new ContactCenterVoiceMediaFrame { Data = new byte[] { 1, 2, 3 } }, cancellation.Token);
        await session.WriteOutgoingAsync(new ContactCenterVoiceMediaFrame { Data = new byte[] { 4, 5 } }, cancellation.Token);
        var first = await asteriskSocket.ReceiveAsync(cancellation.Token);
        var second = await asteriskSocket.ReceiveAsync(cancellation.Token);

        // Assert
        Assert.Equal(0, BinaryPrimitives.ReadUInt16BigEndian(first.Buffer.AsSpan(2, 2)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32BigEndian(first.Buffer.AsSpan(4, 4)));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16BigEndian(second.Buffer.AsSpan(2, 2)));
        Assert.Equal(3u, BinaryPrimitives.ReadUInt32BigEndian(second.Buffer.AsSpan(4, 4)));
        Assert.Equal(
            BinaryPrimitives.ReadUInt32BigEndian(first.Buffer.AsSpan(8, 4)),
            BinaryPrimitives.ReadUInt32BigEndian(second.Buffer.AsSpan(8, 4)));
        Assert.True(AsteriskRtpPacketCodec.TryReadPayload(first.Buffer, out _, out var firstPayload));
        Assert.True(AsteriskRtpPacketCodec.TryReadPayload(second.Buffer, out _, out var secondPayload));
        Assert.Equal(new byte[] { 1, 2, 3 }, firstPayload.ToArray());
        Assert.Equal(new byte[] { 4, 5 }, secondPayload.ToArray());
    }

    [Fact]
    public async Task StopAsync_WhenCalledMoreThanOnce_StopsOnlyOnce()
    {
        // Arrange
        using var sessionSocket = BindLoopback();
        using var asteriskSocket = BindLoopback();
        var stopCount = 0;
        await using var session = CreateSession(
            sessionSocket,
            asteriskSocket,
            _ =>
            {
                stopCount++;

                return Task.CompletedTask;
            });

        // Act
        await session.StopAsync(TestContext.Current.CancellationToken);
        await session.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, stopCount);
    }

    [Fact]
    public async Task StopAsync_WhenCleanupFails_ShouldRetryCleanup()
    {
        // Arrange
        using var sessionSocket = BindLoopback();
        using var asteriskSocket = BindLoopback();
        var stopCount = 0;
        var workManager = new TestContactCenterFeatureWorkManager();
        await using var session = CreateSession(
            sessionSocket,
            asteriskSocket,
            _ =>
            {
                stopCount++;

                return stopCount == 1
                    ? Task.FromException(new InvalidOperationException("Cleanup failed."))
                    : Task.CompletedTask;
            },
            workManager.TryEnter("test"));

        // Act
        var exception = await Record.ExceptionAsync(() =>
            session.StopAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, workManager.ActiveLeaseCount);

        await session.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(2, stopCount);
        Assert.Equal(0, workManager.ActiveLeaseCount);
    }

    [Fact]
    public async Task WriteOutgoingAsync_AfterStop_ThrowsInvalidOperationException()
    {
        // Arrange
        using var sessionSocket = BindLoopback();
        using var asteriskSocket = BindLoopback();
        await using var session = CreateSession(sessionSocket, asteriskSocket);
        await session.StopAsync(TestContext.Current.CancellationToken);

        // Act
        var exception = await Record.ExceptionAsync(async () =>
            await session.WriteOutgoingAsync(
                new ContactCenterVoiceMediaFrame { Data = new byte[] { 1 } },
                TestContext.Current.CancellationToken));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    private static AsteriskContactCenterVoiceMediaSession CreateSession(
        UdpClient sessionSocket,
        UdpClient asteriskSocket,
        Func<CancellationToken, Task> stop = null,
        IContactCenterFeatureWorkLease workLease = null)
    {
        return new AsteriskContactCenterVoiceMediaSession(
            "external-1",
            "call-1",
            sessionSocket,
            LocalEndpoint(asteriskSocket),
            workLease ?? new TestContactCenterFeatureWorkManager().TryEnter("test"),
            stop ?? (_ => Task.CompletedTask));
    }

    private static UdpClient BindLoopback()
    {
        return new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
    }

    private static IPEndPoint LocalEndpoint(UdpClient client)
    {
        return (IPEndPoint)client.Client.LocalEndPoint;
    }
}
