using CrestApps.OrchardCore.Accounts.Models;

namespace CrestApps.OrchardCore.Accounts.Services;

/// <summary>
/// Invites people to an account by e-mail and makes them members when they accept.
/// </summary>
public interface IAccountInvitationService
{
    /// <summary>
    /// Invites an e-mail address to an account. Inviting an address that already has a pending invitation to the account
    /// takes the earlier one back, so only the newest link works.
    /// </summary>
    Task<AccountInvitationResult> InviteAsync(AccountInvitationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the invitation of a link's token, whatever its state.
    /// </summary>
    Task<AccountInvitation> FindByTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts an invitation for a signed-in user whose e-mail address is the one invited.
    /// </summary>
    Task<AccountInvitationResult> AcceptAsync(string token, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes a pending invitation back.
    /// </summary>
    Task<AccountInvitation> RevokeAsync(string invitationId, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Expires the pending invitations whose time ran out, a page at a time in a stable order, telling the observers
    /// of each.
    /// </summary>
    /// <returns>How many invitations expired.</returns>
    Task<int> ExpireDueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an invitation.
    /// </summary>
    Task<AccountInvitation> FindByIdAsync(string invitationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the invitations of an account, newest first.
    /// </summary>
    Task<IReadOnlyList<AccountInvitation>> ListForAccountAsync(string accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the invitations sent to some e-mail addresses or accepted by some users.
    /// </summary>
    Task<IReadOnlyList<AccountInvitation>> ListForPeopleAsync(IReadOnlyCollection<string> emails, IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// What an invitation is.
/// </summary>
public sealed class AccountInvitationRequest
{
    public string AccountId { get; set; }

    public string Email { get; set; }

    public AccountMemberRole Role { get; set; }

    public string InvitedBy { get; set; }

    public string Message { get; set; }

    public string Source { get; set; }

    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets how long the link works; 14 days when empty.
    /// </summary>
    public TimeSpan? Lifetime { get; set; }
}

/// <summary>
/// The result of inviting or accepting.
/// </summary>
public sealed class AccountInvitationResult
{
    /// <summary>
    /// Gets or sets the invitation.
    /// </summary>
    public AccountInvitation Invitation { get; set; }

    /// <summary>
    /// Gets or sets the token of the link, only when it was just made.
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// Gets or sets why it failed.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets whether it worked.
    /// </summary>
    public bool Succeeded => string.IsNullOrEmpty(Error) && Invitation is not null;

    /// <summary>
    /// Gets or sets whether accepting was refused because the person has not confirmed their e-mail address.
    /// </summary>
    public bool NeedsEmailConfirmation { get; set; }
}

/// <summary>
/// Hears account invitations after they are saved. Register one with
/// <c>services.AddAccountInvitationObserver&lt;T&gt;()</c>.
/// </summary>
public interface IAccountInvitationObserver
{
    /// <summary>
    /// Called after an invitation is made, with the token of its link.
    /// </summary>
    Task InvitedAsync(AccountInvitation invitation, string token, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called after a person accepts an invitation and became a member.
    /// </summary>
    Task AcceptedAsync(AccountInvitation invitation, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// Called after an invitation is taken back.
    /// </summary>
    Task RevokedAsync(AccountInvitation invitation, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <summary>
    /// A pending invitation expired: nobody accepted it in time.
    /// </summary>
    Task ExpiredAsync(AccountInvitation invitation, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
