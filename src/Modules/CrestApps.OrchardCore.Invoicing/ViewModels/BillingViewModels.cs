using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.ViewModels;

/// <summary>
/// The editor of a charge kept to bill later.
/// </summary>
public class BillableItemViewModel
{
    public string Party { get; set; }

    public string PartyName { get; set; }

    public DocumentLineKind Kind { get; set; }

    public DateOnly Date { get; set; }

    public string Description { get; set; }

    public decimal Quantity { get; set; } = 1m;

    public string Unit { get; set; }

    public decimal UnitCost { get; set; }

    public decimal MarkupPercent { get; set; }

    public string Currency { get; set; }

    public string TaxCategoryCode { get; set; }

    public bool IsBilled { get; set; }

    public string InvoiceId { get; set; }

    public string PartySearchEndpoint { get; set; }
}

/// <summary>
/// The draft invoice's card of unbilled work.
/// </summary>
public class InvoiceUnbilledWorkViewModel
{
    public Invoice Invoice { get; set; }

    public bool CanManage { get; set; }

    public bool HasParty { get; set; }

    public IReadOnlyList<BillableCandidate> Charges { get; set; } = [];

    public bool CanBillTime { get; set; }

    public string TimeUser { get; set; }

    public DateOnly? TimeFrom { get; set; }

    public DateOnly? TimeThrough { get; set; }

    public decimal? TimeRate { get; set; }

    public IReadOnlyList<BillableCandidate> Time { get; set; }

    public BillableQuery TimeQuery
        => new() { UserId = TimeUser, From = TimeFrom, Through = TimeThrough, HourlyRate = TimeRate, Currency = Invoice?.Currency };
}

/// <summary>
/// The editor of a usage meter.
/// </summary>
public class UsageMeterViewModel
{
    public string Name { get; set; }

    public string Code { get; set; }

    public string Unit { get; set; }

    public UsageAggregation Aggregation { get; set; }

    public bool Enabled { get; set; } = true;
}

/// <summary>
/// A meter's usage page: its latest records and the form that records usage by hand.
/// </summary>
public class UsageMeterUsageViewModel
{
    public UsageMeter Meter { get; set; }

    public DateTime OccurredLocal { get; set; }

    public string PartySearchEndpoint { get; set; }

    public List<UsageRecordRowViewModel> Records { get; } = [];
}

/// <summary>
/// A usage record on its meter's page, in the site's time.
/// </summary>
public class UsageRecordRowViewModel
{
    public UsageRecord Record { get; set; }

    public string PartyName { get; set; }

    public DateTime OccurredLocal { get; set; }
}
