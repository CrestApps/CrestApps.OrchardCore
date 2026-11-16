namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// Ends what lets an account in other than its password, external logins and sign-in cookie, such as the access and
/// refresh tokens an OpenID Connect server issued to it. Called after the account's earlier credentials were reset.
/// </summary>
public interface IUserSignInRevocation
{
    /// <summary>
    /// Revokes what was issued to the account.
    /// </summary>
    /// <param name="userId">The account's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task RevokeAsync(string userId, CancellationToken cancellationToken = default);
}
