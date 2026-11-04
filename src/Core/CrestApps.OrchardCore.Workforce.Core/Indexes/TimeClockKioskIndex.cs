using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

public sealed class TimeClockKioskIndex : CatalogItemIndex, INameAwareIndex
{
    public string Name { get; set; }

    public string TokenHash { get; set; }

    public bool Enabled { get; set; }
}
