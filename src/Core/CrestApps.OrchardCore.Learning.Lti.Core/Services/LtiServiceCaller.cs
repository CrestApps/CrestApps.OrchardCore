using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Lti.Models;
using OpenIddict.Abstractions;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Services;

/// <summary>
/// The tool behind a service request: its access token, validated by Orchard Core's OpenID token validation, and the
/// external tool of the token's client.
/// </summary>
public sealed class LtiServiceCaller
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LtiServiceCaller"/> class.
    /// </summary>
    /// <param name="tool">The tool.</param>
    /// <param name="principal">The access token's principal.</param>
    public LtiServiceCaller(ExternalTool tool, ClaimsPrincipal principal)
    {
        Tool = tool;
        Principal = principal;
    }

    /// <summary>
    /// Gets the tool.
    /// </summary>
    public ExternalTool Tool { get; }

    /// <summary>
    /// Gets the access token's principal.
    /// </summary>
    public ClaimsPrincipal Principal { get; }

    /// <summary>
    /// Gets whether the access token was granted a scope.
    /// </summary>
    /// <param name="scope">The scope.</param>
    /// <returns><see langword="true"/> when the token has the scope.</returns>
    public bool HasScope(string scope)
        => Principal.HasScope(scope);
}
