using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;

/// <summary>
/// A planning group's day as it happens.
/// </summary>
public class IntradayViewModel
{
    /// <summary>
    /// Gets or sets the planning group.
    /// </summary>
    public string PlanningGroupId { get; set; }

    /// <summary>
    /// Gets or sets the day.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets how many people to take off the phones.
    /// </summary>
    public int People { get; set; } = 2;

    /// <summary>
    /// Gets or sets for how many minutes.
    /// </summary>
    public int Minutes { get; set; } = 30;

    /// <summary>
    /// Gets or sets the planning groups.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> PlanningGroupOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the planning group shown.
    /// </summary>
    [BindNever]
    public PlanningGroup Group { get; set; }

    /// <summary>
    /// Gets or sets the team's time zone.
    /// </summary>
    [BindNever]
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the rows.
    /// </summary>
    [BindNever]
    public IReadOnlyList<IntradayRow> Rows { get; set; } = [];

    /// <summary>
    /// Gets or sets the reforecast factor the work so far suggests.
    /// </summary>
    [BindNever]
    public double Factor { get; set; } = 1;

    /// <summary>
    /// Gets or sets the quietest times for the people and minutes asked.
    /// </summary>
    [BindNever]
    public IReadOnlyList<ActivityWindow> Windows { get; set; } = [];

    /// <summary>
    /// Gets or sets the day's offers.
    /// </summary>
    [BindNever]
    public IReadOnlyList<IntradayOffer> Offers { get; set; } = [];

    /// <summary>
    /// Gets or sets the names of the workers who took offers.
    /// </summary>
    [BindNever]
    public IDictionary<string, string> WorkerNames { get; set; } = new Dictionary<string, string>();
}

/// <summary>
/// The form that offers voluntary overtime or time off.
/// </summary>
public class IntradayOfferEditViewModel
{
    /// <summary>
    /// Gets or sets the planning group.
    /// </summary>
    public string PlanningGroupId { get; set; }

    /// <summary>
    /// Gets or sets the day.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Gets or sets the kind.
    /// </summary>
    public IntradayOfferKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the start, in the team's time zone.
    /// </summary>
    public TimeOnly Start { get; set; }

    /// <summary>
    /// Gets or sets the end, in the team's time zone.
    /// </summary>
    public TimeOnly End { get; set; }

    /// <summary>
    /// Gets or sets how many workers can take it.
    /// </summary>
    public int Places { get; set; } = 1;

    /// <summary>
    /// Gets or sets the note.
    /// </summary>
    public string Note { get; set; }
}

/// <summary>
/// The My Work page of offers.
/// </summary>
public class MyOffersViewModel
{
    /// <summary>
    /// Gets or sets the portal menu.
    /// </summary>
    public MyWorkNavViewModel Nav { get; set; }

    /// <summary>
    /// Gets or sets the offers the worker can take.
    /// </summary>
    public IReadOnlyList<IntradayOffer> Offers { get; set; } = [];

    /// <summary>
    /// Gets or sets the worker's time zone.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the message to show, after taking an offer.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets whether the message is an error.
    /// </summary>
    public bool IsError { get; set; }
}
