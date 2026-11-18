using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.WorkOrders.Agreements.ViewModels;

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

    /// <summary>
    /// Gets or sets the assets the schedule's visits are for, or none for the agreement's.
    /// </summary>
    public List<string> AssetIds { get; set; } = [];
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
    public CrestApps.OrchardCore.Agreements.Models.Agreement Agreement { get; set; }

    /// <summary>
    /// Gets or sets the agreement's Work Orders terms.
    /// </summary>
    public WorkOrderAgreementTerms Terms { get; set; } = new();

    /// <summary>
    /// Gets or sets the names of the agreement's customer and CRM account, by kind, when known.
    /// </summary>
    public Dictionary<string, string> CustomerNames { get; set; } = new(StringComparer.Ordinal);

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
