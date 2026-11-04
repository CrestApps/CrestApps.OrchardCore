using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Accounts.Models;

/// <summary>
/// The users who sign in for an account, such as the people who buy for a business or open its support cases. A user
/// is a member of one account at most. Modules read the members to know who acts for the account, and keep their own
/// settings for each member by user id.
/// </summary>
public sealed class AccountMembersPart : ContentPart
{
    /// <summary>
    /// Gets or sets the account's members.
    /// </summary>
    public List<AccountMember> Members { get; set; } = [];

    /// <summary>
    /// Returns the member a user is, or <see langword="null"/>.
    /// </summary>
    /// <param name="userId">The user id.</param>
    public AccountMember FindMember(string userId)
        => string.IsNullOrEmpty(userId)
            ? null
            : Members?.FirstOrDefault(member => string.Equals(member.UserId, userId, StringComparison.Ordinal));
}

/// <summary>
/// A user who signs in for an account.
/// </summary>
public sealed class AccountMember
{
    /// <summary>
    /// Gets or sets the user id.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the user name, as it was when the member was saved.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the email, as it was when the member was saved.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets what the member may do for the account.
    /// </summary>
    public AccountMemberRole Role { get; set; }

    /// <summary>
    /// Gets or sets the account's contact this member is, matched by email, or none.
    /// </summary>
    public string ContactContentItemId { get; set; }
}

/// <summary>
/// What a member may do for an account.
/// </summary>
public enum AccountMemberRole
{
    /// <summary>
    /// Acts for the account in what they do themselves.
    /// </summary>
    Member,

    /// <summary>
    /// Also manages the account for the other members, such as seeing all of its orders and approving them.
    /// </summary>
    Admin,
}
