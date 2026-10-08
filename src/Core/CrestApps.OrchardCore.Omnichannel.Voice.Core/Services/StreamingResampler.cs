namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Changes the sample rate of a continuous stream that arrives in pieces, so the result is the same however the
/// stream was cut up.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="RealtimeAudioConverter.Resample"/> treats every buffer as a whole signal: its anti-aliasing filter
/// starts from silence and its interpolation is stretched to land on the buffer's first and last samples. That is
/// right for a recording and wrong for a call, which is converted one model delta or one 20 ms packet at a time.
/// Every boundary then carries a small step — the filter's rise from nothing, and a nudge of the sample grid —
/// which on the phone is a faint click, fifty times a second on the caller's side and once per delta on the
/// assistant's. Callers described it as a scratch.
/// </para>
/// <para>
/// Here the filter keeps its state and the interpolation keeps its place across buffers. The position is exact
/// rational arithmetic, so the output of a long call never drifts, and each input sample yields exactly its share
/// of output: one 20 ms packet at 8 kHz is always 20 ms at 24 kHz.
/// </para>
/// </remarks>
internal sealed class StreamingResampler
{
    private readonly int _fromRate;
    private readonly int _toRate;
    private readonly long _step;
    private readonly long _unit;
    private readonly Biquad[] _before;
    private readonly AntiAliasFilter _antiAlias;
    private readonly Biquad[] _after;

    private bool _started;
    private double _previous;
    private long _position;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamingResampler"/> class.
    /// </summary>
    /// <param name="fromRate">The rate the stream arrives at.</param>
    /// <param name="toRate">The rate it is wanted at.</param>
    public StreamingResampler(int fromRate, int toRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fromRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(toRate);

        _fromRate = fromRate;
        _toRate = toRate;

        // One output sample moves the position on by fromRate/toRate input samples, kept as a fraction.
        var divisor = GreatestCommonDivisor(fromRate, toRate);
        _step = fromRate / divisor;
        _unit = toRate / divisor;

        // Band-limited on the way down before samples are dropped, on the way up after they are made, and to the
        // telephone passband either way. Four poles, as the whole-buffer converter uses, give the voice the tone it
        // has always had on the line. Going down, a sharp filter follows them: four poles alone leave what is above
        // the new rate's Nyquist to fold back into the line as harshness on every "s".
        _before = toRate < fromRate ? Biquad.LowPass(fromRate, Cutoff(fromRate, toRate)) : [];
        _antiAlias = toRate < fromRate ? AntiAliasFilter.For(fromRate, toRate) : null;
        _after = toRate > fromRate ? Biquad.LowPass(toRate, Cutoff(toRate, fromRate)) : [];
    }

    /// <summary>
    /// Converts the next piece of the stream.
    /// </summary>
    /// <param name="samples">The next samples, at the incoming rate.</param>
    /// <param name="output">Receives the converted samples, at the outgoing rate.</param>
    public void Process(ReadOnlySpan<short> samples, List<double> output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (samples.IsEmpty)
        {
            return;
        }

        if (_fromRate == _toRate)
        {
            foreach (var sample in samples)
            {
                output.Add(sample);
            }

            return;
        }

        var input = new double[samples.Length];

        for (var i = 0; i < samples.Length; i++)
        {
            var value = Filter(_before, samples[i]);
            input[i] = _antiAlias?.Next(value) ?? value;
        }

        if (!_started)
        {
            // Primed with the stream's first sample, so every input sample from here yields its full share.
            _started = true;
            _previous = input[0];
            _position = _step;
        }

        // Index 0 is the last sample of the previous piece; index i is input[i - 1].
        var last = (long)input.Length * _unit;

        while (_position <= last)
        {
            var index = (int)(_position / _unit);
            var fraction = _position % _unit;
            var a = index == 0 ? _previous : input[index - 1];
            var value = fraction == 0 ? a : a + ((input[index] - a) * fraction / _unit);

            output.Add(Filter(_after, value));
            _position += _step;
        }

        _position -= last;
        _previous = input[^1];
    }

    /// <summary>
    /// Forgets the stream so far, so the next piece starts a new one.
    /// </summary>
    public void Reset()
    {
        _started = false;
        _previous = 0;
        _position = 0;

        foreach (var section in _before)
        {
            section.Reset();
        }

        _antiAlias?.Reset();

        foreach (var section in _after)
        {
            section.Reset();
        }
    }

    private static double Filter(Biquad[] sections, double value)
    {
        foreach (var section in sections)
        {
            value = section.Next(value);
        }

        return value;
    }

    // Nyquist of the narrower side, with margin, but never above the telephone passband.
    private static double Cutoff(int sampleRate, int otherRate)
        => Math.Min(3_400d, Math.Min(sampleRate, otherRate) * 0.45d);

    private static int GreatestCommonDivisor(int a, int b)
    {
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }

        return a;
    }

    /// <summary>
    /// One second-order Butterworth low-pass section that remembers where it was.
    /// </summary>
    /// <summary>
    /// A linear-phase low-pass (a Kaiser-windowed sinc) that keeps what a phone carries and removes what would fold.
    /// </summary>
    /// <remarks>
    /// It used to be two Butterworth sections at 3.4 kHz. Taking 24 kHz to 8 kHz, a 5 kHz component -- the bright
    /// part of an "s" -- was only about 15 dB down when it folded back to 3 kHz. One realtime model's voice is
    /// brighter than another's, and on the line it sounded harsher: its recordings carried 6 to 8 dB more energy at
    /// the top of the band than the other model's, while every band below matched to within a decibel. This passes
    /// the telephone band flat and holds everything that would fold into it at least 70 dB down. It follows the
    /// four-pole filter rather than replacing it, so the voice keeps the tone it had: replaced, the top of the band
    /// came through a few decibels brighter for every model, and the one that already sounded right would have
    /// changed too.
    /// </remarks>
    private sealed class AntiAliasFilter
    {
        // The stopband floor, in dB.
        private const double AttenuationDb = 70d;

        private readonly double[] _taps;
        private readonly double[] _history;
        private int _next;

        private AntiAliasFilter(double[] taps)
        {
            _taps = taps;
            _history = new double[taps.Length];
        }

        /// <summary>
        /// The filter for taking a stream from one rate down to another.
        /// </summary>
        /// <param name="fromRate">The rate it is filtered at.</param>
        /// <param name="toRate">The rate it is going down to.</param>
        public static AntiAliasFilter For(int fromRate, int toRate)
        {
            // Flat to the telephone band, and fully down by the frequency that folds onto its upper edge.
            var passEdge = Math.Min(3_400d, toRate * 0.425d);
            var stopEdge = Math.Min(toRate - passEdge, passEdge + 1_200d);
            var cutoff = (passEdge + stopEdge) / 2d / fromRate;
            var transition = 2d * Math.PI * (stopEdge - passEdge) / fromRate;

            // Kaiser's estimates for the length and the window's shape at that attenuation.
            var length = (int)Math.Ceiling((AttenuationDb - 8d) / (2.285d * transition)) + 1;
            length |= 1;
            var beta = 0.1102d * (AttenuationDb - 8.7d);

            var taps = new double[length];
            var middle = (length - 1) / 2d;
            var sum = 0d;

            for (var n = 0; n < length; n++)
            {
                var t = n - middle;
                var sinc = t == 0 ? 2d * cutoff : Math.Sin(2d * Math.PI * cutoff * t) / (Math.PI * t);
                var ratio = t / middle;
                taps[n] = sinc * BesselI0(beta * Math.Sqrt(1d - (ratio * ratio))) / BesselI0(beta);
                sum += taps[n];
            }

            // Unity gain at DC, so the level of the voice is not changed by its filter.
            for (var n = 0; n < length; n++)
            {
                taps[n] /= sum;
            }

            return new AntiAliasFilter(taps);
        }

        public double Next(double x)
        {
            _history[_next] = x;

            var y = 0d;
            var index = _next;

            for (var k = 0; k < _taps.Length; k++)
            {
                y += _taps[k] * _history[index];
                index = index == 0 ? _history.Length - 1 : index - 1;
            }

            _next = _next == _history.Length - 1 ? 0 : _next + 1;

            return y;
        }

        public void Reset()
        {
            Array.Clear(_history);
            _next = 0;
        }

        // The zeroth-order modified Bessel function of the first kind, by its series: enough terms for any beta used.
        private static double BesselI0(double x)
        {
            var sum = 1d;
            var term = 1d;
            var half = x / 2d;

            for (var k = 1; k < 50; k++)
            {
                term *= half / k;
                var squared = term * term;
                sum += squared;

                if (squared < sum * 1e-16)
                {
                    break;
                }
            }

            return sum;
        }
    }

    private sealed class Biquad
    {
        private readonly double _b0;
        private readonly double _b1;
        private readonly double _b2;
        private readonly double _a1;
        private readonly double _a2;

        private double _x1;
        private double _x2;
        private double _y1;
        private double _y2;

        private Biquad(int sampleRate, double cutoffHz)
        {
            var omega = 2d * Math.PI * cutoffHz / sampleRate;
            var cosOmega = Math.Cos(omega);
            var alpha = Math.Sin(omega) / (2d * 0.70710678d);
            var a0 = 1d + alpha;

            _b0 = (1d - cosOmega) / 2d / a0;
            _b1 = (1d - cosOmega) / a0;
            _b2 = _b0;
            _a1 = -2d * cosOmega / a0;
            _a2 = (1d - alpha) / a0;
        }

        public static Biquad[] LowPass(int sampleRate, double cutoffHz)
            => cutoffHz <= 0 || cutoffHz >= sampleRate / 2d
                ? []
                : [new Biquad(sampleRate, cutoffHz), new Biquad(sampleRate, cutoffHz)];

        public double Next(double x)
        {
            var y = (_b0 * x) + (_b1 * _x1) + (_b2 * _x2) - (_a1 * _y1) - (_a2 * _y2);

            _x2 = _x1;
            _x1 = x;
            _y2 = _y1;
            _y1 = y;

            return y;
        }

        public void Reset()
            => _x1 = _x2 = _y1 = _y2 = 0;
    }
}
