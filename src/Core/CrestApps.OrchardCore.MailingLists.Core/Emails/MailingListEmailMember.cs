namespace CrestApps.OrchardCore.MailingLists.Core.Emails;

/// <summary>
/// The person an e-mail goes to, as the site's template reads them: <c>{{ Member.Name }}</c> and <c>{{ Member.Email }}</c>.
/// </summary>
public sealed class MailingListEmailMember
{
    /// <summary>
    /// Gets or sets the person's name, when they gave it.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the address the e-mail goes to.
    /// </summary>
    public string Email { get; set; }
}
