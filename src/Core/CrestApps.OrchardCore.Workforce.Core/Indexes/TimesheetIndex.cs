using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

public sealed class TimesheetIndex : CatalogItemIndex
{
    public string WorkerId { get; set; }

    public string TeamId { get; set; }

    public string PayPeriodCalendarId { get; set; }

    public int PeriodStart { get; set; }

    public int PeriodEnd { get; set; }

    public TimesheetStatus Status { get; set; }
}
