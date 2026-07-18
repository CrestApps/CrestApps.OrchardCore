namespace CrestApps.OrchardCore.Asterisk.Models;

/// <summary>
/// Provides helpers for reasoning about <see cref="AsteriskChannelBindingState"/> lifecycle phases.
/// </summary>
internal static class AsteriskChannelBindingStateExtensions
{
    /// <summary>
    /// Determines whether the supplied state is a provisioning phase whose durable record an allocator (the
    /// caller-to-agent connect flow for <see cref="AsteriskChannelBindingState.Pending"/>, or the inbound offer
    /// flow for <see cref="AsteriskChannelBindingState.Offering"/>) may still be actively completing. A terminal
    /// event that claims a provisioning binding must leave the durable record in place for the reconciler to
    /// finish and age-retire, because the allocator may still create deterministic resources or owe caller
    /// re-parking; retiring the record immediately would leave a later-created resource — or a detached caller —
    /// with no record to drive its recovery.
    /// </summary>
    /// <param name="state">The binding lifecycle state to evaluate.</param>
    /// <returns>
    /// <see langword="true"/> when the state is <see cref="AsteriskChannelBindingState.Pending"/> or
    /// <see cref="AsteriskChannelBindingState.Offering"/>; otherwise <see langword="false"/>.
    /// </returns>
    public static bool IsProvisioning(this AsteriskChannelBindingState state)
    {
        return state == AsteriskChannelBindingState.Pending ||
            state == AsteriskChannelBindingState.Offering;
    }
}
