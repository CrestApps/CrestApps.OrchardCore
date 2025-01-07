using YesSql;
using YesSql.Filters.Query.Services;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class SubscriptionQueryContext : QueryExecutionContext<SubscriptionSession>
{
    public SubscriptionQueryContext(
        IServiceProvider serviceProvider,
        IQuery<SubscriptionSession> query)
        : base(query)
    {
        ServiceProvider = serviceProvider;
    }

    public IServiceProvider ServiceProvider { get; }
}
