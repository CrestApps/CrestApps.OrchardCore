using CrestApps.Core.Support;
using CrestApps.OrchardCore.MailingLists.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// Tells every member observer about a change, one after the other. An observer that fails is logged and the others
/// still run, so a failing integration (a workflow, the welcome coupon) never loses a sign-up.
/// </summary>
public sealed class MailingListMemberObserverRunner
{
    private readonly IEnumerable<IMailingListMemberObserver> _observers;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListMemberObserverRunner"/> class.
    /// </summary>
    /// <param name="observers">The registered observers.</param>
    /// <param name="logger">The logger.</param>
    public MailingListMemberObserverRunner(
        IEnumerable<IMailingListMemberObserver> observers,
        ILogger<MailingListMemberObserverRunner> logger)
    {
        _observers = observers;
        _logger = logger;
    }

    /// <summary>
    /// Tells the observers an address signed up.
    /// </summary>
    /// <param name="context">The change.</param>
    /// <returns>A task that completes when every observer ran.</returns>
    public Task SignedUpAsync(MailingListMemberContext context)
        => RunAsync(context, nameof(IMailingListMemberObserver.SignedUpAsync), (observer, value) => observer.SignedUpAsync(value));

    /// <summary>
    /// Tells the observers a member became subscribed.
    /// </summary>
    /// <param name="context">The change.</param>
    /// <returns>A task that completes when every observer ran.</returns>
    public Task SubscribedAsync(MailingListMemberContext context)
        => RunAsync(context, nameof(IMailingListMemberObserver.SubscribedAsync), (observer, value) => observer.SubscribedAsync(value));

    /// <summary>
    /// Tells the observers a member's address was verified.
    /// </summary>
    /// <param name="context">The change.</param>
    /// <returns>A task that completes when every observer ran.</returns>
    public Task VerifiedAsync(MailingListMemberContext context)
        => RunAsync(context, nameof(IMailingListMemberObserver.VerifiedAsync), (observer, value) => observer.VerifiedAsync(value));

    /// <summary>
    /// Tells the observers a member was unsubscribed or cleaned.
    /// </summary>
    /// <param name="context">The change.</param>
    /// <returns>A task that completes when every observer ran.</returns>
    public Task UnsubscribedAsync(MailingListMemberContext context)
        => RunAsync(context, nameof(IMailingListMemberObserver.UnsubscribedAsync), (observer, value) => observer.UnsubscribedAsync(value));

    /// <summary>
    /// Tells the observers a member was erased.
    /// </summary>
    /// <param name="context">The change.</param>
    /// <returns>A task that completes when every observer ran.</returns>
    public Task ErasedAsync(MailingListMemberContext context)
        => RunAsync(context, nameof(IMailingListMemberObserver.ErasedAsync), (observer, value) => observer.ErasedAsync(value));

    private async Task RunAsync(MailingListMemberContext context, string eventName, Func<IMailingListMemberObserver, MailingListMemberContext, Task> notify)
    {
        foreach (var observer in _observers)
        {
            try
            {
                await notify(observer, context);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "The mailing list member observer {Observer} failed on {Event} for member {MemberId}.", observer.GetType().Name, eventName, context.Member?.ItemId.SanitizeLogValue());
            }
        }
    }
}
