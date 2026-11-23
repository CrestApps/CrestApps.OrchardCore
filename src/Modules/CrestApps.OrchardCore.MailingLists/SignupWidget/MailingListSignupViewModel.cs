namespace CrestApps.OrchardCore.MailingLists.SignupWidget;

/// <summary>
/// The sign-up widget as a visitor sees it.
/// </summary>
public class MailingListSignupViewModel
{
    /// <summary>
    /// Gets or sets the widget's content item id.
    /// </summary>
    public string WidgetId { get; set; }

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
    /// Gets or sets the address to start with, the signed-in user's.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the lists the form offers.
    /// </summary>
    public List<MailingListSignupOption> Lists { get; set; } = [];

    /// <summary>
    /// Gets or sets the message of the last post from this form, when the page was reached by its redirect.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets whether the last post from this form was accepted.
    /// </summary>
    public bool Succeeded { get; set; }
}
