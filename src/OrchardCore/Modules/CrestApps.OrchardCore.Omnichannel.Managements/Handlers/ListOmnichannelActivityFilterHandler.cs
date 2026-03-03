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

        if (!string.IsNullOrEmpty(filter.AttemptFilter))
        {
            if (filter.AttemptFilter.EndsWith('+'))
            {
                // Handle "1+", "2+", etc. - minimum attempts
                if (int.TryParse(filter.AttemptFilter.TrimEnd('+'), out var minAttempts))
                {
                    context.Query = context.Query.Where(index => index.Attempts >= minAttempts);
                }
            }
            else if (filter.AttemptFilter.EndsWith('-'))
            {
                // Handle "2-", "3-", etc. - maximum attempts
                if (int.TryParse(filter.AttemptFilter.TrimEnd('-'), out var maxAttempts))
                {
                    context.Query = context.Query.Where(index => index.Attempts <= maxAttempts);
                }
            }
            else if (int.TryParse(filter.AttemptFilter, out var exactAttempts))
            {
                // Handle exact values "0", "1", "2", etc.
                context.Query = context.Query.Where(index => index.Attempts == exactAttempts);
            }
        }

        if (filter.ScheduledFrom.HasValue)
        {
            context.Query = context.Query.Where(index => index.ScheduledUtc >= filter.ScheduledFrom.Value);
        }

        if (filter.ScheduledTo.HasValue)
        {
            context.Query = context.Query.Where(index => index.ScheduledUtc <= filter.ScheduledTo.Value);
        }

        return Task.CompletedTask;
    }
}
