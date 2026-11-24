using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Accounts.Models;

/// <summary>
/// An invitation to join an account: sent to an e-mail address with a secret link, and accepted by the person who signs
/// in with that address. Only a hash of the link's token is kept.
/// </summary>
public sealed class AccountInvitation : CatalogItem
{
    /// <summary>
    /// Gets or sets the account.
    /// </summary>
    public string AccountId { get; set; }

    /// <summary>
    /// Gets or sets the e-mail address invited.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the role the person gets in the account.
    /// </summary>
    public AccountMemberRole Role { get; set; }

    /// <summary>
    /// Gets or sets the hash of the token in the invitation link.
    /// </summary>
    public string TokenHash { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public AccountInvitationState State { get; set; }

    /// <summary>
    /// Gets or sets when the link stops working.
    /// </summary>
    public DateTime ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets who invited the person.
    /// </summary>
    public string InvitedBy { get; set; }

    /// <summary>
    /// Gets or sets when.
    /// </summary>
    public DateTime InvitedUtc { get; set; }

    /// <summary>
    /// Gets or sets the user who accepted.
    /// </summary>
    public string AcceptedUserId { get; set; }

    /// <summary>
    /// Gets or sets when.
    /// </summary>
    public DateTime? AcceptedUtc { get; set; }

    /// <summary>
    /// Gets or sets what the invitation is for, such as a seat of a licence pool; empty for a plain invitation.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the record in <see cref="Source"/>.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets a short message from the person who invited.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the index row a page of invitations read this one from. Not stored.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public long PageRowId { get; set; }
}

/// <summary>
/// The state of an account invitation.
/// </summary>
public enum AccountInvitationState
{
    /// <summary>
    /// Sent and waiting.
    /// </summary>
    Pending,

    /// <summary>
    /// Accepted: the person is a member.
    /// </summary>
    Accepted,

    /// <summary>
    /// Taken back by the account.
    /// </summary>
    Revoked,

    /// <summary>
    /// Its time ran out before anyone accepted it.
    /// </summary>
    Expired,
}
