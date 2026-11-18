using System.Security.Claims;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Handlers;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// Records the account's address as proved (<see cref="UserEmailOwnership"/>) when an external sign-in, registration or
/// link shows its provider verified that very address: the provider's claims carry the address, matching the account's
/// own, and a verified flag set to true (<c>email_verified</c>, as OpenID Connect providers send it, or
/// <c>verified_email</c>). A genuine passwordless account is then proved at its next sign-in, so a code never takes it
/// back (<see cref="EmailSignInEligibility"/>), while an address somebody typed at registration, which their own provider
/// didn't assert, stays unproved.
/// </summary>
/// <remarks>
/// <para>
/// The proof keeps whatever the account has, so it is recorded only when nothing else could sign in to it: the account
/// was created in this request (its password and the login were set together by whoever holds the provider's account),
/// or it has no password and this login is its only one (matched by the provider's subject, the login's key). An
/// account with a password or other logins set earlier keeps waiting for "Confirm it's you".
/// </para>
/// <para>
/// A provider that sends no verified flag (ASP.NET Core's Google, Microsoft Account, Facebook and GitHub handlers map
/// none by default, and Microsoft Entra ID doesn't send one) proves nothing here. Orchard Core runs this on every external
/// sign-in (<c>ExternalAuthenticationsController.ExternalSignInAsync</c>), before the session is signed in, and saves the
/// properties it changes.
/// </para>
/// </remarks>
public sealed class EmailOwnershipExternalLoginHandler : IExternalLoginEventHandler
{
    private static readonly string[] _emailClaimTypes = [ClaimTypes.Email, "email"];
    private static readonly string[] _verifiedClaimTypes = ["email_verified", "verified_email"];

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    public EmailOwnershipExternalLoginHandler(IHttpContextAccessor httpContextAccessor, IClock clock)
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
    }

    /// <inheritdoc/>
    public Task<string> GenerateUserName(string provider, IEnumerable<SerializableClaim> claims)
        => Task.FromResult<string>(null);

    /// <inheritdoc/>
    public Task UpdateUserAsync(UpdateUserContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.User is not User account ||
            string.IsNullOrWhiteSpace(account.Email) ||
            UserEmailOwnership.IsVerified(account.Properties, account.Email))
        {
            return Task.CompletedTask;
        }

        var claims = context.ExternalClaims?.ToArray() ?? [];

        if (!AssertsVerified(claims, account.Email) ||
            !NothingElseSignsIn(account, context.LoginProvider, Find(claims, ClaimTypes.NameIdentifier) ?? Find(claims, "sub")))
        {
            return Task.CompletedTask;
        }

        context.PropertiesToUpdate ??= new JsonObject();
        UserEmailOwnership.MarkVerified(context.PropertiesToUpdate, account.Email, _clock.UtcNow);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Whether the provider's claims carry <paramref name="email"/> and say the provider verified it.
    /// </summary>
    /// <param name="claims">The provider's claims.</param>
    /// <param name="email">The account's address.</param>
    public static bool AssertsVerified(IReadOnlyCollection<SerializableClaim> claims, string email)
    {
        var asserted = _emailClaimTypes.Select(type => Find(claims, type)).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var verified = _verifiedClaimTypes.Select(type => Find(claims, type)).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return asserted is not null &&
            string.Equals(asserted.Trim().ToUpperInvariant(), email?.Trim().ToUpperInvariant(), StringComparison.Ordinal) &&
            string.Equals(verified?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
    }

    private bool NothingElseSignsIn(User account, string loginProvider, string providerKey)
    {
        var logins = account.LoginInfos ?? [];

        if (logins.Count != 1 ||
            !string.Equals(logins[0].LoginProvider, loginProvider, StringComparison.Ordinal) ||
            !string.Equals(logins[0].ProviderKey, providerKey, StringComparison.Ordinal))
        {
            return false;
        }

        return string.IsNullOrEmpty(account.PasswordHash) || EmailOwnershipUserEventHandler.WasCreatedIn(_httpContextAccessor.HttpContext, account.UserId);
    }

    private static string Find(IEnumerable<SerializableClaim> claims, string type)
        => claims.FirstOrDefault(claim => string.Equals(claim.Type, type, StringComparison.Ordinal))?.Value;
}
