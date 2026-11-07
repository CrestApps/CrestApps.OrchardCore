namespace CrestApps.OrchardCore.Commerce.Emails;

/// <summary>
/// The store's own templates of its emails: each kind's subject and body can be written over in Liquid, with the email
/// the store would send as <c>{{ Content }}</c>, and a layout goes around them all. Without one, an email goes as built.
/// Senders other than <see cref="ICommerceEmailSender"/>, such as one that reaches a buyer through notifications, pass
/// their message through it too.
/// </summary>
public interface ICommerceEmailTemplates
{
    /// <summary>
    /// Returns the email to send: the built one, or the store's template of it. A template that fails to render gives the
    /// built email, so a typo never stops an email.
    /// </summary>
    /// <param name="context">The email as it was built, and what its template can use.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<CommerceEmailMessage> ApplyAsync(CommerceEmailContext context, CancellationToken cancellationToken = default);
}
