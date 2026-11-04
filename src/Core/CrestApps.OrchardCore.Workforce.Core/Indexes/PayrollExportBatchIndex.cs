using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

public sealed class PayrollExportBatchIndex : CatalogItemIndex
{
    public string ProviderName { get; set; }

    public string PayPeriodCalendarId { get; set; }

    public int PeriodStart { get; set; }

    public DateTime CreatedUtc { get; set; }
}
