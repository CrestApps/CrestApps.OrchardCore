using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// An agent's request to consult a destination before handing a live call over.
/// </summary>
public sealed class WarmTransferRequest
{
    /// <summary>
    /// Gets or sets the interaction the agent is on.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the requesting user's identifier.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the requesting principal, used for permission checks on external destinations.
    /// </summary>
    public ClaimsPrincipal Principal { get; set; }

    /// <summary>
    /// Gets or sets the kind of destination: an agent or an external number.
    /// </summary>
    public InteractionTransferTargetType TargetType { get; set; }

    /// <summary>
    /// Gets or sets the destination: an agent profile id, an approved destination id, or a typed number when the
    /// tenant allows unlisted numbers.
    /// </summary>
    public string TargetId { get; set; }
}

/// <summary>
/// An agent's command on a consult they started: complete it, cancel it, or ask how it is going.
/// </summary>
public sealed class WarmTransferCommand
{
    /// <summary>
    /// Gets or sets the interaction the agent is on.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the requesting user's identifier.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the requesting principal.
    /// </summary>
    public ClaimsPrincipal Principal { get; set; }

    /// <summary>
    /// Gets or sets the consult, or <see langword="null"/> for the agent's most recent consult on the call.
    /// </summary>
    public string ConsultId { get; set; }
}

/// <summary>
/// The outcome of a warm-transfer command and where the consult stands.
/// </summary>
public sealed class WarmTransferResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the command did what was asked.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets why the command failed, or what happened.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the consult.
    /// </summary>
    public string ConsultId { get; set; }

    /// <summary>
    /// Gets or sets the consult's state.
    /// </summary>
    public ConsultCallStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the kind of destination consulted.
    /// </summary>
    public InteractionTransferTargetType? TargetType { get; set; }

    /// <summary>
    /// Gets or sets the destination consulted.
    /// </summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the consult is still going: ringing or connected.
    /// </summary>
    public bool IsLive => Status is ConsultCallStatus.Initiated or ConsultCallStatus.Ringing or ConsultCallStatus.Connected;

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="reason">Why it failed.</param>
    /// <returns>The result.</returns>
    public static WarmTransferResult Failure(string reason)
        => new() { Succeeded = false, Reason = reason };

    /// <summary>
    /// Creates a result describing a consult.
    /// </summary>
    /// <param name="consult">The consult.</param>
    /// <param name="reason">What happened.</param>
    /// <returns>The result.</returns>
    public static WarmTransferResult For(ConsultCall consult, string reason = null)
        => new()
        {
            Succeeded = true,
            Reason = reason,
            ConsultId = consult?.ConsultId,
            Status = consult?.Status,
            TargetType = consult?.TargetType,
            TargetId = consult?.TargetId,
        };
}

/// <summary>
/// What a provider does after a consult leg ends, as the Contact Center decided it.
/// </summary>
public enum ConsultLegEndedOutcome
{
    /// <summary>The leg's end changed nothing: the consult was already over, or is not known here.</summary>
    Ignored,

    /// <summary>The destination left before the handover; the customer was returned to the consulting agent.</summary>
    ReturnedToAgent,

    /// <summary>The destination ended the call after taking it over; the call is over and the customer released.</summary>
    CallEnded,

    /// <summary>The external party the call was handed to hung up; the provider still has to release the customer.</summary>
    ReleaseCustomer,
}
