namespace CrestApps.OrchardCore.AI.Chat.Models;

/// <summary>
/// What the voice table on the AI usage report is grouped by.
/// </summary>
public enum AIVoiceUsageGroupBy
{
    /// <summary>
    /// The deployment, and the model behind it, that held the call.
    /// </summary>
    Deployment = 0,

    /// <summary>
    /// The AI profile that held the call.
    /// </summary>
    Profile = 1,

    /// <summary>
    /// The campaign the call was placed for.
    /// </summary>
    Campaign = 2,

    /// <summary>
    /// The channel the call was carried on.
    /// </summary>
    Channel = 3,

    /// <summary>
    /// The conversation engine: live speech-to-speech or turn-based.
    /// </summary>
    Engine = 4,

    /// <summary>
    /// The local day the call was taken.
    /// </summary>
    Day = 5,
}
