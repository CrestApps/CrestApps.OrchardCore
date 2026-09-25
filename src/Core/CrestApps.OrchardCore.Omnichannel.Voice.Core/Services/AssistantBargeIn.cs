namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Keeps account of the assistant's speech that is queued on the line, so that when the caller talks over it the
/// rest can be taken back and the model told how much of it was actually heard.
/// </summary>
/// <remarks>
/// <para>
/// The model produces its voice faster than it plays — a seven-second line can arrive in under three — and the
/// carrier queues it and plays it in order. When a caller talks over the assistant the provider stops the model,
/// but stopping the model does not reach what is already queued: live, a caller talked over the assistant three
/// times on one call and heard it finish its sentence every time, which to them was an assistant that does not
/// listen. Taking it back needs three things this keeps: where on the line each piece of speech plays, which of
/// the model's items it belongs to and how far into that item it is, and which responses were interrupted so a
/// late piece of one is not played after the line was cleared.
/// </para>
/// <para>
/// It also carries the one fact the caller's side knows and the model's side needs: that the echo guard has just
/// let a voice through. The provider's report that the caller started talking is not enough on its own, because
/// its detector also fires on the assistant's own echo — and cutting the assistant off on its own echo is worse
/// than not stopping it at all.
/// </para>
/// <para>
/// Everything except <see cref="CallerTalkingOver(long)"/> is called only from the pump that plays the
/// assistant's speech, so only that one value is shared between threads.
/// </para>
/// </remarks>
internal sealed class AssistantBargeIn
{
    /// <summary>
    /// How long after the echo guard last let the caller through a speech start from the provider still counts as
    /// that caller talking over the assistant.
    /// </summary>
    /// <remarks>
    /// The provider reports speech some way behind the audio that caused it: its detector needs a stretch of speech
    /// before it decides, and the report then crosses the network. Live, the report landed 120 to 330 ms after the
    /// guard opened. The guard itself stays open through the pauses between words, so this only has to cover that
    /// delay for a caller who said one short word and stopped.
    /// </remarks>
    internal const int CallerTalkingOverWindowMilliseconds = 1_500;

    /// <summary>
    /// How many interrupted responses and items are remembered, so a very long call does not grow without bound.
    /// Late audio only ever belongs to the last one or two.
    /// </summary>
    private const int MaximumRemembered = 16;

    private readonly List<QueuedSpeech> _queued = [];
    private readonly HashSet<string> _interruptedResponses = new(StringComparer.Ordinal);
    private readonly HashSet<string> _interruptedItems = new(StringComparer.Ordinal);
    private readonly List<AssistantAudioTruncation> _sent = [];

    private long _callerTalkingOverTicks;
    private bool _holdingUnnamedSpeech;
    private string _currentItemId;
    private long _currentItemQueuedTicks;

    /// <summary>
    /// Records that the echo guard let the caller through while the assistant was speaking.
    /// </summary>
    /// <param name="nowTicks">When, in UTC ticks.</param>
    public void CallerTalkingOver(long nowTicks)
        => Interlocked.Exchange(ref _callerTalkingOverTicks, nowTicks);

    /// <summary>
    /// Whether the echo guard has let the caller through recently enough that a speech start reported now is them.
    /// </summary>
    /// <param name="nowTicks">Now, in UTC ticks.</param>
    public bool IsCallerTalkingOver(long nowTicks)
    {
        var last = Interlocked.Read(ref _callerTalkingOverTicks);

        return last > 0 && nowTicks - last <= CallerTalkingOverWindowMilliseconds * TimeSpan.TicksPerMillisecond;
    }

    /// <summary>
    /// Whether a piece of the assistant's speech should be played, or belongs to a line the caller talked over.
    /// </summary>
    /// <param name="responseId">The provider's response the speech belongs to, when it names one.</param>
    /// <param name="itemId">The provider's item the speech belongs to, when it names one.</param>
    public bool ShouldPlay(string responseId, string itemId)
    {
        if (!string.IsNullOrEmpty(responseId) && _interruptedResponses.Contains(responseId))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(itemId) && _interruptedItems.Contains(itemId))
        {
            return false;
        }

        // A provider that names neither leaves nothing to tell the interrupted line from the next one, except that
        // the next one starts a new turn.
        return !(_holdingUnnamedSpeech && string.IsNullOrEmpty(responseId) && string.IsNullOrEmpty(itemId));
    }

    /// <summary>
    /// Records a piece of the assistant's speech that was written to the line.
    /// </summary>
    /// <param name="responseId">The provider's response it belongs to, when it names one.</param>
    /// <param name="itemId">The provider's item it belongs to, when it names one.</param>
    /// <param name="startsTicks">When it starts playing to the caller, in UTC ticks.</param>
    /// <param name="pcmByteCount">Its length, in bytes of 16-bit PCM at the realtime rate.</param>
    /// <param name="nowTicks">Now, in UTC ticks.</param>
    public void Queued(string responseId, string itemId, long startsTicks, int pcmByteCount, long nowTicks)
    {
        // What has finished playing can no longer be taken back.
        _queued.RemoveAll(speech => speech.EndsTicks <= nowTicks);

        if (!string.Equals(itemId, _currentItemId, StringComparison.Ordinal))
        {
            _currentItemId = itemId;
            _currentItemQueuedTicks = 0;
        }

        var duration = DurationTicks(pcmByteCount);

        _queued.Add(new QueuedSpeech(responseId, itemId, _currentItemQueuedTicks, startsTicks, startsTicks + duration));
        _currentItemQueuedTicks += duration;
    }

    /// <summary>
    /// Notes that the model has moved on to a new turn, after which speech that names nothing is the new line.
    /// </summary>
    public void NextTurn()
        => _holdingUnnamedSpeech = false;

    /// <summary>
    /// Takes back everything still queued on the line, and returns how much of each item it belonged to the caller
    /// actually heard.
    /// </summary>
    /// <remarks>
    /// Measured along each item's own audio rather than the clock, because that is the timeline the model's context
    /// is kept in: a line delivered in two pieces with a pause between them was heard for less than the time since
    /// it started. An item still waiting behind the one being played is cut before its first word.
    /// </remarks>
    /// <param name="nowTicks">When the caller talked over the assistant, in UTC ticks.</param>
    public IReadOnlyList<AssistantAudioTruncation> Interrupt(long nowTicks)
    {
        var truncations = new List<AssistantAudioTruncation>();

        foreach (var speech in _queued)
        {
            if (speech.EndsTicks <= nowTicks)
            {
                continue;
            }

            Remember(_interruptedResponses, speech.ResponseId);
            Remember(_interruptedItems, speech.ItemId);

            // Earlier pieces come first, so the first unfinished piece of an item is where the caller stopped
            // hearing it.
            if (string.IsNullOrEmpty(speech.ItemId) || truncations.Exists(truncation => truncation.ItemId == speech.ItemId))
            {
                continue;
            }

            // Never past the end of what was delivered for the item. The provider refuses a cut beyond the audio
            // it holds for an item, and a refusal used to end the call.
            var delivered = DeliveredTicks(speech.ItemId);
            var heard = Math.Min(delivered, speech.ItemOffsetTicks + Math.Max(0, nowTicks - speech.StartsTicks));

            truncations.Add(new AssistantAudioTruncation(
                speech.ItemId,
                (int)(heard / TimeSpan.TicksPerMillisecond),
                (int)(delivered / TimeSpan.TicksPerMillisecond)));
        }

        _queued.Clear();
        _holdingUnnamedSpeech = true;

        _sent.Clear();
        _sent.AddRange(truncations);

        return truncations;
    }

    /// <summary>
    /// Takes back the cut that asked for <paramref name="requestedMilliseconds"/>, once the provider has refused it.
    /// </summary>
    /// <remarks>
    /// The provider's refusal names only the two lengths, not the item, so the item is found by what was asked for.
    /// Each cut is handed back at most once: a corrected cut that is refused again is not retried.
    /// </remarks>
    /// <param name="requestedMilliseconds">The cut the provider refused, as it quoted it.</param>
    /// <param name="truncation">The cut that was refused.</param>
    public bool TryTakeRefused(int requestedMilliseconds, out AssistantAudioTruncation truncation)
    {
        var index = _sent.FindIndex(sent => sent.AudioEndMilliseconds == requestedMilliseconds);

        if (index < 0)
        {
            truncation = default;

            return false;
        }

        truncation = _sent[index];
        _sent.RemoveAt(index);

        return true;
    }

    // How much of an item's audio has been delivered: the end of its furthest piece still on the line, or, once it
    // has all played, everything recorded for it.
    private long DeliveredTicks(string itemId)
    {
        var delivered = string.Equals(itemId, _currentItemId, StringComparison.Ordinal) ? _currentItemQueuedTicks : 0;

        foreach (var speech in _queued)
        {
            if (string.Equals(speech.ItemId, itemId, StringComparison.Ordinal))
            {
                delivered = Math.Max(delivered, speech.ItemOffsetTicks + (speech.EndsTicks - speech.StartsTicks));
            }
        }

        return delivered;
    }

    /// <summary>
    /// How long a piece of 16-bit PCM at the realtime rate plays for, in ticks.
    /// </summary>
    /// <param name="pcmByteCount">Its length in bytes.</param>
    internal static long DurationTicks(int pcmByteCount)
        => pcmByteCount * TimeSpan.TicksPerSecond / (RealtimeAudioConverter.RealtimeSampleRate * 2L);

    private static void Remember(HashSet<string> interrupted, string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        if (interrupted.Count >= MaximumRemembered)
        {
            interrupted.Clear();
        }

        interrupted.Add(id);
    }

    private readonly record struct QueuedSpeech(
        string ResponseId,
        string ItemId,
        long ItemOffsetTicks,
        long StartsTicks,
        long EndsTicks);
}
