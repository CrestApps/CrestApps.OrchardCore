namespace CrestApps.OrchardCore.AI.Chat.Interactions.Models;

/// <summary>
/// A snapshot of the instructions and model parameters, taken each time they change during a session.
/// </summary>
public sealed class ProfileBuilderRevision
{
    /// <summary>
    /// Gets or sets the revision number, starting at 1.
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// Gets or sets what produced the revision.
    /// </summary>
    public ProfileBuilderRevisionSource Source { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the feedback that produced the revision, when it came from feedback.
    /// </summary>
    public string FeedbackId { get; set; }

    /// <summary>
    /// Gets or sets a short description of what changed and why.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Gets or sets the instruction values of this revision.
    /// </summary>
    public ProfileBuilderSettings Settings { get; set; } = new();

    /// <summary>
    /// Gets or sets when the revision was made.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// What produced a revision.
/// </summary>
public enum ProfileBuilderRevisionSource
{
    /// <summary>
    /// The settings the session started with.
    /// </summary>
    Initial,

    /// <summary>
    /// The coach rewrote the settings from feedback.
    /// </summary>
    Feedback,

    /// <summary>
    /// The author changed the settings in the settings panel.
    /// </summary>
    Manual,

    /// <summary>
    /// The author restored an earlier revision.
    /// </summary>
    Restore,

    /// <summary>
    /// Rewritten by the coach after simulated conversations failed.
    /// </summary>
    Simulation,
}

/// <summary>
/// The values the builder tunes: the instructions and the sampling parameters.
/// </summary>
public sealed class ProfileBuilderSettings
{
    /// <summary>
    /// Gets or sets the system message.
    /// </summary>
    public string SystemMessage { get; set; }

    /// <summary>
    /// Gets or sets the instruction the assistant uses to open a conversation it starts itself.
    /// </summary>
    public string InitialPrompt { get; set; }

    /// <summary>
    /// Gets or sets the temperature.
    /// </summary>
    public float? Temperature { get; set; }

    /// <summary>
    /// Gets or sets the top-p value.
    /// </summary>
    public float? TopP { get; set; }

    /// <summary>
    /// Gets or sets the frequency penalty.
    /// </summary>
    public float? FrequencyPenalty { get; set; }

    /// <summary>
    /// Gets or sets the presence penalty.
    /// </summary>
    public float? PresencePenalty { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of output tokens.
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Gets or sets how many past messages are sent with each request.
    /// </summary>
    public int? PastMessagesCount { get; set; }

    /// <summary>
    /// Returns a value indicating whether these settings hold the same values as <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The settings to compare with.</param>
    public bool HasSameValues(ProfileBuilderSettings other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(Normalize(SystemMessage), Normalize(other.SystemMessage), StringComparison.Ordinal) &&
            string.Equals(Normalize(InitialPrompt), Normalize(other.InitialPrompt), StringComparison.Ordinal) &&
            Temperature == other.Temperature &&
            TopP == other.TopP &&
            FrequencyPenalty == other.FrequencyPenalty &&
            PresencePenalty == other.PresencePenalty &&
            MaxTokens == other.MaxTokens &&
            PastMessagesCount == other.PastMessagesCount;
    }

    /// <summary>
    /// Returns a copy of these settings.
    /// </summary>
    public ProfileBuilderSettings Clone()
        => (ProfileBuilderSettings)MemberwiseClone();

    private static string Normalize(string value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace("\r\n", "\n").Trim();
}
