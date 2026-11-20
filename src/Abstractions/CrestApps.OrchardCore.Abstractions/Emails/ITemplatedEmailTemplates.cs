namespace CrestApps.OrchardCore.Emails;

/// <summary>
/// The site's own templates of its emails: each kind's subject and body can be written over in Liquid, with the email
/// the site would send as <c>{{ Content }}</c>, and a layout goes around them all. Without one, an email goes as built.
/// Senders other than <see cref="ITemplatedEmailSender"/>, such as one that reaches someone through notifications, pass
/// their message through it too.
/// </summary>
public interface ITemplatedEmailTemplates
{
    /// <summary>
    /// Returns the email to send: the built one, or the site's template of it. A template that fails to render gives the
    /// built email, so a typo never stops an email.
    /// </summary>
    /// <param name="context">The email as it was built, and what its template can use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<TemplatedEmailMessage> ApplyAsync(TemplatedEmailContext context, CancellationToken cancellationToken = default);
}
