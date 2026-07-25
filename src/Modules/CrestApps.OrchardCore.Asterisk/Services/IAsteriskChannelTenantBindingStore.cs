using CrestApps.OrchardCore.Asterisk.Models;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Provides tenant-scoped persistence for Asterisk channel ownership bindings.
/// </summary>
internal interface IAsteriskChannelTenantBindingStore
{
    /// <summary>
    /// Gets all Asterisk channel ownership bindings in the current tenant store.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The tenant-scoped channel ownership bindings.</returns>
    Task<IReadOnlyCollection<AsteriskChannelTenantBinding>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the current tenant store holds any Asterisk channel ownership binding. Used to guard
    /// settings changes that would abandon the tenant's current ARI identity (its base URL or Stasis application)
    /// while live calls are still tracked against it.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when at least one binding exists; otherwise <see langword="false"/>.</returns>
    Task<bool> HasAnyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the binding for the supplied Asterisk channel identifier in the current tenant store.
    /// </summary>
    /// <param name="channelId">The Asterisk channel identifier to find.</param>
    /// <returns>The matching binding, or <see langword="null"/> when the channel is not owned by the current tenant.</returns>
    Task<AsteriskChannelTenantBinding> FindByChannelIdAsync(string channelId);

    /// <summary>
    /// Finds every binding whose peer channel matches the supplied Asterisk channel identifier in the current
    /// tenant store. A re-offer after a failed connect can leave more than one binding referencing the same caller
    /// (a stale <see cref="AsteriskChannelBindingState.Terminating"/> agent generation and the live one), so a
    /// terminal event or the reconciler must be able to consider EVERY generation to release the whole call rather
    /// than an arbitrary single match.
    /// </summary>
    /// <param name="peerChannelId">The peer Asterisk channel identifier to find the owning bindings for.</param>
    /// <returns>All bindings referencing the peer channel; an empty collection when none reference it.</returns>
    Task<IReadOnlyCollection<AsteriskChannelTenantBinding>> FindAllByPeerChannelIdAsync(string peerChannelId);

    /// <summary>
    /// Atomically creates the supplied channel binding when no binding for its channel exists yet in the current
    /// tenant store, returning whether THIS call created it. The create is serialized per channel so a duplicate
    /// delivery of the same channel — for example, two overlapping shell-reload listener generations handling the
    /// same StasisStart — claims the channel exactly once and only the winning caller performs the channel's inbound
    /// side effects.
    /// </summary>
    /// <param name="binding">The binding to create.</param>
    /// <returns>
    /// <see langword="true"/> when this call created the binding; <see langword="false"/> when a binding for the
    /// channel already existed, which signals the caller lost the claim and must not repeat the channel's inbound
    /// side effects.
    /// </returns>
    Task<bool> CreateAsync(AsteriskChannelTenantBinding binding);

    /// <summary>
    /// Atomically transitions the binding for the supplied channel from
    /// <see cref="AsteriskChannelBindingState.Pending"/> to <see cref="AsteriskChannelBindingState.Connected"/>
    /// using YesSql document-version optimistic concurrency, committing durably in its own isolated tenant
    /// session. It is called once both legs of a caller-to-agent connect have joined the live bridge. The
    /// compare-and-set is the connect flow's half of the linearization with terminal-event teardown: if a
    /// terminal event has already claimed the binding for teardown (moving it out of
    /// <see cref="AsteriskChannelBindingState.Pending"/>), the promotion is rejected so the two sides can never
    /// both win.
    /// </summary>
    /// <param name="channelId">The agent-leg Asterisk channel identifier whose binding should be marked connected.</param>
    /// <returns>
    /// <see langword="true"/> only when the binding was found in <see cref="AsteriskChannelBindingState.Pending"/>
    /// and this call committed the transition to <see cref="AsteriskChannelBindingState.Connected"/>;
    /// <see langword="false"/> when no binding exists or it was no longer pending, which signals that a terminal
    /// event already claimed the pending agent leg and the connect flow must compensate.
    /// </returns>
    Task<bool> MarkConnectedAsync(string channelId);

    /// <summary>
    /// Atomically transitions the inbound caller-leg binding for the supplied channel from
    /// <see cref="AsteriskChannelBindingState.Offering"/> to <see cref="AsteriskChannelBindingState.Connected"/>
    /// using YesSql document-version optimistic concurrency, committing durably in its own isolated tenant
    /// session. It is called once the inbound offer has been routed to a Contact Center interaction. Promoting the
    /// leg out of the provisioning <see cref="AsteriskChannelBindingState.Offering"/> phase makes the reconciler
    /// treat the still-alive caller as a healthy live call rather than an aged, never-routed offer to terminate.
    /// </summary>
    /// <param name="channelId">The inbound caller-leg Asterisk channel identifier whose binding should be promoted.</param>
    /// <returns>
    /// <see langword="true"/> only when the binding was found in <see cref="AsteriskChannelBindingState.Offering"/>
    /// and this call committed the transition to <see cref="AsteriskChannelBindingState.Connected"/>;
    /// <see langword="false"/> when no binding exists or it was no longer offering, which signals that a terminal
    /// event already claimed the offering caller leg.
    /// </returns>
    Task<bool> TryPromoteOfferingAsync(string channelId);

    /// <summary>
    /// Atomically records — using YesSql document-version optimistic concurrency in its own isolated tenant
    /// session — that the connect flow has detached the caller from its holding bridge, persisting the marker
    /// before the actual ARI detach so recovery of a crashed connect can tell a still-parked caller from one that
    /// must be re-parked. Only a still-<see cref="AsteriskChannelBindingState.Pending"/> agent leg is marked; if a
    /// terminal event has already claimed the binding for teardown, the marker is not needed because that teardown
    /// owns the caller's disposition.
    /// </summary>
    /// <param name="channelId">The agent-leg Asterisk channel identifier whose binding should record the detach.</param>
    /// <returns>
    /// <see langword="true"/> when the still-pending binding now durably records the caller detach (or already
    /// did); <see langword="false"/> when no binding exists or it was no longer pending.
    /// </returns>
    Task<bool> MarkCallerDetachedAsync(string channelId);

    /// <summary>
    /// Atomically claims the binding for the supplied channel for teardown by transitioning it to
    /// <see cref="AsteriskChannelBindingState.Terminating"/> using YesSql document-version optimistic
    /// concurrency, committing durably in its own isolated tenant session. This single committed transition is
    /// the teardown's half of the linearization with connect finalization: whichever side commits first owns the
    /// call's disposition, so a terminal event can never tear down a call the connect flow has already promoted,
    /// nor can two terminal events for the same channel both run cleanup. The binding is deliberately left in the
    /// store (as <see cref="AsteriskChannelBindingState.Terminating"/>) rather than removed, so a crash or ARI
    /// failure before cleanup completes leaves a durable record the reconciler can retry.
    /// </summary>
    /// <param name="channelId">The Asterisk channel identifier whose binding should be claimed for teardown.</param>
    /// <returns>
    /// The claim, carrying the claimed binding and its pre-claim state, when this call committed the transition;
    /// <see langword="null"/> when no binding exists, when it was already
    /// <see cref="AsteriskChannelBindingState.Terminating"/> (claimed by another terminal event), or when the
    /// claim lost the optimistic-concurrency race after retrying.
    /// </returns>
    Task<AsteriskChannelTeardownClaim> TryBeginTeardownAsync(string channelId);

    /// <summary>
    /// Atomically completes a transfer handoff in a single isolated, immediately committed tenant session:
    /// promotes the destination leg's binding from <see cref="AsteriskChannelBindingState.Joining"/> to
    /// <see cref="AsteriskChannelBindingState.Connected"/> AND, in the same transaction, retires the previous agent
    /// leg's binding by transitioning it to a non-owning <see cref="AsteriskChannelBindingState.Terminating"/> state
    /// with a <see cref="AsteriskChannelBindingState.Joining"/> pre-teardown disposition (so teardown and the
    /// reconciler hang up ONLY the previous channel, never the shared bridge, and the previous leg keeps a durable
    /// recovery record until its hangup is confirmed). Committing both in one transaction is the linearization point
    /// that guarantees the canonical conversation bridge is owned by EXACTLY ONE
    /// <see cref="AsteriskChannelBindingState.Connected"/> binding at every instant — the previous agent before the
    /// swap, the destination agent after it — so a terminal event can never observe a window with two Connected
    /// owners (a double-teardown drop) or zero owners (an unowned live bridge). BOTH writes use YesSql
    /// document-version optimistic concurrency: the destination promotion commits only while the destination is
    /// still <see cref="AsteriskChannelBindingState.Joining"/> (a terminal event that already claimed it for teardown
    /// fences the swap), and the previous-owner retirement commits only while the previous leg is still the live
    /// <see cref="AsteriskChannelBindingState.Connected"/> owner — a version-checked transition, not an id-only
    /// delete, so that a second concurrent transfer of the same call to a different destination cannot also promote:
    /// only the first swap can move the previous owner out of Connected, and the loser's version-checked write fails,
    /// re-reads, and rejects. If either precondition no longer holds the swap does not commit and the caller safely
    /// stays with whichever agent won ownership.
    /// </summary>
    /// <param name="newAgentChannelId">The destination agent-leg channel identifier to promote to <see cref="AsteriskChannelBindingState.Connected"/>.</param>
    /// <param name="previousAgentChannelId">The previous agent-leg channel identifier whose binding should be retired to a non-owning terminating state as part of the same transaction.</param>
    /// <returns>
    /// <see langword="true"/> only when the destination binding was found in
    /// <see cref="AsteriskChannelBindingState.Joining"/>, the previous agent leg was still the
    /// <see cref="AsteriskChannelBindingState.Connected"/> owner, and this call committed both the destination's
    /// promotion to <see cref="AsteriskChannelBindingState.Connected"/> and the retirement of the previous agent leg;
    /// <see langword="false"/> when the destination binding does not exist or is no longer joining (a terminal
    /// event claimed it), or when the previous agent leg is missing or no longer Connected (another transfer won
    /// ownership), which signals the transfer's finalization lost the race and must not retire the previous leg.
    /// </returns>
    Task<bool> SwapConnectedOwnerAsync(string newAgentChannelId, string previousAgentChannelId);

    /// <summary>
    /// Removes the binding for the supplied Asterisk channel identifier from the current tenant store. Teardown
    /// calls this only after every ARI cleanup effect for the binding has been applied, so the durable
    /// <see cref="AsteriskChannelBindingState.Terminating"/> record is retired only once no orphaned resource can
    /// remain.
    /// </summary>
    /// <param name="channelId">The Asterisk channel identifier to remove.</param>
    /// <returns>A task that completes when the binding has been removed or no matching binding exists.</returns>
    Task RemoveByChannelIdAsync(string channelId);
}
