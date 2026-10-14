using CrestApps.OrchardCore.Subscriptions.Models;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// Notified after a <see cref="Subscription"/> changes state.
/// </summary>
/// <remarks>
/// This is how a subscription comes to mean something outside billing. Granting a role, provisioning a
/// tenant, sending a dunning email, and revoking access when an agreement lapses are all reactions to a
/// transition, and none of them belong inside the lifecycle rules themselves. A handler that throws is
/// logged and does not roll back the transition: the agreement's state is the fact, and a failed side effect
/// must not undo it.
/// </remarks>
public interface ISubscriptionLifecycleHandler
{
    /// <summary>
    /// Called after a subscription's state has been written.
    /// </summary>
    /// <param name="context">The transition that happened.</param>
    Task ChangedAsync(SubscriptionLifecycleContext context);
}

/// <summary>
/// The transition a <see cref="ISubscriptionLifecycleHandler"/> is told about.
/// </summary>
/// <param name="subscription">The subscription as it now stands.</param>
/// <param name="previousStatus">The status it held before the transition.</param>
/// <param name="source">What asked for the transition, when the caller named it.</param>
public sealed class SubscriptionLifecycleContext(Subscription subscription, SubscriptionStatus previousStatus, string source = null)
{
    /// <summary>
    /// Gets the subscription as it now stands.
    /// </summary>
    public Subscription Subscription { get; } = subscription;

    /// <summary>
    /// Gets the status the subscription held before the transition.
    /// </summary>
    public SubscriptionStatus PreviousStatus { get; } = previousStatus;

    /// <summary>
    /// Gets what asked for the transition, when the caller named it.
    /// </summary>
    /// <remarks>
    /// A handler that pushes a change out to the payment gateway needs this to tell a change the site made
    /// from one the gateway itself reported, so that reporting a cancellation does not send it straight back.
    /// </remarks>
    public string Source { get; } = source;

    /// <summary>
    /// Gets a value indicating whether the transition changed the status, as opposed to only changing dates
    /// or amounts.
    /// </summary>
    public bool StatusChanged
        => PreviousStatus != Subscription.Status;
}

/// <summary>
/// A no-op base for <see cref="ISubscriptionLifecycleHandler"/> so an implementation only overrides what it
/// cares about.
/// </summary>
public abstract class SubscriptionLifecycleHandlerBase : ISubscriptionLifecycleHandler
{
    /// <inheritdoc/>
    public virtual Task ChangedAsync(SubscriptionLifecycleContext context)
        => Task.CompletedTask;
}
