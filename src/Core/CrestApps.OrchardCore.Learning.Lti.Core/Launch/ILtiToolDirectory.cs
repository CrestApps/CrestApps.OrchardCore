using CrestApps.OrchardCore.Learning.Lti.Models;
using Microsoft.IdentityModel.Tokens;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Launch;

/// <summary>
/// Finds registered tools and what their OpenID application says about them: the redirect URIs and the key set.
/// </summary>
public interface ILtiToolDirectory
{
    /// <summary>
    /// Returns the tool of a client id, when both the tool and its OpenID application exist.
    /// </summary>
    /// <param name="clientId">The client id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The tool, or <see langword="null"/>.</returns>
    Task<ExternalTool> FindByClientIdAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a tool by its id.
    /// </summary>
    /// <param name="toolId">The tool id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The tool, or <see langword="null"/>.</returns>
    Task<ExternalTool> FindByIdAsync(string toolId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether a redirect URI is one of the redirect URIs of the tool's OpenID application, compared exactly.
    /// </summary>
    /// <param name="clientId">The client id.</param>
    /// <param name="redirectUri">The redirect URI.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when it is registered.</returns>
    Task<bool> IsRedirectUriAllowedAsync(string clientId, string redirectUri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the keys the tool signs with: from its JWKS URL when it has one (cached, read again when asked), else the
    /// key set of its OpenID application.
    /// </summary>
    /// <param name="tool">The tool.</param>
    /// <param name="refresh">Whether to read the JWKS URL again, as when a token names a key the cache does not hold.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The keys; empty when there are none.</returns>
    Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(ExternalTool tool, bool refresh = false, CancellationToken cancellationToken = default);
}
