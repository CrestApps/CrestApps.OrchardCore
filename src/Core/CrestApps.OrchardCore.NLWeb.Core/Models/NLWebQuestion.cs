namespace CrestApps.OrchardCore.NLWeb.Models;

/// <summary>
/// A question an NLWeb agent asks the caller when it needs more information.
/// </summary>
public sealed class NLWebQuestion
{
    /// <summary>
    /// Gets or sets the identifier of the question.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the question.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the kind of answer: <c>text</c>, <c>single_select</c> or <c>multi_select</c>.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the answers to choose from, for a select question.
    /// </summary>
    public IList<string> Options { get; set; } = [];
}
