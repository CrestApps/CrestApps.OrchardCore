using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores pay codes.
/// </summary>
internal sealed class PayCodeStore : DocumentCatalog<PayCode, PayCodeIndex>
{
    public PayCodeStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    public Task<IReadOnlyList<PayCode>> ListAsync(CancellationToken cancellationToken = default)
    {
        return ListByNameAsync(index => index.Name, cancellationToken);
    }

    public Task<PayCode> FindByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return Session.Query<PayCode, PayCodeIndex>(index => index.Code == code, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);
    }
}
