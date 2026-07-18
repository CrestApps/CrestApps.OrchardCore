namespace CrestApps.OrchardCore.Asterisk.Models;

/// <summary>
/// Describes the lifecycle phase of an Asterisk channel ownership binding. The phase determines whether a
/// terminal event for the agent leg may release the caller peer, closing the window in which a half-connected
/// agent leg could otherwise hang up a caller that the connect flow is still connecting.
/// </summary>
public enum AsteriskChannelBindingState
{
    /// <summary>
    /// The binding's peer relationship is live: both legs share the mixing bridge, so a terminal event for this
    /// leg must release the peer to complete the call. This is the default so caller-leg and pre-existing
    /// bindings keep the original release-on-terminal behavior.
    /// </summary>
    Connected = 0,

    /// <summary>
    /// The agent leg has been originated and its binding persisted, but the caller has not yet been committed to
    /// the shared mixing bridge. The connect flow still owns the caller's disposition (it will bridge the caller
    /// or repark it), so a terminal event for a pending agent leg must not release the caller; it only tears down
    /// the half-built agent bridge.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// A terminal event has durably claimed the binding for teardown. This state is the linearization point that
    /// makes teardown mutually exclusive with the connect flow's pending-to-connected finalization: once a
    /// binding is <see cref="Terminating"/>, a concurrent <c>MarkConnectedAsync</c> can never promote it, and a
    /// second terminal event can never re-claim it. The binding survives in this state until every ARI cleanup
    /// effect has been applied, so a crash or transient ARI failure leaves a durable record the reconciler can
    /// retry rather than an orphaned bridge with no owner.
    /// </summary>
    Terminating = 2,

    /// <summary>
    /// An inbound caller leg has been persisted as a durable recovery record but its offer is not yet complete:
    /// the caller may have been answered and parked, but it has not yet been routed to a Contact Center
    /// interaction. Like <see cref="Pending"/> for the agent leg, this is a provisioning phase the inbound offer
    /// flow still owns, so a terminal event that claims it must not treat the caller as a live connected call, and
    /// the reconciler must not treat a still-alive offering leg as healthy — a crash before routing completes
    /// leaves a durable record the reconciler resolves (terminating an aged, never-routed caller) rather than a
    /// caller stranded in silence with no owning interaction. It is promoted to <see cref="Connected"/> once the
    /// offer has been routed.
    /// </summary>
    Offering = 3,
}
