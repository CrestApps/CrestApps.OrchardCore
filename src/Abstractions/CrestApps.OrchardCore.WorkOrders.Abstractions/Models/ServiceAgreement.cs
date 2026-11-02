using CrestApps.Core.Models;
using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.WorkOrders.Models;

/// <summary>
/// Where a service agreement is.
/// </summary>
public enum ServiceAgreementStatus
{
    /// <summary>
    /// Being written; makes no work orders.
    /// </summary>
    Draft,

    /// <summary>
    /// In force; makes its work orders ahead of time.
    /// </summary>
    Active,

    /// <summary>
    /// Past its end date.
    /// </summary>
    Expired,

    /// <summary>
    /// Ended early.
    /// </summary>
    Canceled,
}

/// <summary>
/// How the visits of an agreement are paid for.
/// </summary>
public enum AgreementPricing
{
    /// <summary>
    /// The agreement covers them: their work orders aren't invoiced.
    /// </summary>
    Included,

    /// <summary>
    /// Each visit is invoiced at the agreement's price.
    /// </summary>
    PerVisit,
}

/// <summary>
/// A recurring visit of a service agreement, such as a boiler service every six months.
/// </summary>
public sealed class AgreementSchedule
{
    /// <summary>
    /// Gets or sets the schedule's id, part of each work order's source.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets its name, such as Annual inspection.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the type of the work orders it makes.
    /// </summary>
    public string TypeId { get; set; }

    /// <summary>
    /// Gets or sets when it repeats, as an RFC 5545 rule such as <c>FREQ=MONTHLY;INTERVAL=6</c>, from the agreement's
    /// start date.
    /// </summary>
    public string Rule { get; set; }

    /// <summary>
    /// Gets or sets how many days ahead of a visit its work order is made.
    /// </summary>
    public int LeadDays { get; set; } = 14;

    /// <summary>
    /// Gets or sets the assets it covers; empty for every asset of the agreement.
    /// </summary>
    public List<string> AssetIds { get; set; } = [];
}

/// <summary>
/// A service agreement: recurring visits, such as preventive maintenance, for a customer over a period.
/// </summary>
public sealed class ServiceAgreement : CatalogItem
{
    /// <summary>
    /// Gets or sets the number, such as <c>SA-00001</c>.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets where it is.
    /// </summary>
    public ServiceAgreementStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the customer.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the company.
    /// </summary>
    public string CompanyId { get; set; }

    /// <summary>
    /// Gets or sets the person to contact.
    /// </summary>
    public WorkOrderContact Contact { get; set; } = new();

    /// <summary>
    /// Gets or sets where the visits are.
    /// </summary>
    public Address ServiceAddress { get; set; }

    /// <summary>
    /// Gets or sets how to get in.
    /// </summary>
    public string AccessNotes { get; set; }

    /// <summary>
    /// Gets or sets the assets it covers.
    /// </summary>
    public List<string> AssetIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the first day it is in force, where the schedules start.
    /// </summary>
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// Gets or sets the last day it is in force, or <see langword="null"/> when it doesn't end.
    /// </summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the time zone its days are in.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the recurring visits.
    /// </summary>
    public List<AgreementSchedule> Schedules { get; set; } = [];

    /// <summary>
    /// Gets or sets how the visits are paid for.
    /// </summary>
    public AgreementPricing Pricing { get; set; }

    /// <summary>
    /// Gets or sets the price of a visit, when each is invoiced.
    /// </summary>
    public decimal PricePerVisit { get; set; }

    /// <summary>
    /// Gets or sets how many days before the end date the expiring event starts.
    /// </summary>
    public int ExpiringNoticeDays { get; set; } = 30;

    /// <summary>
    /// Gets or sets when the expiring event started, so it starts once.
    /// </summary>
    public DateTime? ExpiringNotifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets notes.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets when it was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
