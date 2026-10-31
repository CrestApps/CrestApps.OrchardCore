using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Stores shift templates.
/// </summary>
internal sealed class ShiftTemplateStore : DocumentCatalog<ShiftTemplate, ShiftTemplateIndex>
{
    public ShiftTemplateStore(ISession session)
        : base(session)
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    public Task<IReadOnlyList<ShiftTemplate>> ListAsync(CancellationToken cancellationToken = default)
    {
        return ListByNameAsync(index => index.Name, cancellationToken);
    }
}
