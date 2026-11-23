using CrestApps.OrchardCore.MailingLists.Models;

namespace CrestApps.OrchardCore.MailingLists.Services;

/// <summary>
/// Reacts to the life of a mailing list member (the welcome coupon, workflows and provider sync are observers).
/// Observers run inside the member service's unit of work, after the member is saved and before it is committed.
/// Register one with <c>services.AddMailingListMemberObserver&lt;T&gt;()</c>.
/// </summary>
public interface IMailingListMemberObserver
{
    /// <summary>
    /// Called when an address signs up, whatever the next state (pending or subscribed).
    /// </summary>
    /// <param name="context">The member, its list and what led to the change.</param>
    Task SignedUpAsync(MailingListMemberContext context);

    /// <summary>
    /// Called when a member becomes subscribed.
    /// </summary>
    /// <param name="context">The member, its list and what led to the change.</param>
    Task SubscribedAsync(MailingListMemberContext context);

    /// <summary>
    /// Called when a member's address is verified (a link, a confirmed account e-mail or the provider's confirmation).
    /// </summary>
    /// <param name="context">The member, its list and what led to the change.</param>
    Task VerifiedAsync(MailingListMemberContext context);

    /// <summary>
    /// Called when a member is unsubscribed or cleaned.
    /// </summary>
    /// <param name="context">The member, its list and what led to the change.</param>
    Task UnsubscribedAsync(MailingListMemberContext context);

    /// <summary>
    /// Called when a member is erased for a privacy request.
    /// </summary>
    /// <param name="context">The member, its list and what led to the change.</param>
    Task ErasedAsync(MailingListMemberContext context);
}

/// <summary>
/// A base class for observers that react to some member events only.
/// </summary>
public abstract class MailingListMemberObserverBase : IMailingListMemberObserver
{
    /// <inheritdoc/>
    public virtual Task SignedUpAsync(MailingListMemberContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task SubscribedAsync(MailingListMemberContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task VerifiedAsync(MailingListMemberContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task UnsubscribedAsync(MailingListMemberContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public virtual Task ErasedAsync(MailingListMemberContext context)
        => Task.CompletedTask;
}

/// <summary>
/// What a member observer is told.
/// </summary>
public sealed class MailingListMemberContext
{
    /// <summary>
    /// Gets the member as it is now.
    /// </summary>
    public MailingListMember Member { get; init; }

    /// <summary>
    /// Gets the member's list.
    /// </summary>
    public MailingList List { get; init; }

    /// <summary>
    /// Gets the member's state before the change.
    /// </summary>
    public MailingListMemberStatus? PreviousStatus { get; init; }

    /// <summary>
    /// Gets who or what made the change (see <see cref="MailingListMemberSources"/>, or <c>UnsubscribeLink</c>, <c>Consent</c>).
    /// </summary>
    public string Source { get; init; }

    /// <summary>
    /// Gets whether e-mails and the welcome coupon are skipped (imports and admin additions).
    /// </summary>
    public bool Silent { get; init; }

    /// <summary>
    /// Gets or sets the welcome coupon an observer issued, so the page and the e-mails can show it.
    /// </summary>
    public MailingListIssuedCoupon IssuedCoupon { get; set; }

    /// <summary>
    /// Gets or sets whether an observer holds something back until the address is verified (such as a welcome coupon
    /// given after verification on a list without confirmation). The member service then sends the subscribed member
    /// a link that verifies the address instead of the plain welcome e-mail.
    /// </summary>
    public bool RequestsVerification { get; set; }

    /// <summary>
    /// Gets or sets whether an observer changed something the list's provider must receive, such as a welcome coupon
    /// code that the provider's own welcome e-mail sends. A change the provider reported is otherwise not pushed back to
    /// it, so the member service then queues an upsert for the provider in the same unit of work. A sign-up or a
    /// verification on the site queues its upsert anyway, and the push sends the member as it is once committed.
    /// </summary>
    public bool RequestsProviderPush { get; set; }
}
