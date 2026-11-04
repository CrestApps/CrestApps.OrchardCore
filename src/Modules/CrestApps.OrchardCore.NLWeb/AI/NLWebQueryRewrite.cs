namespace CrestApps.OrchardCore.NLWeb.AI;

/// <summary>
/// The model's rewrite of a follow-up question.
/// </summary>
public sealed class NLWebQueryRewrite
{
    /// <summary>
    /// Gets or sets the question, rewritten to stand on its own.
    /// </summary>
    public string Query { get; set; }

    /// <summary>
    /// Gets or sets the schema.org type of the items asked for, when the question names one.
    /// </summary>
    public string ItemType { get; set; }
}
