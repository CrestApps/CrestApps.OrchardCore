namespace CrestApps.OrchardCore.Asterisk.Models;

/// <summary>
/// Durable per-tenant ownership binding for an Asterisk channel. The document is stored in the current
/// tenant's YesSql database, so tenant attribution is structural and does not require a tenant column.
/// </summary>
public sealed class AsteriskChannelTenantBinding
{
    /// <summary>
    /// Gets or sets the Asterisk channel identifier used as the natural key for the binding.
    /// </summary>
    public string ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the configured provider name that owns the channel.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the contact-center interaction identifier associated with the channel.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the provider call identifier associated with the channel.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the mixing bridge that joins this channel to its peer. Only the agent leg
    /// of a connected caller-to-agent call records a bridge, so its presence marks the leg that owns teardown of
    /// the shared bridge.
    /// </summary>
    public string BridgeId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the peer channel this channel is bridged to. It lets either leg's terminal
    /// event release the whole call without a full-table scan.
    /// </summary>
    public string PeerChannelId { get; set; }

    /// <summary>
    /// Gets or sets the lifecycle phase of the binding. The agent leg of a caller-to-agent connect is
    /// <see cref="AsteriskChannelBindingState.Pending"/> during the window between persisting the binding and
    /// joining both legs to the live bridge, then transitions to <see cref="AsteriskChannelBindingState.Connected"/>;
    /// every other binding is <see cref="AsteriskChannelBindingState.Connected"/>.
    /// </summary>
    public AsteriskChannelBindingState State { get; set; }

    /// <summary>
    /// Gets or sets the binding's lifecycle phase captured durably at the instant it was claimed for teardown
    /// (transitioned to <see cref="AsteriskChannelBindingState.Terminating"/>). It is <see langword="null"/> until
    /// the binding is claimed. Persisting the pre-claim phase — rather than keeping it only in the transient claim —
    /// lets the reconciler recover a crashed teardown correctly: a leg claimed while still
    /// <see cref="AsteriskChannelBindingState.Pending"/> was owned by the connect flow (which reparks the caller for
    /// re-offer), so its recovery must never end the caller, whereas a leg claimed while
    /// <see cref="AsteriskChannelBindingState.Connected"/> was a live call whose peer must be released.
    /// </summary>
    public AsteriskChannelBindingState? PreTeardownState { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the connect flow has already detached the caller from its holding
    /// bridge to join it to the agent. It is persisted durably just before the detach so recovery can tell a
    /// still-parked caller (a connect that never reached bridging — leave it parked) from a caller that was pulled
    /// out of holding by a connect that then failed or crashed (it must be re-parked, or it is left alive with no
    /// bridge). A <see cref="AsteriskChannelBindingState.Pending"/> agent leg whose recovery finds this set must
    /// return the caller to holding before retiring the record, rather than stranding it in silence.
    /// </summary>
    public bool CallerDetached { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the binding was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
