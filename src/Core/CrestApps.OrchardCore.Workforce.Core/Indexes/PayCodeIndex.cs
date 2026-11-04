using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

public sealed class PayCodeIndex : CatalogItemIndex, INameAwareIndex
{
    public string Name { get; set; }

    public string Code { get; set; }

    public PayCodeKind Kind { get; set; }

    public bool Enabled { get; set; }
}
