namespace CrestApps.OrchardCore.MailingLists.ViewModels;

/// <summary>
/// The General card of the mailing list editor.
/// </summary>
public class MailingListGeneralViewModel
{
    /// <summary>
    /// Gets or sets the technical name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the name people see.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets what the list sends and how often.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the consent text shown on the sign-up form.
    /// </summary>
    public string ConsentText { get; set; }

    /// <summary>
    /// Gets or sets whether the list is offered to people.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether a welcome e-mail is sent.
    /// </summary>
    public bool SendWelcomeEmail { get; set; }
}
