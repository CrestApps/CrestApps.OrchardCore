using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Represents the list omnichannel activity filter context.
/// </summary>
public sealed class ListOmnichannelActivityFilterContext
{
    /// <summary>
    /// Gets the filter.
    /// </summary>
    public ListOmnichannelActivityFilter Filter { get; }

    /// <summary>
    /// Gets or sets the query.
    /// </summary>
    public IQuery<OmnichannelActivity, OmnichannelActivityIndex> Query { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ListOmnichannelActivityFilterContext"/> class.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="query">The query.</param>
    public ListOmnichannelActivityFilterContext(
        ListOmnichannelActivityFilter filter,
        IQuery<OmnichannelActivity, OmnichannelActivityIndex> query)
    {
        Filter = filter;
        Query = query;
    }
}
