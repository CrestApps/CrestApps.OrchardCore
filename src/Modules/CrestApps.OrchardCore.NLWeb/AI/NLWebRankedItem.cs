namespace CrestApps.OrchardCore.NLWeb.AI;

/// <summary>
/// The model's score of one result.
/// </summary>
public sealed class NLWebRankedItem
{
    /// <summary>
    /// Gets or sets the position of the result in the list the model was given.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets how well the result answers the question, from 0 to 100.
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// Gets or sets one sentence on why the result answers the question.
    /// </summary>
    public string Reason { get; set; }
}
