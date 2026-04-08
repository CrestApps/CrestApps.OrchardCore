using YesSql.Indexes;

namespace CrestApps.Core.Data.YesSql;

/// <summary>
/// Marks a YesSql index as containing a <see cref="Source"/> column,
/// enabling efficient queries and filtering by the entry's source or provider name.
/// </summary>
public interface ISourceAwareIndex : IIndex
{
    /// <summary>
    /// Gets or sets the source or provider name stored in the index.
    /// </summary>
    string Source { get; set; }
}
