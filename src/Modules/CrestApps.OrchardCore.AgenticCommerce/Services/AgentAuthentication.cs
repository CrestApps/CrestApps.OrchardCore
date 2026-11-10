using CrestApps.OrchardCore.AgenticCommerce.Core.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Services;

/// <summary>
/// Which agent platform a request comes from, or why it isn't allowed.
/// </summary>
public sealed class AgentAuthentication
{
    /// <summary>
    /// Gets the platform, when it is allowed.
    /// </summary>
    public AgentPlatform Platform { get; init; }

    /// <summary>
    /// Gets the linked buyer the request acts for, or <see langword="null"/>.
    /// </summary>
    public string UserId { get; init; }

    /// <summary>
    /// Gets the HTTP status of a refusal.
    /// </summary>
    public int StatusCode { get; init; }

    /// <summary>
    /// Gets why the request is refused, or <see langword="null"/> when there were no credentials at all.
    /// </summary>
    public string Error { get; init; }

    /// <summary>
    /// Gets a value indicating whether the request is allowed.
    /// </summary>
    public bool Succeeded
        => Platform is not null;

    /// <summary>
    /// Returns a refusal.
    /// </summary>
    /// <param name="statusCode">The HTTP status.</param>
    /// <param name="error">Why.</param>
    public static AgentAuthentication Fail(int statusCode, string error)
        => new()
        {
            StatusCode = statusCode,
            Error = error,
        };
}
