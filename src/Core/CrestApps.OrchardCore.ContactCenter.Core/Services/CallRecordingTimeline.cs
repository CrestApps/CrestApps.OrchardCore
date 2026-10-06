using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// One line of a conversation as it was stored: who said it, what they said, and when they began.
/// </summary>
/// <param name="Speaker">Who said it.</param>
/// <param name="Text">What was said.</param>
/// <param name="SaidUtc">When the line began, in UTC; <see langword="default"/> when it was stored without a time.</param>
public readonly record struct CallRecordingTranscriptLine(CallRecordingTranscriptSpeaker Speaker, string Text, DateTime SaidUtc);

/// <summary>
/// Places a conversation's lines on a recording's timeline and measures the silence between them.
/// </summary>
/// <remarks>
/// A line's offset is the time it began less the time the recording started. A transcript keeps only one time per
/// line, so where a line ended is estimated from its length (<see cref="SpokenLineTiming"/>). The silence before a line
/// is the time from the estimated end of the line before -- or from the start of the recording, for the first line --
/// to the line's start, never negative: a line that begins before the previous one is estimated to end overlaps it,
/// which is a person talking over the other, not a silence. The total silence is the sum of those gaps; the quiet
/// after the last line, before the call is hung up, is not counted.
/// </remarks>
public static class CallRecordingTimeline
{
    // A line that began this close before the recording did is the recording catching its first syllable late.
    private const double StartToleranceSeconds = 1;

    // A line stamped this far past the recording's end was said after it stopped.
    private const double EndToleranceSeconds = 2;

    /// <summary>
    /// Builds the transcript of a recording from the conversation's lines.
    /// </summary>
    /// <param name="recording">The recording.</param>
    /// <param name="lines">The lines, in any order.</param>
    /// <returns>The transcript, with its lines in the order they were said.</returns>
    public static CallRecordingTranscript Build(CallRecording recording, IEnumerable<CallRecordingTranscriptLine> lines)
    {
        ArgumentNullException.ThrowIfNull(recording);

        var duration = recording.DurationSeconds > 0 ? recording.DurationSeconds : (double?)null;
        var phrases = new List<CallRecordingTranscriptPhrase>();
        var previousEnd = 0d;
        var totalSilence = 0d;

        // Stable, so lines stored at the same instant keep the order they were stored in.
        foreach (var line in (lines ?? []).Where(line => !string.IsNullOrWhiteSpace(line.Text)).OrderBy(line => line.SaidUtc))
        {
            var text = line.Text.Trim();
            var phrase = new CallRecordingTranscriptPhrase
            {
                Speaker = line.Speaker,
                Text = text,
            };

            phrases.Add(phrase);

            if (line.SaidUtc == default || recording.StartedUtc == default)
            {
                continue;
            }

            var offset = (line.SaidUtc - recording.StartedUtc).TotalSeconds;

            if (offset < -StartToleranceSeconds || (duration is { } length && offset > length + EndToleranceSeconds))
            {
                continue;
            }

            offset = Math.Max(0, offset);

            if (duration is { } recordingLength)
            {
                offset = Math.Min(offset, recordingLength);
            }

            var silence = Math.Max(0, offset - previousEnd);
            var end = offset + SpokenLineTiming.EstimateDuration(text).TotalSeconds;

            phrase.OffsetSeconds = Math.Round(offset, 1);
            phrase.SilenceBeforeSeconds = Math.Round(silence, 1);
            totalSilence += phrase.SilenceBeforeSeconds.Value;
            previousEnd = Math.Max(previousEnd, duration is { } cap ? Math.Min(end, cap) : end);
        }

        return new CallRecordingTranscript
        {
            Phrases = phrases,
            TotalSilenceSeconds = Math.Round(totalSilence, 1),
        };
    }
}
