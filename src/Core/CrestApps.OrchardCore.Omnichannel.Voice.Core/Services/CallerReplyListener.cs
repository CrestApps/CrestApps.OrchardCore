using System.Buffers.Binary;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Notices the caller answering after the assistant has finished speaking, so a reply the provider's speech
/// detector never picked up can be told apart from a caller who has not answered yet.
/// </summary>
/// <remarks>
/// <para>
/// The provider decides when the caller is talking, and on a phone line it sometimes does not decide it at all for
/// a short word. Live, the assistant read an email address back, asked "did I get that right?", and the caller's
/// clear "yes" a second later reached the model and was never reported as speech: the assistant waited for an
/// answer it had been given, and the caller waited seven seconds before saying it again. Nothing on the provider's
/// side shows that happened. What does show it is the line itself: a voice, after the assistant stopped, that the
/// provider never said it heard.
/// </para>
/// <para>
/// Only voice heard once the assistant's audio has finished counts. Anything while it is still playing is either
/// its own echo or the caller talking over it, and both have their own handling (see <see cref="CallerEchoGuard"/>).
/// </para>
/// <para>
/// One pump writes and another thread reads, so what is read is published with interlocked operations.
/// </para>
/// </remarks>
internal sealed class CallerReplyListener
{
    /// <summary>
    /// How loud a frame must be to count as the caller's voice. A quiet talker's voiced syllables sit around -44
    /// dBFS on a handset; this is just below that, and well above a quiet line.
    /// </summary>
    internal const double VoiceLevelDbfs = -46d;

    /// <summary>
    /// How much voice makes a reply. A "yes" is a couple of hundred milliseconds; a click or a knock is one frame.
    /// </summary>
    internal const int MinimumReplyMilliseconds = 120;

    /// <summary>
    /// How long a reply may go quiet between two loud stretches and still be one reply: the gap inside "yeah, yes".
    /// </summary>
    internal const int GapMilliseconds = 300;

    private static readonly double _voiceRms = 32768d * Math.Pow(10, VoiceLevelDbfs / 20d);

    private readonly int _bytesPerSecond;

    // Written and read only by the caller pump.
    private long _burstStartTicks;
    private long _lastLoudTicks;
    private int _loudBytes;

    // Published to the watchdog.
    private long _replyStartTicks;
    private long _replyEndTicks;

    // When any voice was last heard after the assistant finished, for the watchdog.
    private long _lastVoiceTicks;

    // Read and written only by the watchdog.
    private long _lastTakenReplyStartTicks;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallerReplyListener"/> class.
    /// </summary>
    /// <param name="sampleRate">The rate of the 16-bit PCM the listener is given.</param>
    public CallerReplyListener(int sampleRate = RealtimeAudioConverter.RealtimeSampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);

        _bytesPerSecond = sampleRate * 2;
    }

    /// <summary>
    /// Takes one frame of caller audio as it arrives.
    /// </summary>
    /// <param name="pcm">The frame, 16-bit little-endian PCM at the listener's rate.</param>
    /// <param name="nowTicks">When the frame arrived, in UTC ticks.</param>
    /// <param name="assistantPlaysUntilTicks">
    /// When the assistant's audio is projected to finish playing to the caller, in UTC ticks; zero or less when it
    /// has not spoken.
    /// </param>
    public void Hear(ReadOnlySpan<byte> pcm, long nowTicks, long assistantPlaysUntilTicks)
    {
        if (pcm.IsEmpty)
        {
            return;
        }

        if (nowTicks < assistantPlaysUntilTicks)
        {
            // The assistant is still talking. Whatever is on the line is not a reply to it yet.
            _burstStartTicks = 0;
            _loudBytes = 0;

            return;
        }

        if (Rms(pcm) < _voiceRms)
        {
            return;
        }

        if (_burstStartTicks == 0 || nowTicks - _lastLoudTicks > GapMilliseconds * TimeSpan.TicksPerMillisecond)
        {
            // The frame arrives once it has been heard, so the voice began one frame earlier.
            _burstStartTicks = nowTicks - DurationTicks(pcm.Length);
            _loudBytes = 0;
        }

        _loudBytes += pcm.Length;
        _lastLoudTicks = nowTicks;
        Interlocked.Exchange(ref _lastVoiceTicks, nowTicks);

        if (_loudBytes >= BytesFor(MinimumReplyMilliseconds))
        {
            Interlocked.Exchange(ref _replyStartTicks, _burstStartTicks);
            Interlocked.Exchange(ref _replyEndTicks, nowTicks);
        }
    }

    /// <summary>
    /// Gets when the caller's latest reply began on the line, in UTC ticks, or zero when none has been heard.
    /// </summary>
    public long LatestReplyStartTicks => Interlocked.Read(ref _replyStartTicks);

    /// <summary>
    /// Takes the caller's latest reply when it has gone unheard: it finished at least <paramref name="wait"/> ago,
    /// and the provider has not reported the caller speaking since it began. Each reply is taken at most once.
    /// </summary>
    /// <param name="nowTicks">The current time, in UTC ticks.</param>
    /// <param name="providerHeardCallerTicks">When the provider last reported the caller speaking, in UTC ticks.</param>
    /// <param name="wait">How long after the reply the provider is given to report it.</param>
    /// <returns><see langword="true"/> when there is a reply the provider has not heard; otherwise <see langword="false"/>.</returns>
    public bool TryTakeUnheardReply(long nowTicks, long providerHeardCallerTicks, TimeSpan wait)
    {
        var start = Interlocked.Read(ref _replyStartTicks);
        var end = Interlocked.Read(ref _replyEndTicks);

        if (start == 0 || start <= _lastTakenReplyStartTicks || nowTicks - end < wait.Ticks)
        {
            return false;
        }

        // Somebody is talking on the line right now, too briefly yet to be a reply of its own. Live, a soft "um"
        // two seconds before the answer was taken for a reply the provider missed, and the assistant asked for it
        // again just as the caller started to give it. Wait for them to finish.
        if (nowTicks - Interlocked.Read(ref _lastVoiceTicks) < GapMilliseconds * TimeSpan.TicksPerMillisecond)
        {
            return false;
        }

        // The provider reports speech some time after it begins, but never before: anything it reported from the
        // start of the reply on was this reply being heard.
        if (providerHeardCallerTicks >= start)
        {
            _lastTakenReplyStartTicks = start;

            return false;
        }

        _lastTakenReplyStartTicks = start;

        return true;
    }

    private long DurationTicks(int byteCount)
        => byteCount * TimeSpan.TicksPerSecond / _bytesPerSecond;

    private int BytesFor(int milliseconds)
        => (int)((long)_bytesPerSecond * milliseconds / 1000);

    private static double Rms(ReadOnlySpan<byte> pcm)
    {
        var samples = pcm.Length / 2;

        if (samples == 0)
        {
            return 0;
        }

        double sum = 0;

        for (var i = 0; i < samples; i++)
        {
            double sample = BinaryPrimitives.ReadInt16LittleEndian(pcm.Slice(i * 2, 2));
            sum += sample * sample;
        }

        return Math.Sqrt(sum / samples);
    }
}
