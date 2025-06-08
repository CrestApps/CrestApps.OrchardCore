using YesSql.Filters.Query;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

public class DefaultSubscriptionsAdminListFilterParser : ISubscriptionAdminListFilterParser
{
    private readonly IQueryParser<SubscriptionSession> _parser;

    public DefaultSubscriptionsAdminListFilterParser(IQueryParser<SubscriptionSession> parser)
    {
        _parser = parser;
    }

    public QueryFilterResult<SubscriptionSession> Parse(string text)
        => _parser.Parse(text);
}
