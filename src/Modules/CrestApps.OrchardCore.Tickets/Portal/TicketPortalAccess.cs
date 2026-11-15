using System.Security.Claims;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using OrchardCore.ContentManagement;
using YesSql;

namespace CrestApps.OrchardCore.Tickets.Portal;

/// <summary>
/// What a requester may see in the portal: the tickets they asked for, and, for an administrator of an account, the
/// account's tickets. A guest (no login) sees one ticket through the signed link in its e-mails. Desk permissions are for
/// agents and play no part here.
/// </summary>
public sealed class TicketPortalAccess
{
    /// <summary>
    /// The purpose of a guest's signed link.
    /// </summary>
    public const string GuestPurpose = "Tickets.Guest";

    /// <summary>
    /// How long a guest's link works.
    /// </summary>
    public static readonly TimeSpan GuestLinkLifetime = TimeSpan.FromDays(90);

    /// <summary>
    /// The purpose of a rating link.
    /// </summary>
    public const string RatingPurpose = "Tickets.Rating";

    /// <summary>
    /// How long a rating link works.
    /// </summary>
    public static readonly TimeSpan RatingLinkLifetime = TimeSpan.FromDays(30);

    private readonly ISession _session;
    private readonly IEnumerable<IAccountMemberService> _members;
    private readonly SignedTokenService _tokens;

    public TicketPortalAccess(ISession session, IEnumerable<IAccountMemberService> members, SignedTokenService tokens)
    {
        _session = session;
        _members = members;
        _tokens = tokens;
    }

    /// <summary>
    /// Gets who the signed-in user is to the portal, or <see langword="null"/> for an anonymous user.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The viewer.</returns>
    public async Task<TicketPortalViewer> GetViewerAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        ContentItem adminOf = null;

        // An administrator of an account (with Accounts on) also follows the account's tickets.
        if (_members.FirstOrDefault() is { } members
            && await members.FindAccountAsync(userId, cancellationToken) is { } account
            && account.TryGet<AccountMembersPart>(out var membersPart)
            && membersPart.FindMember(userId)?.Role == AccountMemberRole.Admin)
        {
            adminOf = account;
        }

        return new TicketPortalViewer(userId, user.Identity?.Name, new PartyRef(PartyKinds.User, userId), adminOf?.ContentItemId, adminOf?.DisplayText);
    }

    /// <summary>
    /// Returns whether a viewer may see a ticket.
    /// </summary>
    /// <param name="viewer">The viewer.</param>
    /// <param name="ticket">The ticket.</param>
    /// <returns>Whether they may.</returns>
    public static bool CanSee(TicketPortalViewer viewer, ContentItem ticket)
        => viewer is not null
            && ticket?.TryGet<TicketPart>(out var part) == true
            && (part.Requester == viewer.Party
                || (!string.IsNullOrEmpty(viewer.AdminAccountId) && string.Equals(part.AccountId, viewer.AdminAccountId, StringComparison.Ordinal)));

    /// <summary>
    /// Lists a viewer's tickets, newest first: theirs, or the account's when <paramref name="organization"/> is set and
    /// they administer one.
    /// </summary>
    /// <param name="viewer">The viewer.</param>
    /// <param name="organization">Whether to list the account's tickets instead of their own.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to read.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The page and the total.</returns>
    public async Task<(IReadOnlyList<ContentItem> Tickets, int Total)> ListAsync(TicketPortalViewer viewer, bool organization, int skip, int take, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(viewer);

        var requester = viewer.Party.ToString();
        var accountId = viewer.AdminAccountId;
        var query = organization && !string.IsNullOrEmpty(accountId)
            ? _session.Query<ContentItem, TicketIndex>(index => index.AccountId == accountId)
            : _session.Query<ContentItem, TicketIndex>(index => index.Requester == requester);

        var total = await query.CountAsync(cancellationToken);
        var tickets = await query.OrderByDescending(index => index.CreatedUtc).ThenByDescending(index => index.Id).Skip(skip).Take(take).ListAsync(cancellationToken);

        return ([.. tickets], total);
    }

    /// <summary>
    /// Creates the token of a guest's link to a ticket.
    /// </summary>
    /// <param name="ticketId">The ticket.</param>
    /// <returns>The token.</returns>
    public string CreateGuestToken(string ticketId)
        => _tokens.Protect(GuestPurpose, ticketId, GuestLinkLifetime);

    /// <summary>
    /// Returns whether a token is a working guest link to a ticket.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="ticketId">The ticket.</param>
    /// <returns>Whether it is.</returns>
    public bool IsGuestTokenFor(string token, string ticketId)
        => !string.IsNullOrEmpty(ticketId) && string.Equals(_tokens.Unprotect(GuestPurpose, token), ticketId, StringComparison.Ordinal);

    /// <summary>
    /// Creates the token of a link to rate a ticket.
    /// </summary>
    /// <param name="ticketId">The ticket.</param>
    /// <returns>The token.</returns>
    public string CreateRatingToken(string ticketId)
        => _tokens.Protect(RatingPurpose, ticketId, RatingLinkLifetime);

    /// <summary>
    /// Returns whether a token is a working link to rate a ticket.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="ticketId">The ticket.</param>
    /// <returns>Whether it is.</returns>
    public bool IsRatingTokenFor(string token, string ticketId)
        => !string.IsNullOrEmpty(ticketId) && string.Equals(_tokens.Unprotect(RatingPurpose, token), ticketId, StringComparison.Ordinal);
}

/// <summary>
/// A signed-in requester, and the account they administer, if any.
/// </summary>
/// <param name="UserId">The user's id.</param>
/// <param name="UserName">The user's name.</param>
/// <param name="Party">The user as a party.</param>
/// <param name="AdminAccountId">The account they administer, or <see langword="null"/>.</param>
/// <param name="AdminAccountName">That account's name.</param>
public sealed record TicketPortalViewer(string UserId, string UserName, PartyRef Party, string AdminAccountId, string AdminAccountName);
