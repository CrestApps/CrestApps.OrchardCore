using CrestApps.OrchardCore.Dispatch.Core.Indexes;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Dispatch.Core.Services;

/// <summary>
/// Keeps visits in the Dispatch collection.
/// </summary>
public sealed class ServiceVisitStore : DocumentCatalog<ServiceVisit, ServiceVisitIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceVisitStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public ServiceVisitStore(ISession session)
        : base(session)
    {
        CollectionName = DispatchConstants.CollectionName;
    }
}
