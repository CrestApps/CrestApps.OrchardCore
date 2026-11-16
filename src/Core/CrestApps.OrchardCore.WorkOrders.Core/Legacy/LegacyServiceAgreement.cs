using CrestApps.Core.Models;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.WorkOrders.Models;

namespace CrestApps.OrchardCore.WorkOrders.Core.Legacy;

/// <summary>
/// A service agreement as Work Orders kept it before agreements moved to the Agreements module, read only to move it.
/// </summary>
public sealed class LegacyServiceAgreement : CatalogItem
{
    public string Number { get; set; }

    public string Name { get; set; }

    /// <summary>
    /// Gets or sets where it was: Draft, Active, Expired or Canceled.
    /// </summary>
    public string Status { get; set; }

    public string CustomerId { get; set; }

    public string AccountId { get; set; }

    public WorkOrderContact Contact { get; set; } = new();

    public Address ServiceAddress { get; set; }

    public string AccessNotes { get; set; }

    public List<string> AssetIds { get; set; } = [];

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string TimeZoneId { get; set; }

    public List<AgreementSchedule> Schedules { get; set; } = [];

    public AgreementPricing Pricing { get; set; }

    public decimal PricePerVisit { get; set; }

    public int ExpiringNoticeDays { get; set; } = 30;

    public DateTime? ExpiringNotifiedUtc { get; set; }

    public string Notes { get; set; }

    public DateTime CreatedUtc { get; set; }
}
