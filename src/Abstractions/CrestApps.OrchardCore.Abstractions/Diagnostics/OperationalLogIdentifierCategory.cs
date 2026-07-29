namespace CrestApps.OrchardCore.Diagnostics;

/// <summary>
/// Provides the well-known identifier categories used with <see cref="OperationalLogRedactor.Pseudonymize(string, string)"/>
/// so the same raw identifier produces the same process-local log pseudonym, letting operators correlate related
/// log lines within one application process without exposing the raw identifier or a brute-forceable unkeyed digest.
/// </summary>
public static class OperationalLogIdentifierCategory
{
    /// <summary>The category for an Orchard Core user identifier.</summary>
    public const string User = "user";

    /// <summary>The category for a Contact Center agent identifier.</summary>
    public const string Agent = "agent";

    /// <summary>The category for a SignalR connection or agent live-session identifier.</summary>
    public const string Session = "session";

    /// <summary>The category for a telephony provider call identifier.</summary>
    public const string Call = "call";

    /// <summary>The category for a communication-history interaction identifier.</summary>
    public const string Interaction = "interaction";

    /// <summary>The category for an Omnichannel activity identifier.</summary>
    public const string Activity = "activity";

    /// <summary>The category for a Contact Center queue-item reservation identifier.</summary>
    public const string Reservation = "reservation";

    /// <summary>The category for a Contact Center queue identifier.</summary>
    public const string Queue = "queue";

    /// <summary>The category for an idempotency key or outbox message/event identifier.</summary>
    public const string Event = "event";

    /// <summary>The category used for otherwise-unclassified metadata values.</summary>
    public const string Metadata = "metadata";
}
