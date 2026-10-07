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
/// Only speech moves the estimate. Silence and pauses are left alone rather than lifted, the gain changes smoothly
/// rather than per word, it is capped so a nearly silent stretch is never pushed up into hiss, and any sample that
/// would clip turns the gain down at once.
/// </para>
/// </remarks>
internal sealed class AssistantVoiceLeveler
{
    /// <summary>
    /// The speaking level the voice is brought to, in dBFS: where the model that callers heard clearly spoke, so
    /// that model is left as it was and only a quieter one is lifted.
    /// </summary>
    internal const double TargetDbfs = -22d;

    /// <summary>
    /// The most the voice is ever raised, in dB.
    /// </summary>
    internal const double MaxGainDb = 18d;

    /// <summary>
    /// The most the voice is ever lowered, in dB. A loud model is turned down only gently; it was never the problem.
    /// </summary>
    internal const double MinGainDb = -6d;

    // Blocks quieter than this are pauses, breaths and silence, and do not move the estimate.
    private const double SpeechGateDbfs = -50d;

    // The peak any sample may reach after gain, as a fraction of full scale.
    private const double PeakCeiling = 0.85d;

    // How quickly the estimate follows speech, per block: quick enough to settle within the first sentence.
    private const double LevelSmoothing = 0.08d;

    private const double FullScale = 32768d;

    private readonly int _blockSamples;
    private readonly double _rampStep;
    private double _levelDb = TargetDbfs;
    private double _gain = 1d;
    private double _targetGain = 1d;
    private int _blockPosition;
    private double _blockSumOfSquares;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssistantVoiceLeveler"/> class.
    /// </summary>
    /// <param name="sampleRate">The sample rate of the audio it levels.</param>
    public AssistantVoiceLeveler(int sampleRate)
    {
        // 10 ms blocks: long enough to measure a level, short enough to follow it.
        _blockSamples = Math.Max(1, (sampleRate > 0 ? sampleRate : 8_000) / 100);

        // A rise in gain is spread over a block, so it is never heard as a step.
        _rampStep = 1d / _blockSamples;
    }

    /// <summary>
    /// Gets the gain currently applied, in dB.
    /// </summary>
    internal double CurrentGainDb => 20d * Math.Log10(_gain);

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

            // Rises are eased in; the ceiling below is enforced at once.
            if (_gain < _targetGain)
            {
                _gain += (_targetGain - _gain) * _rampStep;
            }
            else if (_gain > _targetGain)
            {
                _gain = _targetGain;
            }

            // A sample that would clip turns the gain down for it and what follows, until the next block decides.
            var magnitude = Math.Abs(value);

            if (magnitude > 0 && magnitude * _gain > PeakCeiling * FullScale)
            {
                _gain = PeakCeiling * FullScale / magnitude;
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

        if (blockDb > SpeechGateDbfs)
        {
            _levelDb += (blockDb - _levelDb) * LevelSmoothing;
        }

        _targetGain = Math.Pow(10d, Math.Clamp(TargetDbfs - _levelDb, MinGainDb, MaxGainDb) / 20d);
        _blockPosition = 0;
        _blockSumOfSquares = 0d;
    }
}
