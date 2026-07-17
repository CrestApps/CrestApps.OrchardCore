using System.Security.Claims;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Describes a call-control authorization decision request.
/// </summary>
public sealed class CallControlAuthorizationContext
{
    /// <summary>
    /// Gets or sets the authenticated principal invoking the command.
    /// </summary>
    public ClaimsPrincipal Principal { get; set; }

    /// <summary>
    /// Gets or sets the Orchard user identifier invoking the command.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the verb being authorized.
    /// </summary>
    public CallControlVerb Verb { get; set; }

    /// <summary>
    /// Gets or sets the logical Contact Center interaction identifier supplied by the caller.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the optional provider name that must match the resolved call session.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the optional provider call identifier that must match the resolved call session.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the command is a supervisor operation.
    /// </summary>
    public bool SupervisorOperation { get; set; }
}
