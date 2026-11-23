using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.MailingLists.SignupWidget;

/// <summary>
/// A newsletter sign-up form on a page: which lists it offers and what the visitor reads.
/// </summary>
public sealed class MailingListSignupPart : ContentPart
{
    /// <summary>
    /// Gets or sets the ids of the lists the form offers; one list is joined at once, several are offered as checkboxes.
    /// </summary>
    public string[] ListIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the heading above the form; empty for none.
    /// </summary>
    public string Heading { get; set; }

    /// <summary>
    /// Gets or sets the text under the heading; empty for none.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets the label of the button; empty for the default.
    /// </summary>
    public string ButtonLabel { get; set; }

    /// <summary>
    /// Gets or sets whether the form asks for the person's name.
    /// </summary>
    public bool AskForName { get; set; }

    /// <summary>
    /// Gets or sets what the visitor reads after signing up; empty for the default. It is the same whether or not the
    /// address was already on the list.
    /// </summary>
    public string SuccessMessage { get; set; }
}
