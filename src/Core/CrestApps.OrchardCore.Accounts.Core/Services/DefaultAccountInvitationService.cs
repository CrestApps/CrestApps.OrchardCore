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
using OrchardCore.Locking.Distributed;
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

    public async Task<IReadOnlyList<AccountInvitation>> PagePendingAsync(long afterId, int take, CancellationToken cancellationToken = default)
    {
        var rows = await Session.QueryIndex<AccountInvitationIndex>(index => index.State == AccountInvitationState.Pending && index.Id > afterId, collection: CollectionName)
            .OrderBy(index => index.Id)
            .Take(take)
            .ListAsync(cancellationToken);
        var result = new List<AccountInvitation>();

        foreach (var row in rows)
        {
            if (await FindByIdAsync(row.ItemId, cancellationToken) is { } invitation)
            {
                invitation.PageRowId = row.Id;
                result.Add(invitation);
            }
        }

        return result;
    }

    public async Task<AccountInvitation> FindFreshByIdAsync(string invitationId, CancellationToken cancellationToken = default)
    {
        var invitation = await FindByIdAsync(invitationId, cancellationToken);

        if (invitation is null)
        {
            return null;
        }

        Session.Detach(invitation, CollectionName);

        return await FindByIdAsync(invitationId, cancellationToken);
    }

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
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(2);
    private const int PageSize = 200;

    private readonly AccountInvitationStore _invitations;
    private readonly IContentManager _contentManager;
    private readonly IAccountMemberService _members;
    private readonly IServiceProvider _services;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public DefaultAccountInvitationService(
        AccountInvitationStore invitations,
        IContentManager contentManager,
        IAccountMemberService members,
        IServiceProvider services,
        IDistributedLock distributedLock,
        ISession session,
        IClock clock,
        IStringLocalizer<DefaultAccountInvitationService> stringLocalizer)
    {
        _invitations = invitations;
        _contentManager = contentManager;
        _members = members;
        _services = services;
        _distributedLock = distributedLock;
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

        // A new invitation replaces only an earlier one for the same purpose: an invitation to a course seat never
        // takes back the invitation of another seat, and every one replaced is announced, so what it held is released.
        var superseded = (await _invitations.ListPendingAsync(account.ContentItemId, normalized, cancellationToken))
            .Where(earlier => earlier.Source == request.Source && earlier.SourceId == request.SourceId)
            .ToArray();

        foreach (var earlier in superseded)
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

        foreach (var earlier in superseded)
        {
            foreach (var observer in _services.GetServices<IAccountInvitationObserver>())
            {
                await observer.RevokedAsync(earlier, cancellationToken);
            }
        }

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
        var found = await FindByTokenAsync(token, cancellationToken);

        if (found is null || string.IsNullOrEmpty(userId))
        {
            return new AccountInvitationResult { Error = S["This invitation is not valid any more."] };
        }

        // One lock for the account and one for the person: two people accepting into one account, one person
        // accepting into two accounts, a double submit and an accept against a revoke are each done one at a time.
        var (accountLocker, accountLocked) = await _distributedLock.TryAcquireLockAsync(AccountLockKey(found.AccountId), _lockTimeout, _lockExpiration);

        if (!accountLocked)
        {
            return new AccountInvitationResult { Error = S["The account is being changed. Try again in a moment."] };
        }

        AccountInvitation invitation;

        await using (accountLocker)
        {
            var (userLocker, userLocked) = await _distributedLock.TryAcquireLockAsync(UserLockKey(userId), _lockTimeout, _lockExpiration);

            if (!userLocked)
            {
                return new AccountInvitationResult { Error = S["Your account is being changed. Try again in a moment."] };
            }

            await using (userLocker)
            {
                invitation = await _invitations.FindFreshByIdAsync(found.ItemId, cancellationToken);

                var refusal = await CheckAcceptAsync(invitation, userId, cancellationToken);

                if (refusal is not null)
                {
                    return refusal;
                }

                if (invitation.State == AccountInvitationState.Accepted)
                {
                    // Accepted already by this person, such as a double submit: nobody is told again.
                    return new AccountInvitationResult { Invitation = invitation };
                }

                var userManager = _services.GetRequiredService<UserManager<IUser>>();
                var user = await userManager.FindByIdAsync(userId);
                var member = new AccountMember
                {
                    UserId = userId,
                    UserName = await userManager.GetUserNameAsync(user),
                    Email = await userManager.GetEmailAsync(user),
                    Role = invitation.Role,
                };

                if (!await AddMemberAsync(invitation.AccountId, member))
                {
                    return new AccountInvitationResult { Error = S["The account was not found."] };
                }

                invitation.State = AccountInvitationState.Accepted;
                invitation.AcceptedUserId = userId;
                invitation.AcceptedUtc = _clock.UtcNow;
                await _invitations.UpdateAsync(invitation, cancellationToken);
                await _session.SaveChangesAsync(cancellationToken);
            }
        }

        foreach (var observer in _services.GetServices<IAccountInvitationObserver>())
        {
            await observer.AcceptedAsync(invitation, cancellationToken);
        }

        return new AccountInvitationResult { Invitation = invitation };
    }

    /// <inheritdoc/>
    public async Task<AccountInvitation> RevokeAsync(string invitationId, string actor, CancellationToken cancellationToken = default)
    {
        var found = await FindByIdAsync(invitationId, cancellationToken);

        if (found is null)
        {
            return null;
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(AccountLockKey(found.AccountId), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return null;
        }

        AccountInvitation invitation;

        await using (locker)
        {
            invitation = await _invitations.FindFreshByIdAsync(invitationId, cancellationToken);

            if (invitation is null || invitation.State != AccountInvitationState.Pending)
            {
                return null;
            }

            invitation.State = AccountInvitationState.Revoked;
            await _invitations.UpdateAsync(invitation, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
        }

        foreach (var observer in _services.GetServices<IAccountInvitationObserver>())
        {
            await observer.RevokedAsync(invitation, cancellationToken);
        }

        return invitation;
    }

    /// <inheritdoc/>
    public async Task<int> ExpireDueAsync(CancellationToken cancellationToken = default)
    {
        var expired = 0;
        var now = _clock.UtcNow;

        for (var afterId = 0L; ;)
        {
            var page = await _invitations.PagePendingAsync(afterId, PageSize, cancellationToken);

            foreach (var due in page.Where(invitation => invitation.ExpiresUtc < now))
            {
                var (locker, locked) = await _distributedLock.TryAcquireLockAsync(AccountLockKey(due.AccountId), _lockTimeout, _lockExpiration);

                if (!locked)
                {
                    continue;
                }

                AccountInvitation invitation;

                await using (locker)
                {
                    invitation = await _invitations.FindFreshByIdAsync(due.ItemId, cancellationToken);

                    if (invitation is null || invitation.State != AccountInvitationState.Pending)
                    {
                        continue;
                    }

                    invitation.State = AccountInvitationState.Expired;
                    await _invitations.UpdateAsync(invitation, cancellationToken);
                    await _session.SaveChangesAsync(cancellationToken);
                }

                expired++;

                foreach (var observer in _services.GetServices<IAccountInvitationObserver>())
                {
                    await observer.ExpiredAsync(invitation, cancellationToken);
                }
            }

            if (page.Count < PageSize)
            {
                return expired;
            }

            afterId = page[^1].PageRowId;
        }
    }

    private async Task<AccountInvitationResult> CheckAcceptAsync(AccountInvitation invitation, string userId, CancellationToken cancellationToken)
    {
        if (invitation is null || invitation.State is AccountInvitationState.Revoked or AccountInvitationState.Expired)
        {
            return new AccountInvitationResult { Error = S["This invitation is not valid any more."] };
        }

        if (invitation.State == AccountInvitationState.Accepted)
        {
            return invitation.AcceptedUserId == userId ? null : new AccountInvitationResult { Error = S["This invitation was already accepted."] };
        }

        if (invitation.ExpiresUtc < _clock.UtcNow)
        {
            return new AccountInvitationResult { Error = S["This invitation has expired. Ask the account for a new one."] };
        }

        var userManager = _services.GetRequiredService<UserManager<IUser>>();
        var user = await userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return new AccountInvitationResult { Error = S["Sign in to accept the invitation."] };
        }

        var email = await userManager.GetEmailAsync(user);

        if (!string.Equals(email?.Trim(), invitation.Email, StringComparison.OrdinalIgnoreCase))
        {
            return new AccountInvitationResult { Error = S["This invitation was sent to {0}. Sign in with that address to accept it.", invitation.Email] };
        }

        // Anyone can type an address into a profile: only a confirmed address proves the invitation reached them.
        if (!await userManager.IsEmailConfirmedAsync(user))
        {
            return new AccountInvitationResult { Error = S["Confirm your e-mail address first, then open the invitation again."], NeedsEmailConfirmation = true };
        }

        if (await _members.FindOtherAccountIdAsync(userId, invitation.AccountId, cancellationToken) is { Length: > 0 })
        {
            return new AccountInvitationResult { Error = S["You already belong to another account."] };
        }

        return null;
    }

    // Adds the member to the published account, which the members pages read, and to a draft too when there is one,
    // so publishing the draft later does not drop them.
    private async Task<bool> AddMemberAsync(string accountId, AccountMember member)
    {
        var published = await _contentManager.GetAsync(accountId, VersionOptions.Published);
        var latest = await _contentManager.GetAsync(accountId, VersionOptions.Latest);

        if (published is null && latest is null)
        {
            return false;
        }

        foreach (var version in new[] { published, latest }.Where(item => item is not null).DistinctBy(item => item.ContentItemVersionId))
        {
            if (!version.TryGet<AccountMembersPart>(out var part))
            {
                continue;
            }

            part.Members ??= [];

            if (part.FindMember(member.UserId) is null)
            {
                part.Members.Add(member);
                version.Apply(part);
                await _contentManager.UpdateAsync(version);
            }
        }

        return true;
    }

    private static string AccountLockKey(string accountId) => $"Accounts:Account:{accountId}";

    private static string UserLockKey(string userId) => $"Accounts:User:{userId}";

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
