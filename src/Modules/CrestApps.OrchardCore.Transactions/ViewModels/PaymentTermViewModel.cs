using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// The editor of a payment term.
/// </summary>
public class PaymentTermViewModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the note.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets how the due date follows from the issue date.
    /// </summary>
    public PaymentTermKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the number of days.
    /// </summary>
    public int Days { get; set; }

    /// <summary>
    /// Gets or sets the day of the next month.
    /// </summary>
    public int DayOfMonth { get; set; }

    /// <summary>
    /// Gets or sets the early-payment discount percent.
    /// </summary>
    public decimal? DiscountPercent { get; set; }

    /// <summary>
    /// Gets or sets for how many days the discount is offered.
    /// </summary>
    public int? DiscountDays { get; set; }

    /// <summary>
    /// Gets or sets whether the term may be chosen for new documents.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the kinds of term to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> KindOptions { get; set; } = [];
}
