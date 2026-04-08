using YesSql.Indexes;

namespace CrestApps.Core.Data.YesSql;

/// <summary>
/// Marks a YesSql index as containing a <see cref="DisplayText"/> column,
/// enabling efficient queries and filtering by human-readable display text.
/// </summary>
public interface IDisplayTextAwareIndex : IIndex
{
    /// <summary>
    /// Gets or sets the human-readable display text stored in the index.
    /// </summary>
    string DisplayText { get; set; }
}
