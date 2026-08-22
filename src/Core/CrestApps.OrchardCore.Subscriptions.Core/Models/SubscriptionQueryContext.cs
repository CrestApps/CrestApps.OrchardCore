using YesSql;
using YesSql.Filters.Query.Services;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Provides query execution context for subscription session filter processing.
/// </summary>
public class SubscriptionQueryContext : QueryExecutionContext<SubscriptionSession>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionQueryContext"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used by query filters.</param>
    /// <param name="query">The YesSql query being filtered.</param>
    public SubscriptionQueryContext(
        IServiceProvider serviceProvider,
        IQuery<SubscriptionSession> query)
        : base(query)
    {
        ServiceProvider = serviceProvider;
    }

    /// <summary>
    /// Gets the service provider used by query filters to resolve request services.
    /// </summary>
    public IServiceProvider ServiceProvider { get; }
}
