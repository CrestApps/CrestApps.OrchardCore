using CrestApps.OrchardCore.Omnichannel.Voice.Models;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Measures one automated call from its audio: when the assistant's speech played to the caller, when the caller's
/// voice was detected, and how much of the call neither side filled.
/// </summary>
/// <remarks>
/// <para>
/// Times are UTC ticks from whichever clock the engine reads its audio on, and every one of a meter's readings has
/// to come from the same clock. The live session reads the machine clock for its audio pumps, so it measures on
/// that; the turn-based loop only learns of speech through provider events, so it measures on the times those
/// events say they happened.
/// </para>
/// <para>
/// Talk time is a union of intervals rather than a sum of lengths. The live session hands speech to the line faster
/// than it plays, in pieces that are scheduled back to back and occasionally over each other; and a caller who talks
/// over the assistant is not two seconds of conversation for every second of overlap, nor is that second silent.
/// </para>
/// <para>
/// Every member is safe to call from the session's concurrent pumps.
/// </para>
/// </remarks>
internal sealed class AIVoiceSessionMeter
{
    private readonly Lock _lock = new();
    private readonly bool _measuresCallerSpeech;
    private readonly List<(long Start, long End)> _assistant = [];
    private readonly List<(long Start, long End)> _caller = [];

    private long? _startTicks;
    private long? _stopTicks;
    private long? _assistantOpenedTicks;
    private long? _callerOpenedTicks;
    private int _unpairedAssistantEnds;
    private int _bargeIns;
    private int _idlePrompts;
    private long? _echoHeldMilliseconds;
    private bool _failed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIVoiceSessionMeter"/> class.
    /// </summary>
    /// <param name="measuresCallerSpeech">Whether the engine reports when the caller starts and stops speaking. When
    /// it does not, caller speaking time and the silence between the two sides are reported as unknown.</param>
    public AIVoiceSessionMeter(bool measuresCallerSpeech)
    {
        _measuresCallerSpeech = measuresCallerSpeech;
    }

    /// <summary>
    /// Gets a value indicating whether the session reported a failure.
    /// </summary>
    public bool HasFailed
    {
        get
        {
            lock (_lock)
            {
                return _failed;
            }
        }
    }

    /// <summary>
    /// The assistant took the call. Only the first report counts.
    /// </summary>
    /// <param name="ticks">When, in UTC ticks.</param>
    public void Start(long ticks)
    {
        lock (_lock)
        {
            _startTicks ??= ticks;
        }
    }

    /// <summary>
    /// The assistant's part of the call ended. Only the first report counts.
    /// </summary>
    /// <param name="ticks">When, in UTC ticks.</param>
    public void Stop(long ticks)
    {
        lock (_lock)
        {
            if (_startTicks.HasValue)
            {
                _stopTicks ??= ticks;
            }
        }
    }

    /// <summary>
    /// Speech was written to the line and will play from <paramref name="startsTicks"/> for
    /// <paramref name="durationTicks"/>.
    /// </summary>
    /// <param name="startsTicks">When it starts playing, in UTC ticks.</param>
    /// <param name="durationTicks">How long it plays, in ticks.</param>
    public void AssistantAudioScheduled(long startsTicks, long durationTicks)
    {
        if (durationTicks <= 0)
        {
            return;
        }

        lock (_lock)
        {
            _assistant.Add((startsTicks, startsTicks + durationTicks));
        }
    }

    /// <summary>
    /// The provider reported the assistant's speech starting to play.
    /// </summary>
    /// <param name="ticks">When, in UTC ticks.</param>
    public void AssistantSpeechStarted(long ticks)
    {
        lock (_lock)
        {
            _assistantOpenedTicks ??= ticks;
        }
    }

    /// <summary>
    /// The provider reported the assistant's speech finishing.
    /// </summary>
    /// <param name="ticks">When, in UTC ticks.</param>
    public void AssistantSpeechEnded(long ticks)
    {
        lock (_lock)
        {
            if (_assistantOpenedTicks is not { } opened)
            {
                // Played, but not measurable: the start was never seen.
                _unpairedAssistantEnds++;

                return;
            }

            _assistant.Add((opened, Math.Max(opened, ticks)));
            _assistantOpenedTicks = null;
        }
    }

    /// <summary>
    /// The caller talked over the assistant and its queued speech was taken off the line: nothing scheduled after
    /// <paramref name="ticks"/> was heard.
    /// </summary>
    /// <param name="ticks">When the line was cleared, in UTC ticks.</param>
    public void AssistantInterrupted(long ticks)
    {
        lock (_lock)
        {
            _bargeIns++;

            for (var i = _assistant.Count - 1; i >= 0; i--)
            {
                var (start, end) = _assistant[i];

                if (start >= ticks)
                {
                    _assistant.RemoveAt(i);
                }
                else if (end > ticks)
                {
                    _assistant[i] = (start, ticks);
                }
            }
        }
    }

    /// <summary>
    /// The caller's voice was detected.
    /// </summary>
    /// <param name="ticks">When, in UTC ticks.</param>
    public void CallerSpeechStarted(long ticks)
    {
        lock (_lock)
        {
            _callerOpenedTicks ??= ticks;
        }
    }

    /// <summary>
    /// The caller's turn was over.
    /// </summary>
    /// <param name="ticks">When, in UTC ticks.</param>
    public void CallerSpeechStopped(long ticks)
    {
        lock (_lock)
        {
            if (_callerOpenedTicks is not { } opened)
            {
                return;
            }

            _caller.Add((opened, Math.Max(opened, ticks)));
            _callerOpenedTicks = null;
        }
    }

    /// <summary>
    /// The assistant spoke up because nobody had said anything for a while.
    /// </summary>
    public void IdlePrompt()
    {
        lock (_lock)
        {
            _idlePrompts++;
        }
    }

    /// <summary>
    /// Caller audio heard while the assistant was speaking was held back as its echo.
    /// </summary>
    /// <param name="milliseconds">How much.</param>
    public void EchoHeld(long milliseconds)
    {
        lock (_lock)
        {
            _echoHeldMilliseconds = (_echoHeldMilliseconds ?? 0) + Math.Max(0, milliseconds);
        }
    }

    /// <summary>
    /// The session reported an error.
    /// </summary>
    public void Failed()
    {
        lock (_lock)
        {
            _failed = true;
        }
    }

    /// <summary>
    /// What was measured, or <see langword="null"/> when the assistant never took the call.
    /// </summary>
    /// <param name="endTicks">Where to measure to when the meter has not been stopped; the machine clock's now
    /// when omitted.</param>
    public AIVoiceSessionMeasurements Measure(long? endTicks = null)
    {
        lock (_lock)
        {
            if (_startTicks is not { } start)
            {
                return null;
            }

            var end = Math.Max(start, _stopTicks ?? endTicks ?? DateTime.UtcNow.Ticks);

            var assistant = Clip(Closed(_assistant, _assistantOpenedTicks, end), start, end);
            var caller = Clip(Closed(_caller, _callerOpenedTicks, end), start, end);

            long? assistantTicks = assistant.Count == 0 && _unpairedAssistantEnds > 0
                ? null
                : Length(assistant);
            long? callerTicks = _measuresCallerSpeech ? Length(caller) : null;
            long? silenceTicks = assistantTicks.HasValue && callerTicks.HasValue
                ? (end - start) - Length(Union([.. assistant, .. caller]))
                : null;

            return new AIVoiceSessionMeasurements
            {
                StartedUtc = new DateTime(start, DateTimeKind.Utc),
                EndedUtc = new DateTime(end, DateTimeKind.Utc),
                SessionDurationMs = ToMilliseconds(end - start),
                AssistantSpeakingMs = ToMilliseconds(assistantTicks),
                CallerSpeakingMs = ToMilliseconds(callerTicks),
                MutualSilenceMs = ToMilliseconds(silenceTicks),
                TimeToFirstAssistantAudioMs = assistant.Count == 0 ? null : ToMilliseconds(assistant.Min(interval => interval.Start) - start),
                BargeIns = _bargeIns,
                IdlePrompts = _idlePrompts,
                EchoHeldMs = _echoHeldMilliseconds,
            };
        }
    }

    // Speech still going when the call ended counts up to the end.
    private static List<(long Start, long End)> Closed(List<(long Start, long End)> intervals, long? openedTicks, long end)
        => openedTicks is { } opened ? [.. intervals, (opened, end)] : [.. intervals];

    private static List<(long Start, long End)> Clip(List<(long Start, long End)> intervals, long start, long end)
        => intervals
            .Select(interval => (Start: Math.Max(interval.Start, start), End: Math.Min(interval.End, end)))
            .Where(interval => interval.End > interval.Start)
            .ToList();

    private static List<(long Start, long End)> Union(List<(long Start, long End)> intervals)
    {
        var merged = new List<(long Start, long End)>();

        foreach (var interval in intervals.OrderBy(interval => interval.Start))
        {
            if (merged.Count > 0 && interval.Start <= merged[^1].End)
            {
                merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, interval.End));
            }
            else
            {
                merged.Add(interval);
            }
        }

        return merged;
    }

    private static long Length(List<(long Start, long End)> intervals)
        => Union(intervals).Sum(interval => interval.End - interval.Start);

    private static long ToMilliseconds(long ticks)
        => ticks / TimeSpan.TicksPerMillisecond;

    private static long? ToMilliseconds(long? ticks)
        => ticks.HasValue ? ToMilliseconds(ticks.Value) : null;
}
