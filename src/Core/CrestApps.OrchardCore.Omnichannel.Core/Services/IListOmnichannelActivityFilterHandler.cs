using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

public interface IListOmnichannelActivityFilterHandler
{
    /// <summary>
    /// Applies additional filtering logic to the omnichannel activity query context.
    /// </summary>
    /// <param name="context">The filter context to update.</param>
    Task FilteringAsync(ListOmnichannelActivityFilterContext context);
}

public sealed class ListOmnichannelActivityFilterContext
{
    public ListOmnichannelActivityFilter Filter { get; }

    public IQuery<OmnichannelActivity, OmnichannelActivityIndex> Query { get; set; }

    public ListOmnichannelActivityFilterContext(
        ListOmnichannelActivityFilter filter,
        IQuery<OmnichannelActivity, OmnichannelActivityIndex> query)
    {
        Filter = filter;
        Query = query;
    }
}
