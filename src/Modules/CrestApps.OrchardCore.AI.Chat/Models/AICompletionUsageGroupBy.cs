namespace CrestApps.OrchardCore.AI.Chat.Models;

/// <summary>
/// What the completion usage table on the AI usage report is grouped by.
/// </summary>
public enum AICompletionUsageGroupBy
{
    /// <summary>
    /// The user, the client and the model together: the report's original breakdown.
    /// </summary>
    UserAndModel = 0,

    /// <summary>
    /// The model the provider reported.
    /// </summary>
    Model = 1,

    /// <summary>
    /// The deployment the completion was sent to.
    /// </summary>
    Deployment = 2,

    /// <summary>
    /// The AI profile the completion belonged to.
    /// </summary>
    Profile = 3,

    /// <summary>
    /// The provider connection the completion was sent through.
    /// </summary>
    Connection = 4,
}
