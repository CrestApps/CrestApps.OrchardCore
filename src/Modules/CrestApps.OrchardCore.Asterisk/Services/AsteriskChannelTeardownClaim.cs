using CrestApps.OrchardCore.Asterisk.Models;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Represents the successful, durable claim of an Asterisk channel binding for teardown. The claim is granted by
/// a single committed state transition to <see cref="AsteriskChannelBindingState.Terminating"/>, so exactly one
/// caller can ever own the teardown of a given binding; a concurrent connect finalization observes the binding as
/// no longer promotable and compensates instead. The pre-claim state is captured so the teardown can decide the
/// caller's disposition (a pending agent leg is still owned by the connect flow, a connected one is not).
/// </summary>
internal sealed class AsteriskChannelTeardownClaim
{
    /// <summary>
    /// Gets the binding that was claimed for teardown, with its state already transitioned to
    /// <see cref="AsteriskChannelBindingState.Terminating"/>.
    /// </summary>
    public AsteriskChannelTenantBinding Binding { get; init; }

    /// <summary>
    /// Gets the binding's lifecycle state immediately before it was claimed for teardown. This distinguishes a
    /// pending agent leg (whose caller the connect flow still owns) from a connected leg (whose caller the
    /// teardown must release).
    /// </summary>
    public AsteriskChannelBindingState PreviousState { get; init; }
}
