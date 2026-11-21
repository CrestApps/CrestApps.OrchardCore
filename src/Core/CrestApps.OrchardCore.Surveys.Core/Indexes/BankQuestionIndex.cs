using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Finds the questions of a bank.
/// </summary>
public sealed class BankQuestionIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the id of the bank.
    /// </summary>
    public string BankId { get; set; }

    /// <summary>
    /// Gets or sets the question type.
    /// </summary>
    public string ControlType { get; set; }

    /// <summary>
    /// Gets or sets the question text, cut to 255 characters.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the difficulty.
    /// </summary>
    public int Difficulty { get; set; }

    /// <summary>
    /// Gets or sets whether pools no longer draw it.
    /// </summary>
    public bool Retired { get; set; }

    /// <summary>
    /// Gets or sets the identifier of its import source.
    /// </summary>
    public string SourceIdentifier { get; set; }

    /// <summary>
    /// Gets or sets when it last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
