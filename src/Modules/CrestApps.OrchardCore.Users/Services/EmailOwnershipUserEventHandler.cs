using System.Security.Claims;
using CrestApps.OrchardCore.Users.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Users.Handlers;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// Notices, as Orchard Core's user store saves an account, two things the Confirm email ownership feature needs:
/// <list type="bullet">
/// <item>a password, an external login or a passkey added, changed or removed from a session an e-mailed code began,
/// while the address isn't proved (<see cref="UserCredentialsProof.ChangedInCodeSessionPropertyName"/>): the owner linked
/// their own login, added their passkey, or removed one, next to credentials somebody else may hold, and Orchard Core
/// then signs the browser in again without the code's mark (ASP.NET Core Identity keeps the security stamp when a
/// passkey is added or removed, so the session's digest counts the passkeys themselves). Every session of that account is told on every page from then on
/// (<see cref="Filters.EmailOwnershipNoticeFilter"/>), including those signed in before the mark, whose claims are
/// refreshed at their next request (<see cref="UserCredentialsResets.PublishClaimsRefreshAsync"/>);</item>
/// <item>an account created in the current request, so an external registration whose provider asserted the same
/// address as verified can record it as proved (<see cref="EmailOwnershipExternalLoginHandler"/>).</item>
/// </list>
/// It reads only the request's own user and the account being saved.
/// </summary>
public sealed class EmailOwnershipUserEventHandler : UserEventHandlerBase
{
    private const string CreatedItemPrefix = "CrestApps.EmailOwnership.Created:";
    private const string MarkedItemPrefix = "CrestApps.EmailOwnership.Marked:";

    // Orchard Core's user store flushes the update before UpdatedAsync and the request commits it a little later. A
    // session refreshed in between would read the account without the mark, so sessions issued until a little after the
    // mark are refreshed too.
    private static readonly TimeSpan _commitMargin = TimeSpan.FromSeconds(30);

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;
    private readonly UserCredentialsResets _credentialsResets;
    private readonly ILogger _logger;

    public EmailOwnershipUserEventHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        UserCredentialsResets credentialsResets,
        ILogger<EmailOwnershipUserEventHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        _credentialsResets = credentialsResets;
        _logger = logger;
    }

    /// <summary>
    /// Whether the account was created in <paramref name="httpContext"/>'s request.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <param name="userId">The account's id.</param>
    public static bool WasCreatedIn(HttpContext httpContext, string userId)
        => httpContext is not null && !string.IsNullOrEmpty(userId) && httpContext.Items.ContainsKey(CreatedItemPrefix + userId);

    /// <inheritdoc/>
    public override Task CreatedAsync(UserCreateContext context)
    {
        if (context?.User is User { UserId: { Length: > 0 } userId } && _httpContextAccessor.HttpContext is { } httpContext)
        {
            httpContext.Items[CreatedItemPrefix + userId] = true;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UserUpdateContext context)
    {
        if (context?.User is User account &&
            !UserCredentialsProof.ChangedInCodeSession(account) &&
            UserCredentialsProof.HasCredentialsInDoubt(account) &&
            _httpContextAccessor.HttpContext?.User is { } principal &&
            UserCredentialsProof.SignedInWithEmailedCode(principal) &&
            string.Equals(principal.FindFirstValue(ClaimTypes.NameIdentifier), account.UserId, StringComparison.Ordinal) &&
            CredentialsChanged(principal, account))
        {
            UserCredentialsProof.MarkChangedInCodeSession(account, _clock.UtcNow);
            _httpContextAccessor.HttpContext.Items[MarkedItemPrefix + account.UserId] = true;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A6 (review 10): a session signed in before the mark carries no claim saying so, and the notice filter reads the
    /// account only for one that does. So once the mark is saved, every earlier session of the account has its claims
    /// built again at its next request and stays signed in, the code session too. A failure is logged: those sessions
    /// then get the claim at Identity's next stamp check, up to 30 minutes later.
    /// </remarks>
    public override async Task UpdatedAsync(UserUpdateContext context)
    {
        if (context?.User is not User { UserId: { Length: > 0 } userId } ||
            _httpContextAccessor.HttpContext is not { } httpContext ||
            !httpContext.Items.Remove(MarkedItemPrefix + userId))
        {
            return;
        }

        try
        {
            await _credentialsResets.PublishClaimsRefreshAsync(userId, _clock.UtcNow + _commitMargin, httpContext.RequestAborted);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "The sessions of a user whose sign-ins changed in a code session could not be refreshed, so they are warned from their next security stamp check.");
        }
    }

    // What the code session saw at sign-in, against the account as it is saved now. A session without it (signed in
    // before the upgrade, or refreshed by RefreshSignInAsync, which keeps only how the session began) counts any change
    // of the account as one.
    private static bool CredentialsChanged(ClaimsPrincipal principal, User account)
        => principal.FindFirstValue(UserCredentialsProof.CodeSessionCredentialsClaim) is not { Length: > 0 } atSignIn ||
            !string.Equals(atSignIn, UserCredentialsProof.CredentialsFingerprint(account), StringComparison.Ordinal);
}
