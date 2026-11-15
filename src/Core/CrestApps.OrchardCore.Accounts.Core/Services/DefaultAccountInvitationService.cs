using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Accounts.Core.Indexes;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Users;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Accounts.Core.Services;

/// <summary>
/// Stores account invitations.
/// </summary>
public sealed class AccountInvitationStore : DocumentCatalog<AccountInvitation, AccountInvitationIndex>
{
    public AccountInvitationStore(ISession session)
        : base(session)
    {
        CollectionName = AccountsConstants.InvitationsCollectionName;
    }

    public Task<AccountInvitation> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => Session.Query<AccountInvitation, AccountInvitationIndex>(index => index.TokenHash == tokenHash, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<AccountInvitation>> ListByAccountAsync(string accountId, CancellationToken cancellationToken = default)
        => (await Session.Query<AccountInvitation, AccountInvitationIndex>(index => index.AccountId == accountId, collection: CollectionName).ListAsync(cancellationToken))
            .OrderByDescending(invitation => invitation.InvitedUtc)
            .ToArray();

    public async Task<IReadOnlyList<AccountInvitation>> ListPendingAsync(string accountId, string normalizedEmail, CancellationToken cancellationToken = default)
        => (await Session.Query<AccountInvitation, AccountInvitationIndex>(
                index => index.AccountId == accountId && index.NormalizedEmail == normalizedEmail && index.State == AccountInvitationState.Pending,
                collection: CollectionName).ListAsync(cancellationToken)).ToArray();

    public async Task<IReadOnlyList<AccountInvitation>> ListForPeopleAsync(IReadOnlyCollection<string> normalizedEmails, IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default)
    {
        var emails = normalizedEmails.ToArray();
        var users = userIds.ToArray();

        if (emails.Length == 0 && users.Length == 0)
        {
            return [];
        }

        return (await Session.Query<AccountInvitation, AccountInvitationIndex>(
                index => index.NormalizedEmail.IsIn(emails) || index.AcceptedUserId.IsIn(users),
                collection: CollectionName).ListAsync(cancellationToken)).ToArray();
    }
}

/// <summary>
/// Invites people to accounts by e-mail. The link carries a random token; only its SHA-256 hash is stored, so a copy of
/// the database cannot accept an invitation. Accepting needs a signed-in user whose e-mail address is the one invited
/// and who belongs to no other account.
/// </summary>
public sealed class DefaultAccountInvitationService : IAccountInvitationService
{
    private static readonly TimeSpan _defaultLifetime = TimeSpan.FromDays(14);

    private readonly AccountInvitationStore _invitations;
    private readonly IContentManager _contentManager;
    private readonly IAccountMemberService _members;
    private readonly IServiceProvider _services;
    private readonly ISession _session;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public DefaultAccountInvitationService(
        AccountInvitationStore invitations,
        IContentManager contentManager,
        IAccountMemberService members,
        IServiceProvider services,
        ISession session,
        IClock clock,
        IStringLocalizer<DefaultAccountInvitationService> stringLocalizer)
    {
        _invitations = invitations;
        _contentManager = contentManager;
        _members = members;
        _services = services;
        _session = session;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<AccountInvitationResult> InviteAsync(AccountInvitationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = request.Email?.Trim();

        if (string.IsNullOrEmpty(email) || !email.Contains('@', StringComparison.Ordinal) || email.Length > 254)
        {
            return new AccountInvitationResult { Error = S["Enter a valid e-mail address."] };
        }

        var account = string.IsNullOrEmpty(request.AccountId) ? null : await _contentManager.GetAsync(request.AccountId, VersionOptions.Latest);

        if (account is null || !account.Has<AccountMembersPart>())
        {
            return new AccountInvitationResult { Error = S["The account was not found or has no members."] };
        }

        var members = account.TryGet<AccountMembersPart>(out var membersPart) ? membersPart.Members ?? [] : [];

        if (members.Any(member => string.Equals(member.Email, email, StringComparison.OrdinalIgnoreCase)))
        {
            return new AccountInvitationResult { Error = S["{0} is already a member of the account.", email] };
        }

        var normalized = email.ToUpperInvariant();
        var now = _clock.UtcNow;

        foreach (var earlier in await _invitations.ListPendingAsync(account.ContentItemId, normalized, cancellationToken))
        {
            earlier.State = AccountInvitationState.Revoked;
            await _invitations.UpdateAsync(earlier, cancellationToken);
        }

        var token = NewToken();
        var invitation = new AccountInvitation
        {
            AccountId = account.ContentItemId,
            Email = email,
            Role = request.Role,
            TokenHash = Hash(token),
            State = AccountInvitationState.Pending,
            ExpiresUtc = now + (request.Lifetime ?? _defaultLifetime),
            InvitedBy = request.InvitedBy,
            InvitedUtc = now,
            Source = request.Source,
            SourceId = request.SourceId,
            Message = request.Message?.Trim(),
        };

        await _invitations.CreateAsync(invitation, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        foreach (var observer in _services.GetServices<IAccountInvitationObserver>())
        {
            await observer.InvitedAsync(invitation, token, cancellationToken);
        }

        return new AccountInvitationResult { Invitation = invitation, Token = token };
    }

    /// <inheritdoc/>
    public async Task<AccountInvitation> FindByTokenAsync(string token, CancellationToken cancellationToken = default)
        => string.IsNullOrWhiteSpace(token) ? null : await _invitations.FindByTokenHashAsync(Hash(token.Trim()), cancellationToken);

    /// <inheritdoc/>
    public async Task<AccountInvitationResult> AcceptAsync(string token, string userId, CancellationToken cancellationToken = default)
    {
        var invitation = await FindByTokenAsync(token, cancellationToken);

        if (invitation is null || invitation.State == AccountInvitationState.Revoked)
        {
            return new AccountInvitationResult { Error = S["This invitation is not valid any more."] };
        }

        if (invitation.State == AccountInvitationState.Accepted)
        {
            return invitation.AcceptedUserId == userId
                ? new AccountInvitationResult { Invitation = invitation }
                : new AccountInvitationResult { Error = S["This invitation was already accepted."] };
        }

        if (invitation.ExpiresUtc < _clock.UtcNow)
        {
            return new AccountInvitationResult { Error = S["This invitation has expired. Ask the account for a new one."] };
        }

        var userManager = _services.GetRequiredService<UserManager<IUser>>();
        var user = string.IsNullOrEmpty(userId) ? null : await userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return new AccountInvitationResult { Error = S["Sign in to accept the invitation."] };
        }

        var email = await userManager.GetEmailAsync(user);

        if (!string.Equals(email?.Trim(), invitation.Email, StringComparison.OrdinalIgnoreCase))
        {
            return new AccountInvitationResult { Error = S["This invitation was sent to {0}. Sign in with that address to accept it.", invitation.Email] };
        }

        if (await _members.FindOtherAccountIdAsync(userId, invitation.AccountId, cancellationToken) is { Length: > 0 })
        {
            return new AccountInvitationResult { Error = S["You already belong to another account."] };
        }

        var account = await _contentManager.GetAsync(invitation.AccountId, VersionOptions.Latest);

        if (account is null || !account.Has<AccountMembersPart>())
        {
            return new AccountInvitationResult { Error = S["The account was not found."] };
        }

        account.TryGet<AccountMembersPart>(out var part);
        part ??= new AccountMembersPart();
        part.Members ??= [];

        if (part.FindMember(userId) is null)
        {
            part.Members.Add(new AccountMember
            {
                UserId = userId,
                UserName = await userManager.GetUserNameAsync(user),
                Email = email,
                Role = invitation.Role,
            });
            account.Apply(part);
            await _contentManager.UpdateAsync(account);
        }

        invitation.State = AccountInvitationState.Accepted;
        invitation.AcceptedUserId = userId;
        invitation.AcceptedUtc = _clock.UtcNow;
        await _invitations.UpdateAsync(invitation, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        foreach (var observer in _services.GetServices<IAccountInvitationObserver>())
        {
            await observer.AcceptedAsync(invitation, cancellationToken);
        }

        return new AccountInvitationResult { Invitation = invitation };
    }

    /// <inheritdoc/>
    public async Task<AccountInvitation> RevokeAsync(string invitationId, string actor, CancellationToken cancellationToken = default)
    {
        var invitation = await FindByIdAsync(invitationId, cancellationToken);

        if (invitation is null || invitation.State != AccountInvitationState.Pending)
        {
            return null;
        }

        invitation.State = AccountInvitationState.Revoked;
        await _invitations.UpdateAsync(invitation, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        foreach (var observer in _services.GetServices<IAccountInvitationObserver>())
        {
            await observer.RevokedAsync(invitation, cancellationToken);
        }

        return invitation;
    }

    /// <inheritdoc/>
    public async Task<AccountInvitation> FindByIdAsync(string invitationId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(invitationId) ? null : await _invitations.FindByIdAsync(invitationId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<AccountInvitation>> ListForAccountAsync(string accountId, CancellationToken cancellationToken = default)
        => _invitations.ListByAccountAsync(accountId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<AccountInvitation>> ListForPeopleAsync(IReadOnlyCollection<string> emails, IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default)
        => _invitations.ListForPeopleAsync(
            (emails ?? []).Where(email => !string.IsNullOrWhiteSpace(email)).Select(email => email.Trim().ToUpperInvariant()).Distinct().ToArray(),
            (userIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToArray(),
            cancellationToken);

    private static string NewToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
