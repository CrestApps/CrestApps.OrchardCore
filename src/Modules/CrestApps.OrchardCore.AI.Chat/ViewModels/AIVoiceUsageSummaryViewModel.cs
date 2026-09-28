namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// The voice usage of a group of automated calls, or of all of them.
/// </summary>
/// <remarks>
/// Averages and rates are over the calls that measured them: a turn-based call cannot hear the caller start and
/// stop, and cannot be talked over, so it counts toward the call total but not toward the caller or barge-in figures.
/// </remarks>
public sealed class AIVoiceUsageSummaryViewModel
{
    /// <summary>
    /// Gets or sets what the row is for; empty for the totals.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the number of calls.
    /// </summary>
    public int Calls { get; set; }

    /// <summary>
    /// Gets or sets the minutes the assistant held calls.
    /// </summary>
    public double SessionMinutes { get; set; }

    /// <summary>
    /// Gets or sets the minutes callers heard the assistant speak.
    /// </summary>
    public double AIMinutes { get; set; }

    /// <summary>
    /// Gets or sets the minutes callers spoke, on calls that measured it; <see langword="null"/> when none did.
    /// </summary>
    public double? CallerMinutes { get; set; }

    /// <summary>
    /// Gets or sets the minutes neither side spoke, on calls that measured it; <see langword="null"/> when none did.
    /// </summary>
    public double? SilenceMinutes { get; set; }

    /// <summary>
    /// Gets or sets the average length of a measured call, in seconds.
    /// </summary>
    public double? AverageSessionSeconds { get; set; }

    /// <summary>
    /// Gets or sets the average wait for the assistant's first word after the answer, in milliseconds.
    /// </summary>
    public double? AverageTimeToFirstAudioMs { get; set; }

    /// <summary>
    /// Gets or sets the share of calls handed to a live agent, from 0 to 1.
    /// </summary>
    public double HandoffRate { get; set; }

    /// <summary>
    /// Gets or sets the average number of barge-ins per call that could be talked over.
    /// </summary>
    public double? BargeInsPerCall { get; set; }

    /// <summary>
    /// Gets or sets how many times the assistant spoke up because the line had gone quiet.
    /// </summary>
    public int IdlePrompts { get; set; }

    /// <summary>
    /// Gets or sets the text completion tokens recorded against the calls' chat sessions.
    /// </summary>
    public long TextTokens { get; set; }

    /// <summary>
    /// Gets or sets the audio tokens the provider reported, when it reported any.
    /// </summary>
    public long? AudioTokens { get; set; }
}
