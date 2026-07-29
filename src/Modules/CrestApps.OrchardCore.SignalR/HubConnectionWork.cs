namespace CrestApps.OrchardCore.SignalR;

/// <summary>
/// Names the cancellation convention hub methods follow, so the choice of token is a decision a reader can see
/// rather than an omission they have to infer.
/// <para>
/// Hub work falls into two classes. Work whose only product is a value returned to the calling connection may
/// honour <c>Context.ConnectionAborted</c>, because if the caller is gone the answer has nowhere to go and
/// abandoning it is correct. Work that changes durable state or SignalR group membership must not: abandoning it
/// part-way leaves the durable record and the connection's group membership disagreeing, and there is no
/// mechanism that later repairs that. An agent can be durably signed into a queue while its connection was never
/// added to that queue's group, in which case the agent stays connected, appears available, and silently receives
/// none of that queue's events until it reconnects.
/// </para>
/// <para>
/// A connection's own token is a particularly poor fit for that second class, because the moment it is most
/// likely to trip — a flaky or reconnecting client — is exactly the moment a half-applied membership change does
/// the most damage.
/// </para>
/// </summary>
public static class HubConnectionWork
{
    /// <summary>
    /// Gets the token used for hub work that must run to completion once it has started, because abandoning it
    /// part-way would leave durable state and SignalR group membership inconsistent. This is deliberately a token
    /// that is never cancelled.
    /// </summary>
    public static CancellationToken MustComplete => CancellationToken.None;
}
