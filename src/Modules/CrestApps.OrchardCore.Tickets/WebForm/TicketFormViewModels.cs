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
