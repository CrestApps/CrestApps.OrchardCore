using System.Net;
using System.Net.Sockets;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed class AsteriskContactCenterVoiceMediaSession : IContactCenterVoiceMediaSession
{
    private readonly UdpClient _udpClient;
    private readonly IPEndPoint _asteriskMediaEndpoint;
    private readonly Func<CancellationToken, Task> _stop;
    private readonly SemaphoreSlim _stopLock = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly uint _synchronizationSource = unchecked((uint)Random.Shared.NextInt64());
    private ushort _sequenceNumber;
    private uint _timestamp;
    private int _cleanupCompleted;
    private int _stopped;

    public AsteriskContactCenterVoiceMediaSession(
        string sessionId,
        string providerCallId,
        UdpClient udpClient,
        IPEndPoint asteriskMediaEndpoint,
        Func<CancellationToken, Task> stop)
    {
        SessionId = sessionId;
        ProviderCallId = providerCallId;
        _udpClient = udpClient;
        _asteriskMediaEndpoint = asteriskMediaEndpoint;
        _stop = stop;
    }

    public string SessionId { get; }

    public string ProviderCallId { get; }

    public ContactCenterVoiceMediaFormat IncomingFormat { get; } = CreateFormat();

    public ContactCenterVoiceMediaFormat OutgoingFormat { get; } = CreateFormat();

    public async IAsyncEnumerable<ContactCenterVoiceMediaFrame> ReadIncomingAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested && Volatile.Read(ref _stopped) == 0)
        {
            UdpReceiveResult result;

            try
            {
                result = await _udpClient.ReceiveAsync(cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                yield break;
            }
            catch (SocketException) when (Volatile.Read(ref _stopped) != 0)
            {
                yield break;
            }

            if (!result.RemoteEndPoint.Equals(_asteriskMediaEndpoint))
            {
                continue;
            }

            if (!AsteriskRtpPacketCodec.TryReadPayload(
                result.Buffer,
                out var sequenceNumber,
                out var payload))
            {
                continue;
            }

            yield return new ContactCenterVoiceMediaFrame
            {
                SequenceNumber = sequenceNumber,
                Data = payload.ToArray(),
            };
        }
    }

    public async ValueTask WriteOutgoingAsync(
        ContactCenterVoiceMediaFrame frame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (Volatile.Read(ref _stopped) != 0)
        {
            throw new InvalidOperationException("The Asterisk media session has already stopped.");
        }

        await _writeLock.WaitAsync(cancellationToken);

        try
        {
            var packet = AsteriskRtpPacketCodec.CreatePacket(
                frame.Data.Span,
                _sequenceNumber++,
                _timestamp,
                _synchronizationSource);

            _timestamp += (uint)frame.Data.Length;

            await _udpClient.SendAsync(packet, _asteriskMediaEndpoint, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Exchange(ref _stopped, 1);
        _udpClient.Dispose();

        await _stopLock.WaitAsync(CancellationToken.None);

        try
        {
            if (Volatile.Read(ref _cleanupCompleted) != 0)
            {
                return;
            }

            using var cleanupCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _stop(cleanupCancellation.Token);
            Volatile.Write(ref _cleanupCompleted, 1);
        }
        finally
        {
            _stopLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _stopLock.Dispose();
        _writeLock.Dispose();
    }

    private static ContactCenterVoiceMediaFormat CreateFormat()
    {
        return new ContactCenterVoiceMediaFormat
        {
            Encoding = ContactCenterVoiceMediaEncoding.MuLaw,
            SampleRate = 8_000,
            Channels = 1,
            FrameDurationMilliseconds = 20,
        };
    }
}
