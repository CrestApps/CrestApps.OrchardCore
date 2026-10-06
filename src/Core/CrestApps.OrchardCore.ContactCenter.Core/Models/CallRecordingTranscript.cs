namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Who said a line of a recorded call's transcript.
/// </summary>
public enum CallRecordingTranscriptSpeaker
{
    /// <summary>
    /// An automated voice agent.
    /// </summary>
    Ai = 0,

    /// <summary>
    /// The customer.
    /// </summary>
    Customer = 1,

    /// <summary>
    /// A person answering for the business.
    /// </summary>
    Agent = 2,
}

/// <summary>
/// One line of a recorded call's transcript, placed on the recording's timeline.
/// </summary>
public sealed class CallRecordingTranscriptPhrase
{
    /// <summary>
    /// Gets or sets who said it.
    /// </summary>
    public CallRecordingTranscriptSpeaker Speaker { get; set; }

    /// <summary>
    /// Gets or sets what was said.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets how far into the recording the line began, in seconds. <see langword="null"/> when the line was
    /// said outside the recording, such as before an AI handed the call to an agent whose leg was recorded, or when
    /// the line was stored without a time.
    /// </summary>
    public double? OffsetSeconds { get; set; }

    /// <summary>
    /// Gets or sets the silence between the end of the line before (or the start of the recording) and this line, in
    /// seconds. <see langword="null"/> when the line is not on the recording.
    /// </summary>
    public double? SilenceBeforeSeconds { get; set; }
}

/// <summary>
/// The transcript of a recorded call.
/// </summary>
public sealed class CallRecordingTranscript
{
    /// <summary>
    /// Gets or sets the lines, in the order they were said.
    /// </summary>
    public IReadOnlyList<CallRecordingTranscriptPhrase> Phrases { get; set; } = [];

    /// <summary>
    /// Gets or sets the silence on the recording before its lines, in seconds: the sum of every line's
    /// <see cref="CallRecordingTranscriptPhrase.SilenceBeforeSeconds"/>.
    /// </summary>
    public double TotalSilenceSeconds { get; set; }
}
