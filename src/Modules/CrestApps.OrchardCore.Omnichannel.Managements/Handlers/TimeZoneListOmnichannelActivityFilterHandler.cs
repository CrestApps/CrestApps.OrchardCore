using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Filters omnichannel activities by the lead time zone stored on the related contact.
/// </summary>
public sealed class TimeZoneListOmnichannelActivityFilterHandler : IListOmnichannelActivityFilterHandler
{
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeZoneListOmnichannelActivityFilterHandler"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public TimeZoneListOmnichannelActivityFilterHandler(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc />
    public async Task FilteringAsync(ListOmnichannelActivityFilterContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(context.Filter.TimeZoneId))
        {
            return;
        }

        var contactContentItemIds = (await _session.QueryIndex<OmnichannelContactIndex>(index => index.TimeZoneId == context.Filter.TimeZoneId)
            .ListAsync(cancellationToken))
            .Select(index => index.ContentItemId)
            .Where(contentItemId => !string.IsNullOrEmpty(contentItemId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        context.Query = contactContentItemIds.Length == 0
            ? context.Query.Where(index => index.DocumentId == -1)
            : context.Query.Where(index => index.ContactContentItemId.IsIn(contactContentItemIds));
    }
}
