namespace CrestApps.OrchardCore.Omnichannel.Voice.Models;

/// <summary>
/// A listening turn on a turn-based automated call, as it stood when listening began.
/// </summary>
public sealed class TurnBasedSilence
{
    /// <summary>
    /// Gets or sets the activity the call belongs to.
    /// </summary>
    public string ActivityId { get; set; }

    /// <summary>
    /// Gets or sets the provider carrying the call.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider's identifier for the call.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets how many turns the transcript held when listening began.
    /// </summary>
    /// <remarks>
    /// Anything said since -- by the caller, or by the assistant in reply -- adds a turn, so an unchanged count
    /// when the watch fires means the line has been silent the whole time.
    /// </remarks>
    public int PromptCount { get; set; }

    /// <summary>
    /// Gets or sets how long the line may stay quiet before the watch fires, when not the default.
    /// </summary>
    /// <remarks>
    /// A voicemail greeting that has finished is followed by the tone and then a recording of whatever comes next,
    /// so the pause worth waiting for there is a few seconds rather than the time a person is given to answer.
    /// </remarks>
    public TimeSpan? Wait { get; set; }
}
