using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Tickets.WebForm;

/// <summary>
/// The Ticket Form widget as a visitor sees it.
/// </summary>
public class TicketFormViewModel
{
    public string WidgetId { get; set; }

    public bool Ready { get; set; }

    public bool SignedIn { get; set; }

    public bool ShowChallenge { get; set; }

    public string SentNumber { get; set; }

    public string SuccessMessage { get; set; }

    public string Error { get; set; }

    /// <summary>
    /// Gets or sets the ticket type's form as customers fill it in: the subject and the fields customers fill in, with
    /// their rules; <see langword="null"/> before the widget is set up.
    /// </summary>
    public global::OrchardCore.DisplayManagement.IShape Editor { get; set; }

    /// <summary>
    /// Gets or sets whether the form asks for the subject itself (a ticket type without a title).
    /// </summary>
    public bool AsksForSubject { get; set; } = true;

    /// <summary>
    /// Gets or sets the categories customers can choose; none when the type has no categories.
    /// </summary>
    public List<CrestApps.OrchardCore.ContentFields.Taxonomies.CascadingPickerItem> Categories { get; set; } = [];

    /// <summary>
    /// Gets or sets whether a category must be chosen.
    /// </summary>
    public bool CategoryRequired { get; set; }
}

/// <summary>
/// The Ticket Form widget's settings editor.
/// </summary>
public class TicketFormPartViewModel
{
    public string TicketType { get; set; }

    public string DeskId { get; set; }

    public string SuccessMessage { get; set; }

    /// <summary>
    /// Gets or sets whether guests must pass a reCAPTCHA challenge (ReCaptcha is on and set up).
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public bool ChallengeConfigured { get; set; }

    [BindNever]
    public List<SelectListItem> Types { get; set; } = [];

    [BindNever]
    public List<SelectListItem> Desks { get; set; } = [];
}
