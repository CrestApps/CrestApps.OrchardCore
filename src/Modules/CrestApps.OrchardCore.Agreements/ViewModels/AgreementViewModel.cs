using CrestApps.OrchardCore.Agreements.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Agreements.ViewModels;

/// <summary>
/// The general fields of an agreement's editor.
/// </summary>
public class AgreementViewModel
{
    /// <summary>
    /// Gets or sets the number, shown once it has one.
    /// </summary>
    [BindNever]
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets where it stands.
    /// </summary>
    public AgreementStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the account.
    /// </summary>
    public string AccountId { get; set; }

    /// <summary>
    /// Gets or sets the account's name.
    /// </summary>
    [BindNever]
    public string AccountName { get; set; }

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
    /// Gets or sets the notice before the end, in days.
    /// </summary>
    public int ExpiringNoticeDays { get; set; }

    /// <summary>
    /// Gets or sets the notes.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets the time zones to choose from.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> TimeZones { get; set; } = [];
}
