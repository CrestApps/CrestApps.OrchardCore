using CrestApps.OrchardCore.Omnichannel.Core.Services;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Handles filtering for bulk manage activity queries.
/// </summary>
public sealed class BulkManageActivityFilterHandler : IBulkManageActivityFilterHandler
{
    /// <inheritdoc/>
    public Task FilteringAsync(BulkManageActivityFilterContext context)
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

        if (filter.AssignedToUserIds is { Length: > 0 })
        {
            context.Query = context.Query.Where(index => index.AssignedToId.IsIn(filter.AssignedToUserIds));
        }

        if (!string.IsNullOrEmpty(filter.AttemptFilter))
        {
            if (filter.AttemptFilter.EndsWith('+'))
            {
                if (int.TryParse(filter.AttemptFilter.TrimEnd('+'), out var minAttempts))
                {
                    if (minAttempts <= 1)
                    {
                        minAttempts = 2;
                    }

                    context.Query = context.Query.Where(index => index.Attempts >= minAttempts);
                }
            }
            else if (filter.AttemptFilter.EndsWith('-'))
            {
                if (int.TryParse(filter.AttemptFilter.TrimEnd('-'), out var maxAttempts))
                {
                    context.Query = context.Query.Where(index => index.Attempts <= maxAttempts);
                }
            }
            else if (int.TryParse(filter.AttemptFilter, out var exactAttempts))
            {
                context.Query = exactAttempts <= 1
                    ? context.Query.Where(index => index.Attempts <= 1)
                    : context.Query.Where(index => index.Attempts == exactAttempts);
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

        if (filter.CreatedFrom.HasValue)
        {
            context.Query = context.Query.Where(index => index.CreatedUtc >= filter.CreatedFrom.Value);
        }

        if (filter.CreatedTo.HasValue)
        {
            context.Query = context.Query.Where(index => index.CreatedUtc <= filter.CreatedTo.Value);
        }

        return Task.CompletedTask;
    }
}
