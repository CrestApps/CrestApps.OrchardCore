using YesSql.Filters.Query;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Parses subscription admin list filter text by using the configured YesSql query parser.
/// </summary>
public class DefaultSubscriptionsAdminListFilterParser : ISubscriptionAdminListFilterParser
{
    private readonly IQueryParser<SubscriptionSession> _parser;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSubscriptionsAdminListFilterParser"/> class.
    /// </summary>
    /// <param name="parser">The query parser used to parse subscription session filter text.</param>
    public DefaultSubscriptionsAdminListFilterParser(IQueryParser<SubscriptionSession> parser)
    {
        _parser = parser;
    }

    /// <summary>
    /// Parses the supplied filter text into a subscription session query filter result.
    /// </summary>
    /// <param name="text">The filter text to parse.</param>
    /// <returns>The parsed subscription session query filter result.</returns>
    public QueryFilterResult<SubscriptionSession> Parse(string text)
        => _parser.Parse(text);
}
