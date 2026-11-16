using OpenIddict.Abstractions;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// Revokes the OpenID Connect authorizations and tokens Orchard Core's OpenID server issued to an account, whose subject
/// is the account's id. Registered only when the OpenID server is on, so the Users module does not depend on it. A
/// revoked refresh token can't be exchanged; an access token already issued stays valid until it expires, unless token
/// validation checks the token's entry.
/// </summary>
public sealed class OpenIdUserSignInRevocation : IUserSignInRevocation
{
    private readonly IOpenIddictAuthorizationManager _authorizationManager;
    private readonly IOpenIddictTokenManager _tokenManager;

    public OpenIdUserSignInRevocation(IOpenIddictAuthorizationManager authorizationManager, IOpenIddictTokenManager tokenManager)
    {
        _authorizationManager = authorizationManager;
        _tokenManager = tokenManager;
    }

    /// <inheritdoc/>
    public async Task RevokeAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        await _authorizationManager.RevokeBySubjectAsync(userId, cancellationToken);
        await _tokenManager.RevokeBySubjectAsync(userId, cancellationToken);
    }
}
