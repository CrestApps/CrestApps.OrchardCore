namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Brings the assistant's voice to a steady speaking level on the line, whichever model is producing it.
/// </summary>
/// <remarks>
/// <para>
/// Realtime models do not all speak at the same level. On the same call path, one spoke at around -22 dBFS and its
/// successor at around -36 dBFS -- a fifth of the loudness -- and callers asked the assistant why it sounded so low.
/// Nothing on the line was wrong; the model was simply quieter. The level is measured here and evened out, so a
/// change of model or voice does not change how loud the assistant is on the phone.
/// </para>
/// <para>
/// It behaves like a hand on a volume knob rather than a compressor. The first version followed the level within
/// about an eighth of a second, which is inside a word: the quiet ends of words, the breaths and the model's own
/// background were lifted along with the speech, and on the quieter model -- which needs about 14 dB -- callers
/// heard a voice that sounded less clean than the louder model's. Measured on the recordings, the quieter model's
/// low-level sound relative to its speech was half as much again as the louder one's. So the estimate settles
/// quickly on the first words and then moves only over a second or more, only speech near the current level moves
/// it at all, and the stretches between words get half the boost the speech gets.
/// </para>
/// <para>
/// Any sample that would clip still turns the gain down at once.
/// </para>
/// </remarks>
internal sealed class AssistantVoiceLeveler
{
    /// <summary>
    /// The speaking level the voice is brought to, in dBFS, as this measures it: speech only, not the gaps.
    /// </summary>
    /// <remarks>
    /// Set just under where the model callers heard clearly speaks: measured by this class on a live call, before
    /// any gain, it spoke at -21.0, so it is never lifted. At -24 the quieter model still came out about 4 dB below
    /// it on the recordings (speech medians of -23.6 and -19.8), because its speech has more soft syllables at the
    /// same measured level; -22 halves that and still leaves the louder model untouched.
    /// </remarks>
    internal const double TargetDbfs = -22d;

    /// <summary>
    /// The most the voice is ever raised, in dB.
    /// </summary>
    internal const double MaxGainDb = 18d;

    /// <summary>
    /// The most the voice is ever lowered, in dB: not at all. Only a model quieter than the target is lifted; one at
    /// or above it -- the model callers already heard clearly -- passes through exactly as it spoke, its speech and
    /// the gaps in it alike. Nothing here knows which model is speaking: the level is measured on each call.
    /// </summary>
    internal const double MinGainDb = 0d;

    /// <summary>
    /// How far below the voice's level a block may be and still count as its speech, in dB. Anything quieter is
    /// the tail of a word, a breath or the room, which neither moves the estimate nor gets the speech's full gain.
    /// </summary>
    internal const double SpeechWindowDb = 18d;

    // Blocks quieter than this are silence, whatever the voice's level.
    private const double SpeechGateDbfs = -55d;

    // The peak any sample may reach after gain, as a fraction of full scale.
    private const double PeakCeiling = 0.85d;

    // How quickly the estimate follows speech while it settles on the first words, per block.
    private const double SettlingSmoothing = 0.08d;

    // How many speech blocks the estimate settles over: half a second of speech.
    private const int SettlingBlocks = 50;

    // How quickly the estimate follows speech once it has settled, per block: over a second and a half or so, so
    // the gain follows the model's level from sentence to sentence and never within a word.
    private const double SettledSmoothing = 0.0067d;

    private const double FullScale = 32768d;

    private readonly int _blockSamples;
    private readonly double _riseStep;
    private readonly double _fallStep;
    private double _levelDb = TargetDbfs;
    private int _speechBlocks;
    private double _gain = 1d;
    private double _targetGain = 1d;
    private int _blockPosition;
    private double _blockSumOfSquares;
    private double _speechGainDbSum;
    private int _limitedSamples;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssistantVoiceLeveler"/> class.
    /// </summary>
    /// <param name="sampleRate">The sample rate of the audio it levels.</param>
    public AssistantVoiceLeveler(int sampleRate)
    {
        // 10 ms blocks: long enough to measure a level, short enough to follow it.
        _blockSamples = Math.Max(1, (sampleRate > 0 ? sampleRate : 8_000) / 100);

        // A rise is spread over a couple of blocks, so the start of a word is never heard as a step. A fall is
        // spread over about a seventh of a second, so the end of a word fades as the model said it rather than
        // being cut off when the gap after it gets less gain.
        _riseStep = 1d / (_blockSamples * 2);
        _fallStep = 1d / (_blockSamples * 15);
    }

    /// <summary>
    /// Gets the gain currently applied, in dB.
    /// </summary>
    internal double CurrentGainDb => 20d * Math.Log10(_gain);

    /// <summary>
    /// Gets the level the voice is measured to speak at before any gain, in dBFS.
    /// </summary>
    internal double SpeechLevelDbfs => _levelDb;

    /// <summary>
    /// Gets how much speech has been measured, in milliseconds.
    /// </summary>
    internal int SpeechMilliseconds => _speechBlocks * 10;

    /// <summary>
    /// Gets the average gain given to the voice's speech, in dB, or zero before any speech.
    /// </summary>
    internal double AverageSpeechGainDb => _speechBlocks > 0 ? _speechGainDbSum / _speechBlocks : 0d;

    /// <summary>
    /// Gets how many samples were turned down so as not to clip.
    /// </summary>
    internal int LimitedSamples => _limitedSamples;

    /// <summary>
    /// Levels the samples in place. Samples are on the 16-bit scale.
    /// </summary>
    /// <remarks>
    /// Sample by sample, with the measuring block carried from one call to the next, so the voice comes out the same
    /// however the model's audio happens to be split into pieces.
    /// </remarks>
    /// <param name="samples">The samples to level.</param>
    public void Process(List<double> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        for (var i = 0; i < samples.Count; i++)
        {
            var value = samples[i];

            // Both directions are eased, so moving between speech and the gaps in it is never heard as a step.
            _gain += (_targetGain - _gain) * (_gain < _targetGain ? _riseStep : _fallStep);

            // A sample that would clip turns the gain down for it and what follows, until the next block decides.
            var magnitude = Math.Abs(value);

            if (magnitude > 0 && magnitude * _gain > PeakCeiling * FullScale)
            {
                _gain = PeakCeiling * FullScale / magnitude;
                _limitedSamples++;
            }

            samples[i] = value * _gain;

            _blockSumOfSquares += value * value;

            if (++_blockPosition == _blockSamples)
            {
                EndBlock();
            }
        }
    }

    private void EndBlock()
    {
        var rms = Math.Sqrt(_blockSumOfSquares / _blockSamples) / FullScale;
        var blockDb = rms > 0 ? 20d * Math.Log10(rms) : double.NegativeInfinity;

        _blockPosition = 0;
        _blockSumOfSquares = 0d;

        var settling = _speechBlocks < SettlingBlocks;

        // While settling, anything above silence counts, so a very quiet model is found at all. After that only
        // speech near the voice's level does, so breaths and the quiet ends of words do not drag the estimate down
        // and earn the speech more gain than it needs.
        var speech = blockDb > SpeechGateDbfs && (settling || blockDb > _levelDb - SpeechWindowDb);

        var speechGainDb = Math.Clamp(TargetDbfs - _levelDb, MinGainDb, MaxGainDb);

        if (!speech)
        {
            // Between words: half the boost, so the gaps rise with the voice without the room, the breaths and
            // whatever the model has under its speech being lifted as far as the words are.
            _targetGain = Math.Pow(10d, Math.Min(speechGainDb, speechGainDb / 2d) / 20d);

            return;
        }

        _levelDb += (blockDb - _levelDb) * (settling ? SettlingSmoothing : SettledSmoothing);
        _speechBlocks++;

        speechGainDb = Math.Clamp(TargetDbfs - _levelDb, MinGainDb, MaxGainDb);
        _speechGainDbSum += speechGainDb;
        _targetGain = Math.Pow(10d, speechGainDb / 20d);
    }
}
