using YesSql.Indexes;

namespace CrestApps.Core.Data.YesSql;

/// <summary>
/// Marks a YesSql index as containing a <see cref="Name"/> column,
/// enabling efficient queries and filtering by the entry's unique technical name.
/// </summary>
public interface INameAwareIndex : IIndex
{
    /// <summary>
    /// Gets or sets the unique technical name stored in the index.
    /// </summary>
    string Name { get; set; }
}
