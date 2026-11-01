using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

public sealed class PunchCorrectionIndex : CatalogItemIndex
{
    public string WorkerId { get; set; }

    public string TeamId { get; set; }

    public PunchCorrectionStatus Status { get; set; }

    public DateTime CreatedUtc { get; set; }
}
