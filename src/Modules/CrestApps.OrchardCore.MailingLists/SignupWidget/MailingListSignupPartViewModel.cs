using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.MailingLists.SignupWidget;

/// <summary>
/// The sign-up widget's settings editor.
/// </summary>
public class MailingListSignupPartViewModel
{
    /// <summary>
    /// Gets or sets the ids of the lists the form offers.
    /// </summary>
    public string[] ListIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the heading.
    /// </summary>
    public string Heading { get; set; }

    /// <summary>
    /// Gets or sets the text under the heading.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the label of the button.
    /// </summary>
    public string ButtonLabel { get; set; }

    /// <summary>
    /// Gets or sets whether the form asks for the person's name.
    /// </summary>
    public bool AskForName { get; set; }

    /// <summary>
    /// Gets or sets what the visitor reads after signing up.
    /// </summary>
    public string SuccessMessage { get; set; }

    /// <summary>
    /// Gets or sets whether a challenge (such as reCAPTCHA) is set up for public forms.
    /// </summary>
    [BindNever]
    public bool ChallengeConfigured { get; set; }

    /// <summary>
    /// Gets or sets the lists to choose from.
    /// </summary>
    [BindNever]
    public List<SelectListItem> AvailableLists { get; set; } = [];
}
