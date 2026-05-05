using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Defines the contract for bulk manage activity filter handlers.
/// Implement this interface to add custom filtering logic that extends the bulk manage activities query.
/// </summary>
public interface IBulkManageActivityFilterHandler
{
    /// <summary>
    /// Applies additional filtering logic to the bulk manage activity query context.
    /// </summary>
    /// <param name="context">The filter context containing the filter criteria and query to update.</param>
    Task FilteringAsync(BulkManageActivityFilterContext context);
}

/// <summary>
/// Represents the context for bulk manage activity filtering, containing both the filter criteria and the query to modify.
/// </summary>
public sealed class BulkManageActivityFilterContext
{
    /// <summary>
    /// Gets the filter criteria.
    /// </summary>
    public BulkManageActivityFilter Filter { get; }

    /// <summary>
    /// Gets or sets the query to apply filtering to.
    /// </summary>
    public IQuery<OmnichannelActivity, OmnichannelActivityIndex> Query { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BulkManageActivityFilterContext"/> class.
    /// </summary>
    /// <param name="filter">The filter criteria.</param>
    /// <param name="query">The query to filter.</param>
    public BulkManageActivityFilterContext(
        BulkManageActivityFilter filter,
        IQuery<OmnichannelActivity, OmnichannelActivityIndex> query)
    {
        Filter = filter;
        Query = query;
    }
}
