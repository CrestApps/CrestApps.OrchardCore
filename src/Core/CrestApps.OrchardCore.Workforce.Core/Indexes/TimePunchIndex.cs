using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

public sealed class TimePunchIndex : CatalogItemIndex
{
    public string WorkerId { get; set; }

    public string TeamId { get; set; }

    public TimePunchKind Kind { get; set; }

    public DateTime Utc { get; set; }

    public string IdempotencyKey { get; set; }

    public string ReplacesPunchId { get; set; }
}
