using YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// One row per tag of a bank question, so pools draw questions by tag.
/// </summary>
public sealed class BankQuestionTagIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the id of the bank question.
    /// </summary>
    public string BankQuestionId { get; set; }

    /// <summary>
    /// Gets or sets the id of the bank.
    /// </summary>
    public string BankId { get; set; }

    /// <summary>
    /// Gets or sets the tag, cut to 100 characters.
    /// </summary>
    public string Tag { get; set; }

    /// <summary>
    /// Gets or sets whether pools no longer draw the question.
    /// </summary>
    public bool Retired { get; set; }
}
