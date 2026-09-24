using System.Buffers.Binary;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Keeps the assistant's own voice, leaking back up the caller's line, from reaching the model as if the caller
/// had said something.
/// </summary>
/// <remarks>
/// <para>
/// A phone line returns some of what is played down it: the earpiece couples into the handset microphone, and the
/// network's two-wire hybrids reflect it. On a call with poor echo cancellation that return is faint and garbled,
/// but it is still speech, and the provider's speech detector hears it as the caller starting to talk. The
/// recognizer then does what it does with faint speech-shaped noise and hallucinates a stock phrase. Live, a call
/// opened with the model answering "Bye-bye." while its greeting was still playing, and then "you" during its next
/// line — neither said by anybody, both answered — which to the person holding the phone is an assistant talking
/// to itself.
/// </para>
/// <para>
/// Echo is always quieter than a person talking into the phone, so while the assistant's audio is playing (and for
/// a short tail after, while the last of it comes back) caller audio below <see cref="BargeInLevelDbfs"/> is sent
/// to the model as silence. Audio above it, sustained long enough to be a voice rather than a click, opens the
/// gate: the caller is talking over the assistant, and interrupting it must keep working. When it opens, the
/// last <see cref="PreRollMilliseconds"/> of held audio are released as they were heard, so the soft first
/// syllable that came before the loud part is not lost. Once the assistant has finished and the tail has passed,
/// nothing is altered at all, however quiet — the short, soft "yeah" after a question is the most common thing a
/// caller says, and clipping it has already cost a call.
/// </para>
/// <para>
/// Held audio is replaced rather than dropped, so the model's view of the line stays in step with the clock.
/// The only thing ever outstanding is the onset buffer, and it is released as soon as the gate opens or the guard
/// window ends.
/// </para>
/// <para>
/// The open gate is also what lets an interruption actually stop the assistant. Letting the caller's voice through
/// is only half of barge-in: the provider then stops the model, but the model speaks faster than the line plays,
/// so the rest of its sentence is already queued at the carrier. <see cref="IsLettingCallerThrough"/> is what the
/// session reads, when the provider reports the caller has started, to decide that it was a person and not this
/// echo — and only then is the queued speech discarded. See <see cref="AssistantBargeIn"/>.
/// </para>
/// </remarks>
internal sealed class CallerEchoGuard
{
    /// <summary>
    /// How loud caller audio must be, as the RMS level of a frame, before it is treated as the caller talking over
    /// the assistant rather than the assistant's echo.
    /// </summary>
    /// <remarks>
    /// Speech into a handset sits around -20 to -26 dBFS, and a quiet talker's voiced syllables stay well above
    /// -38. Echo that survives a carrier's canceller is typically quieter than -50; a line with poor cancellation
    /// can return it in the -40s. This sits between the two, deliberately nearer the echo so a caller talking over
    /// the assistant is never held back. It only ever applies while the assistant is speaking — see the remarks
    /// on the class — and the loudest held level is logged per call so it can be checked against real lines.
    /// </remarks>
    internal const double BargeInLevelDbfs = -38d;

    /// <summary>
    /// How long caller audio must stay above the barge-in level to open the gate. A voice is sustained; a line
    /// click or a knock is one frame.
    /// </summary>
    internal const int MinimumBargeInMilliseconds = 40;

    /// <summary>
    /// How much held audio is kept, and released as heard when the gate opens or the guard ends, so the start of
    /// what the caller said is not clipped.
    /// </summary>
    internal const int PreRollMilliseconds = 240;

    /// <summary>
    /// How long the gate stays open after the caller was last loud, so the quiet between two words is not cut out
    /// of what they said.
    /// </summary>
    internal const int HangoverMilliseconds = 500;

    /// <summary>
    /// How long after the assistant's audio is projected to finish playing that its echo is still expected back.
    /// </summary>
    /// <remarks>
    /// The projection is of when the audio finishes leaving here. The far end hears it later, and its echo takes as
    /// long again to return. The last <see cref="PreRollMilliseconds"/> of the tail are released as heard when it
    /// ends, so a caller who answers the instant the assistant stops keeps the start of their answer; the audio is
    /// only ever withheld up to the difference of the two after the projected end.
    /// </remarks>
    internal const int EchoTailMilliseconds = 600;

    private static readonly double _bargeInRms = 32768d * Math.Pow(10, BargeInLevelDbfs / 20d);

    private readonly Queue<ReadOnlyMemory<byte>> _held = new();
    private readonly int _bytesPerSecond;
    private readonly int _preRollBytes;

    private byte[] _silence = [];
    private int _heldBytes;
    private bool _open;
    private int _loudBytes;
    private long _lastLoudTicks;
    private long _withheldBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallerEchoGuard"/> class.
    /// </summary>
    /// <param name="sampleRate">The rate of the 16-bit PCM the guard is given.</param>
    public CallerEchoGuard(int sampleRate = RealtimeAudioConverter.RealtimeSampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);

        _bytesPerSecond = sampleRate * 2;
        _preRollBytes = BytesFor(PreRollMilliseconds);
    }

    /// <summary>
    /// How many times the caller was let through while the assistant was speaking.
    /// </summary>
    public int Openings { get; private set; }

    /// <summary>
    /// How much caller audio has been sent to the model as silence.
    /// </summary>
    public int WithheldMilliseconds => (int)(_withheldBytes * 1000L / _bytesPerSecond);

    /// <summary>
    /// The loudest frame that was sent as silence, in dBFS, or negative infinity when none was.
    /// </summary>
    public double LoudestWithheldDbfs { get; private set; } = double.NegativeInfinity;

    /// <summary>
    /// How loud the caller was, in dBFS, the last time they were let through while the assistant was speaking.
    /// </summary>
    public double LastOpeningDbfs { get; private set; } = double.NegativeInfinity;

    /// <summary>
    /// Whether the caller is talking over the assistant right now: the gate is open on a voice, while the
    /// assistant's audio is still playing or its echo could still be coming back.
    /// </summary>
    /// <remarks>
    /// This is what tells a real interruption from a phantom one. The provider's speech detector fires on the
    /// caller, but it can also fire on the assistant's own echo — the held-back onset is released as heard, and a
    /// semantic detector is not level-based at all — so its word alone must not stop the assistant. Only when this
    /// guard has let a voice through is there somebody on the line talking over it.
    /// </remarks>
    public bool IsLettingCallerThrough => _open;

    /// <summary>
    /// Takes one frame of caller audio and releases what should go to the model, in order.
    /// </summary>
    /// <param name="pcm">The frame, 16-bit little-endian PCM at the guard's rate.</param>
    /// <param name="nowTicks">When the frame arrived, in UTC ticks.</param>
    /// <param name="assistantPlaysUntilTicks">
    /// When the assistant's audio is projected to finish playing to the caller, in UTC ticks; zero or less when it
    /// has not spoken.
    /// </param>
    /// <param name="released">Receives the audio to send to the model, which may be none, one or several chunks.</param>
    public void Process(
        ReadOnlyMemory<byte> pcm,
        long nowTicks,
        long assistantPlaysUntilTicks,
        ICollection<ReadOnlyMemory<byte>> released)
    {
        ArgumentNullException.ThrowIfNull(released);

        if (pcm.IsEmpty)
        {
            return;
        }

        var guarding = assistantPlaysUntilTicks > 0 &&
            nowTicks < assistantPlaysUntilTicks + (EchoTailMilliseconds * TimeSpan.TicksPerMillisecond);

        if (!guarding)
        {
            // The assistant has finished and its echo has had time to come back. What is still held is the end of
            // the tail — past the echo, and possibly the start of an answer — so it goes as it was heard.
            ReleaseHeld(released);
            _open = false;
            _loudBytes = 0;
            released.Add(pcm);

            return;
        }

        var rms = Rms(pcm.Span);
        var loud = rms >= _bargeInRms;

        if (loud)
        {
            _loudBytes += pcm.Length;
            _lastLoudTicks = nowTicks;
        }
        else
        {
            _loudBytes = 0;
        }

        if (_open && !loud && nowTicks - _lastLoudTicks > HangoverMilliseconds * TimeSpan.TicksPerMillisecond)
        {
            // The caller has stopped and the assistant is still talking, so what follows is its echo again.
            _open = false;
        }

        if (!_open && _loudBytes >= BytesFor(MinimumBargeInMilliseconds))
        {
            // The caller is talking over the assistant. Let them through from the start of what they said.
            _open = true;
            Openings++;
            LastOpeningDbfs = ToDbfs(rms);
            ReleaseHeld(released);
        }

        if (_open)
        {
            released.Add(pcm);

            return;
        }

        _held.Enqueue(pcm);
        _heldBytes += pcm.Length;

        // Only the onset buffer is kept. Anything older is echo nobody talked over, and goes as silence.
        while (_heldBytes > _preRollBytes && _held.Count > 0)
        {
            var oldest = _held.Dequeue();
            _heldBytes -= oldest.Length;
            released.Add(Withhold(oldest));
        }
    }

    private void ReleaseHeld(ICollection<ReadOnlyMemory<byte>> released)
    {
        while (_held.Count > 0)
        {
            released.Add(_held.Dequeue());
        }

        _heldBytes = 0;
    }

    private ReadOnlyMemory<byte> Withhold(ReadOnlyMemory<byte> pcm)
    {
        _withheldBytes += pcm.Length;

        var level = ToDbfs(Rms(pcm.Span));

        if (level > LoudestWithheldDbfs)
        {
            LoudestWithheldDbfs = level;
        }

        // One shared buffer of zeros, never written after it is made: the sender only reads what it is given.
        if (_silence.Length < pcm.Length)
        {
            _silence = new byte[pcm.Length];
        }

        return _silence.AsMemory(0, pcm.Length);
    }

    private int BytesFor(int milliseconds)
        => (int)((long)_bytesPerSecond * milliseconds / 1000);

    private static double Rms(ReadOnlySpan<byte> pcm)
    {
        var count = pcm.Length / 2;

        if (count == 0)
        {
            return 0d;
        }

        double sum = 0;

        for (var i = 0; i < count; i++)
        {
            double sample = BinaryPrimitives.ReadInt16LittleEndian(pcm.Slice(i * 2, 2));
            sum += sample * sample;
        }

        return Math.Sqrt(sum / count);
    }

    private static double ToDbfs(double rms)
        => rms <= 0d ? double.NegativeInfinity : 20d * Math.Log10(rms / 32768d);
}
