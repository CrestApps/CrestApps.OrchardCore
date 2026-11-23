using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Taxation.ViewModels;

/// <summary>
/// The page of a year's tax return periods.
/// </summary>
public class TaxReturnsIndexViewModel
{
    /// <summary>
    /// Gets or sets the year shown.
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Gets or sets the periods, by due date.
    /// </summary>
    public IReadOnlyList<TaxReturnPeriodSummary> Periods { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether a registration files returns.
    /// </summary>
    public bool HasFilingRegistrations { get; set; }

    /// <summary>
    /// Gets or sets the rows of the list.
    /// </summary>
    public AdminListContext<TaxReturnPeriodSummary> List { get; set; }
}

/// <summary>
/// The year filter of the tax return periods.
/// </summary>
public class TaxReturnsYearFilterViewModel
{
    /// <summary>
    /// Gets or sets the years to choose from.
    /// </summary>
    public IList<SelectListItem> Years { get; set; } = [];
}

/// <summary>
/// A row of the tax return periods.
/// </summary>
public class TaxReturnPeriodRowViewModel
{
    /// <summary>
    /// Gets or sets the period.
    /// </summary>
    public TaxReturnPeriodSummary Summary { get; set; }

    /// <summary>
    /// Gets or sets the name of the form.
    /// </summary>
    public string FormName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user may prepare returns.
    /// </summary>
    public bool CanPrepare { get; set; }
}

/// <summary>
/// The page of a tax return.
/// </summary>
public class TaxReturnDetailsViewModel
{
    /// <summary>
    /// Gets or sets the return.
    /// </summary>
    public TaxReturn Return { get; set; }

    /// <summary>
    /// Gets or sets the title of the page.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the return's cards, built by its display drivers.
    /// </summary>
    public IShape Display { get; set; }

    /// <summary>
    /// Gets or sets today, the day a return is marked as filed on by default.
    /// </summary>
    public DateOnly Today { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user may file the return.
    /// </summary>
    public bool CanFile { get; set; }
}

/// <summary>
/// What the cards of a tax return show.
/// </summary>
public class TaxReturnDetailViewModel
{
    /// <summary>
    /// Gets or sets the return.
    /// </summary>
    public TaxReturn Return { get; set; }

    /// <summary>
    /// Gets or sets the name of the registration.
    /// </summary>
    public string RegistrationName { get; set; }

    /// <summary>
    /// Gets or sets the name of the form.
    /// </summary>
    public string FormName { get; set; }

    /// <summary>
    /// Gets or sets the return this one corrects, or <see langword="null"/>.
    /// </summary>
    public TaxReturn Amends { get; set; }

    /// <summary>
    /// Gets or sets today, the day a payment is recorded on by default.
    /// </summary>
    public DateOnly Today { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user may file the return and record its payments.
    /// </summary>
    public bool CanFile { get; set; }
}

/// <summary>
/// The e-filing card of a tax return.
/// </summary>
public class TaxReturnEFilingViewModel
{
    /// <summary>
    /// Gets or sets the return.
    /// </summary>
    public TaxReturn Return { get; set; }

    /// <summary>
    /// Gets or sets the submissions, newest first.
    /// </summary>
    public IReadOnlyList<EFileSubmission> Submissions { get; set; } = [];

    /// <summary>
    /// Gets or sets the pairs of format and channel offered, as <c>format|channel</c> values.
    /// </summary>
    public IList<SelectListItem> Options { get; set; } = [];

    /// <summary>
    /// Gets or sets why the return can't be filed now, or <see langword="null"/>.
    /// </summary>
    public string NotFileableReason { get; set; }

    /// <summary>
    /// Gets or sets the names of the channels, by stored name.
    /// </summary>
    public IDictionary<string, string> TransportNames { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets today, the day a document is recorded as filed on by default.
    /// </summary>
    public DateOnly Today { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the user may file the return.
    /// </summary>
    public bool CanFile { get; set; }
}
