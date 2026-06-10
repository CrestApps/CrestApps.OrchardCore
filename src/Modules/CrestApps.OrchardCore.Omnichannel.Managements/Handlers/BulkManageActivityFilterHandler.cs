using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using OrchardCore.ContentManagement;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Handles filtering for bulk manage activity queries.
/// </summary>
public sealed class BulkManageActivityFilterHandler : IBulkManageActivityFilterHandler
{
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="BulkManageActivityFilterHandler"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public BulkManageActivityFilterHandler(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc/>
    public async Task FilteringAsync(BulkManageActivityFilterContext context, CancellationToken cancellationToken = default)
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

        // Apply contact-level filters (phone number, timezone, DNC date range).
        var contactContentItemIds = await GetFilteredContactContentItemIdsAsync(filter, cancellationToken);

        if (contactContentItemIds is not null)
        {
            if (contactContentItemIds.Length == 0)
            {
                // No contacts matched; force an empty result.
                context.Query = context.Query.Where(index => index.ContactContentItemId == "__no_match__");
            }
            else
            {
                context.Query = context.Query.Where(index => index.ContactContentItemId.IsIn(contactContentItemIds));
            }
        }
    }

    private async Task<string[]> GetFilteredContactContentItemIdsAsync(BulkManageActivityFilter filter, CancellationToken cancellationToken)
    {
        var hasPhoneFilter = !string.IsNullOrEmpty(filter.PhoneNumber);
        var hasTimeZoneFilter = filter.TimeZoneIds is { Length: > 0 };
        var hasDncFilter = filter.DoNotCallFrom.HasValue || filter.DoNotCallTo.HasValue;

        if (!hasPhoneFilter && !hasTimeZoneFilter && !hasDncFilter)
        {
            return null;
        }

        HashSet<string> phoneMatchedIds = null;
        HashSet<string> timeZoneMatchedIds = null;
        HashSet<string> dncMatchedIds = null;

        if (hasPhoneFilter)
        {
            phoneMatchedIds = await GetPhoneFilteredContactIdsAsync(filter.PhoneNumber, filter.PhoneNumberMatchType, cancellationToken);
        }

        if (hasTimeZoneFilter)
        {
            var contactQuery = _session.QueryIndex<OmnichannelContactIndex>(
                index => index.TimeZoneId.IsIn(filter.TimeZoneIds));

            var contacts = await contactQuery.ListAsync(cancellationToken);

            timeZoneMatchedIds = contacts.Select(c => c.ContentItemId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        if (hasDncFilter)
        {
            var dncQuery = _session.QueryIndex<OmnichannelContactCommunicationPreferenceIndex>(
                index => index.DoNotCall);

            if (filter.DoNotCallFrom.HasValue)
            {
                dncQuery = dncQuery.Where(index => index.DoNotCallUtc >= filter.DoNotCallFrom.Value);
            }

            if (filter.DoNotCallTo.HasValue)
            {
                dncQuery = dncQuery.Where(index => index.DoNotCallUtc <= filter.DoNotCallTo.Value);
            }

            var dncContacts = await dncQuery.ListAsync(cancellationToken);

            dncMatchedIds = dncContacts.Select(c => c.ContentItemId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        // Intersect all non-null result sets.
        HashSet<string> result = null;

        foreach (var set in new[] { phoneMatchedIds, timeZoneMatchedIds, dncMatchedIds })
        {
            if (set is null)
            {
                continue;
            }

            if (result is null)
            {
                result = set;
            }
            else
            {
                result.IntersectWith(set);
            }
        }

        return result?.ToArray() ?? [];
    }

    private async Task<HashSet<string>> GetPhoneFilteredContactIdsAsync(
        string phoneNumber,
        PhoneNumberMatchType matchType,
        CancellationToken cancellationToken)
    {
        IQuery<ContentItem, OmnichannelContactIndex> query;

        switch (matchType)
        {
            case PhoneNumberMatchType.Exact:
                query = _session.Query<ContentItem, OmnichannelContactIndex>(index =>
                    index.NormalizedPrimaryCellPhoneNumber == phoneNumber ||
                    index.NormalizedPrimaryHomePhoneNumber == phoneNumber);
                break;

            case PhoneNumberMatchType.BeginsWith:
                query = _session.Query<ContentItem, OmnichannelContactIndex>(index =>
                    index.NormalizedPrimaryCellPhoneNumber.StartsWith(phoneNumber) ||
                    index.NormalizedPrimaryHomePhoneNumber.StartsWith(phoneNumber));
                break;

            case PhoneNumberMatchType.EndsWith:
                query = _session.Query<ContentItem, OmnichannelContactIndex>(index =>
                    index.NormalizedPrimaryCellPhoneNumber.EndsWith(phoneNumber) ||
                    index.NormalizedPrimaryHomePhoneNumber.EndsWith(phoneNumber));
                break;

            default:
                query = _session.Query<ContentItem, OmnichannelContactIndex>(index =>
                    index.NormalizedPrimaryCellPhoneNumber.StartsWith(phoneNumber) ||
                    index.NormalizedPrimaryHomePhoneNumber.StartsWith(phoneNumber));
                break;
        }

        var contacts = await query.ListAsync(cancellationToken);

        return contacts.Select(c => c.ContentItemId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
