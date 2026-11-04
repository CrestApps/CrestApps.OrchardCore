using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

public sealed class TimeClockPolicyIndex : CatalogItemIndex, INameAwareIndex
{
    public string Name { get; set; }

    public bool Enabled { get; set; }
}
