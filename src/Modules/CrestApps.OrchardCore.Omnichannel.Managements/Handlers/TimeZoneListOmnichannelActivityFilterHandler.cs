using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Filters omnichannel activities by the lead time zone stored on the related contact.
/// </summary>
public sealed class TimeZoneListOmnichannelActivityFilterHandler : IListOmnichannelActivityFilterHandler
{
    private const string ContactAlias = "tz_contact";
    private const string TimeZoneParameter = "@TimeZoneFilterId";
    private const string PublishedParameter = "@TimeZoneFilterPublished";

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
    public Task FilteringAsync(ListOmnichannelActivityFilterContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(context.Filter.TimeZoneId))
        {
            return Task.CompletedTask;
        }

        // The time zone is matched in SQL rather than by loading the contacts in that time zone and binding their
        // identifiers into an IN list. That list carried a parameter per contact, and SQL Server refuses a command
        // with more than 2,100 parameters, so a tenant with that many contacts in one time zone could not open the
        // list at all. The list is paged, so it cannot be split into batches either.
        //
        // Contacts are content items in the default collection while activities live in their own collection.
        // YesSql's IsIn<TIndex> subquery resolves its index table in the outer query's collection, so the subquery
        // is written here against the default-collection contact index. EXISTS rather than a join, because a
        // contact with a published version and a draft has a row per version and a join would list its
        // activities once per row.
        var configuration = _session.Store.Configuration;
        var contactTable = configuration.TableNameConvention.GetIndexTable(typeof(OmnichannelContactIndex));
        var activityAlias = context.Query.GetTypeAlias(typeof(OmnichannelActivityIndex));

        context.Query = context.Query
            .WithParameter(TimeZoneParameter, context.Filter.TimeZoneId)

            // Bound rather than written as the literal 1: PostgreSQL stores the column as boolean and refuses
            // "boolean = integer".
            .WithParameter(PublishedParameter, true)
            .Where(dialect =>
            {
                var quotedContactAlias = dialect.QuoteForAliasName(ContactAlias);
                var contactTableName = dialect.QuoteForTableName(configuration.TablePrefix + contactTable, configuration.Schema);
                var contactItemIdCol = $"{quotedContactAlias}.{dialect.QuoteForColumnName(nameof(OmnichannelContactIndex.ContentItemId))}";
                var publishedCol = $"{quotedContactAlias}.{dialect.QuoteForColumnName(nameof(OmnichannelContactIndex.Published))}";
                var timeZoneCol = $"{quotedContactAlias}.{dialect.QuoteForColumnName(nameof(OmnichannelContactIndex.TimeZoneId))}";
                var activityContactCol = $"{dialect.QuoteForAliasName(activityAlias)}.{dialect.QuoteForColumnName(nameof(OmnichannelActivityIndex.ContactContentItemId))}";

                return $"EXISTS (SELECT 1 FROM {contactTableName} AS {quotedContactAlias} " +
                    $"WHERE {contactItemIdCol} = {activityContactCol} " +
                    $"AND {publishedCol} = {PublishedParameter} " +
                    $"AND {timeZoneCol} = {TimeZoneParameter})";
            });

        return Task.CompletedTask;
    }
}
