using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

public sealed class ListOmnichannelActivityFilterHandler : IListOmnichannelActivityFilterHandler
{
    public Task FilteringAsync(ListOmnichannelActivityFilterContext context)
    {
        var filter = context.Filter;

        if (filter.UrgencyLevel.HasValue)
        {
            context.Query = context.Query.Where(index => index.UrgencyLevel == filter.UrgencyLevel.Value);
        }

        if (!string.IsNullOrEmpty(filter.SubjectContentType))
        {
            context.Query = context.Query.Where(index => index.SubjectContentType == filter.SubjectContentType);
        }

        if (!string.IsNullOrEmpty(filter.Channel))
        {
            context.Query = context.Query.Where(index => index.Channel == filter.Channel);
        }

        if (filter.AttemptFrom.HasValue)
        {
            context.Query = context.Query.Where(index => index.Attempts >= filter.AttemptFrom.Value);
        }

        if (filter.AttemptTo.HasValue)
        {
            context.Query = context.Query.Where(index => index.Attempts <= filter.AttemptTo.Value);
        }

        return Task.CompletedTask;
    }
}
