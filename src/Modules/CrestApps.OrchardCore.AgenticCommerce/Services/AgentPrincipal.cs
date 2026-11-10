using System.Security.Claims;

namespace CrestApps.OrchardCore.AgenticCommerce.Services;

/// <summary>
/// The principal an agent platform's request runs as, so idempotency keys and rate limits are kept per platform.
/// </summary>
public static class AgentPrincipal
{
    /// <summary>
    /// The authentication type of an agent platform's principal.
    /// </summary>
    public const string AuthenticationType = "AgentPlatform";

    /// <summary>
    /// What a platform principal's name starts with.
    /// </summary>
    public const string OwnerPrefix = "agent-platform:";

    /// <summary>
    /// Returns the principal of a platform.
    /// </summary>
    /// <param name="platformId">The platform's id.</param>
    public static ClaimsPrincipal Create(string platformId)
        => new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, OwnerPrefix + platformId),
            ],
            AuthenticationType));
}
