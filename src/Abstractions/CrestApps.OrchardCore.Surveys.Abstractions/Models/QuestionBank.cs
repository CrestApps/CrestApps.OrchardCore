using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// A bank of reusable questions that pools draw from.
/// </summary>
public sealed class QuestionBank : CatalogItem, IAuditedModel, INameAwareModel
{
    /// <summary>
    /// Gets or sets the unique technical name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets free tags.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Gets or sets the record that owns the bank, or null.
    /// </summary>
    public RecordRef? Owner { get; set; }

    /// <summary>
    /// Gets or sets when the bank was created, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the bank last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the id of the user who owns the bank.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the user name of the user who created the bank.
    /// </summary>
    public string Author { get; set; }
}
