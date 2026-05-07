using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Handles events for list omnichannel activity filter.
/// </summary>
public sealed class ListOmnichannelActivityFilterHandler : IListOmnichannelActivityFilterHandler
{
    /// <summary>
    /// Asynchronously performs the filtering operation.
    /// </summary>
    /// <param name="context">The context.</param>
    public Task FilteringAsync(ListOmnichannelActivityFilterContext context, CancellationToken cancellationToken = default)
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
                    if (minAttempts <= 1)
                    {
                        minAttempts = 2;
                    }

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
                // Handle exact values where 0 or 1 both mean "no attempt".
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

        return Task.CompletedTask;
    }
}
