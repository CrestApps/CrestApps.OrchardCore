using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.WorkOrders.Agreements.ViewModels;

/// <summary>
/// A row of the agreement list.
/// </summary>
public sealed class ServiceAgreementRow
{
    /// <summary>
    /// Gets or sets the agreement.
    /// </summary>
    public ServiceAgreement Agreement { get; set; }
}

/// <summary>
/// The agreement list.
/// </summary>
public class ServiceAgreementsListViewModel
{
    /// <summary>
    /// Gets or sets the search.
    /// </summary>
    public string Q { get; set; }

    /// <summary>
    /// Gets or sets the status filter.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the rows.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }
}

/// <summary>
/// A schedule in the agreement editor.
/// </summary>
public class ScheduleEditModel
{
    /// <summary>
    /// Gets or sets the schedule's id, kept across edits.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets its name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the work order type.
    /// </summary>
    public string TypeId { get; set; }

    /// <summary>
    /// Gets or sets the chosen repeat, or empty for a custom rule.
    /// </summary>
    public string Rule { get; set; }

    /// <summary>
    /// Gets or sets the custom rule.
    /// </summary>
    public string CustomRule { get; set; }

    /// <summary>
    /// Gets or sets how many days ahead the work order is made.
    /// </summary>
    public int LeadDays { get; set; } = 14;
}

/// <summary>
/// The agreement editor.
/// </summary>
public class ServiceAgreementEditViewModel
{
    /// <summary>
    /// Gets or sets the id, or <see langword="null"/> for a new agreement.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the number.
    /// </summary>
    [BindNever]
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public ServiceAgreementStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the customer number.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the company number.
    /// </summary>
    public string CompanyId { get; set; }

    /// <summary>
    /// Gets or sets the contact's name.
    /// </summary>
    public string ContactName { get; set; }

    /// <summary>
    /// Gets or sets the contact's phone.
    /// </summary>
    public string ContactPhone { get; set; }

    /// <summary>
    /// Gets or sets the contact's email.
    /// </summary>
    public string ContactEmail { get; set; }

    /// <summary>
    /// Gets or sets the street address.
    /// </summary>
    public string AddressLine1 { get; set; }

    /// <summary>
    /// Gets or sets the city.
    /// </summary>
    public string City { get; set; }

    /// <summary>
    /// Gets or sets the region.
    /// </summary>
    public string Region { get; set; }

    /// <summary>
    /// Gets or sets the postal code.
    /// </summary>
    public string PostalCode { get; set; }

    /// <summary>
    /// Gets or sets the country.
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// Gets or sets how to get in.
    /// </summary>
    public string AccessNotes { get; set; }

    /// <summary>
    /// Gets or sets the assets it covers.
    /// </summary>
    public List<string> AssetIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the first day.
    /// </summary>
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// Gets or sets the last day.
    /// </summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the time zone.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets how the visits are paid for.
    /// </summary>
    public AgreementPricing Pricing { get; set; }

    /// <summary>
    /// Gets or sets the price of a visit.
    /// </summary>
    public decimal PricePerVisit { get; set; }

    /// <summary>
    /// Gets or sets the days of notice before the end.
    /// </summary>
    public int ExpiringNoticeDays { get; set; } = 30;

    /// <summary>
    /// Gets or sets notes.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets the schedules.
    /// </summary>
    public List<ScheduleEditModel> Schedules { get; set; } = [];

    /// <summary>
    /// Gets or sets the work order types.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TypeOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the assets, when Assets is on.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> AssetOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the time zones.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TimeZoneOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the usual repeats.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> RuleOptions { get; set; } = [];
}

/// <summary>
/// The upcoming visits of a schedule.
/// </summary>
public sealed class ScheduleVisits
{
    /// <summary>
    /// Gets or sets the schedule.
    /// </summary>
    public AgreementSchedule Schedule { get; set; }

    /// <summary>
    /// Gets or sets its work order type's name.
    /// </summary>
    public string TypeName { get; set; }

    /// <summary>
    /// Gets or sets the repeat in words.
    /// </summary>
    public string Repeats { get; set; }

    /// <summary>
    /// Gets or sets the next visit days.
    /// </summary>
    public IList<DateOnly> Next { get; set; } = [];
}

/// <summary>
/// An agreement's page.
/// </summary>
public class ServiceAgreementDetailsViewModel
{
    /// <summary>
    /// Gets or sets the agreement.
    /// </summary>
    public ServiceAgreement Agreement { get; set; }

    /// <summary>
    /// Gets or sets its schedules with their next visits.
    /// </summary>
    public IList<ScheduleVisits> Schedules { get; set; } = [];

    /// <summary>
    /// Gets or sets the work orders it made, newest first.
    /// </summary>
    public IList<WorkOrder> WorkOrders { get; set; } = [];

    /// <summary>
    /// Gets or sets the names of its assets.
    /// </summary>
    public IList<SelectListItem> Assets { get; set; } = [];
}
