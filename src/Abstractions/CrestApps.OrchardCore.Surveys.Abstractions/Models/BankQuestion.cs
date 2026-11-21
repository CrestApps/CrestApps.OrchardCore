using CrestApps.Core.Models;
using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// A question of a question bank. Its id becomes the question id in every version that draws it.
/// </summary>
public sealed class BankQuestion : CatalogItem, IAuditedModel
{
    /// <summary>
    /// Gets or sets the id of the bank.
    /// </summary>
    public string BankId { get; set; }

    /// <summary>
    /// Gets or sets the question, scoring key included.
    /// </summary>
    public SurveyQuestion Question { get; set; } = new();

    /// <summary>
    /// Gets or sets tags pools filter on.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Gets or sets the difficulty from 1 (easiest) to 5 (hardest).
    /// </summary>
    public int Difficulty { get; set; } = 3;

    /// <summary>
    /// Gets or sets the identifier of the source it was imported from, to avoid duplicates.
    /// </summary>
    public string SourceIdentifier { get; set; }

    /// <summary>
    /// Gets or sets whether pools no longer draw it.
    /// </summary>
    public bool Retired { get; set; }

    /// <summary>
    /// Gets or sets the revision, increased on every save.
    /// </summary>
    public int Revision { get; set; }

    /// <summary>
    /// Gets or sets when the question was created, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the question last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the id of the user who owns the question.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the user name of the user who created the question.
    /// </summary>
    public string Author { get; set; }
}
