using System.Globalization;
using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using YesSql;
using YesSql.Services;

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
    private readonly ICatalog<TicketDesk> _desks;
    private readonly IServiceProvider _services;

    public TicketPortalAccess(ISession session, IEnumerable<IAccountMemberService> members, SignedTokenService tokens, ICatalog<TicketDesk> desks, IServiceProvider services)
    {
        _session = session;
        _members = members;
        _tokens = tokens;
        _desks = desks;
        _services = services;
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

        ContentItem memberOf = null;
        ContentItem adminOf = null;

        // A member of an account (with Accounts on) files requests under it; an administrator also follows its tickets.
        if (_members.FirstOrDefault() is { } members
            && await members.FindAccountAsync(userId, cancellationToken) is { } account
            && account.TryGet<AccountMembersPart>(out var membersPart)
            && membersPart.FindMember(userId) is { } member)
        {
            memberOf = account;
            adminOf = member.Role == AccountMemberRole.Admin ? account : null;
        }

        // A member of staff (with Staff on) also asks the internal desks, such as IT and HR.
        var isStaff = _services.GetService<IStaffDirectory>() is { } staff && await staff.IsStaffAsync(userId, cancellationToken);

        return new TicketPortalViewer(userId, user.Identity?.Name, new PartyRef(PartyKinds.User, userId), adminOf?.ContentItemId, adminOf?.DisplayText)
        {
            MemberAccountId = memberOf?.ContentItemId,
            IsStaff = isStaff,
        };
    }

    /// <summary>
    /// Returns whether a viewer may see a ticket in the portal.
    /// </summary>
    /// <param name="viewer">The viewer.</param>
    /// <param name="ticket">The ticket.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether they may.</returns>
    public async Task<bool> CanSeeAsync(TicketPortalViewer viewer, ContentItem ticket, CancellationToken cancellationToken = default)
    {
        if (viewer is null || ticket?.TryGet<TicketPart>(out var part) != true)
        {
            return false;
        }

        var desk = string.IsNullOrEmpty(part.DeskId) ? null : await _desks.FindByIdAsync(part.DeskId, cancellationToken);

        return CanSee(viewer, ticket, desk?.Audience);
    }

    /// <summary>
    /// Returns whether a viewer may see a ticket on a desk of an audience: a ticket on a desk that serves customers to
    /// its requester and its account's administrators; a ticket on an internal desk only to its requester, when they are
    /// a member of staff, and never through an account; a ticket whose desk is gone only to its requester.
    /// </summary>
    /// <param name="viewer">The viewer.</param>
    /// <param name="ticket">The ticket.</param>
    /// <param name="audience">Its desk's audience, or <see langword="null"/> when the desk is gone.</param>
    /// <returns>Whether they may.</returns>
    public static bool CanSee(TicketPortalViewer viewer, ContentItem ticket, TicketDeskAudience? audience)
    {
        if (viewer is null || ticket?.TryGet<TicketPart>(out var part) != true)
        {
            return false;
        }

        var own = part.Requester == viewer.Party;

        return audience switch
        {
            TicketDeskAudience.Customers => own || (!string.IsNullOrEmpty(viewer.AdminAccountId) && string.Equals(part.AccountId, viewer.AdminAccountId, StringComparison.Ordinal)),
            TicketDeskAudience.Internal => own && viewer.IsStaff,
            _ => own,
        };
    }

    /// <summary>
    /// Lists a viewer's tickets, newest first: theirs, or the account's when <paramref name="organization"/> is set and
    /// they administer one, with the same rule as <see cref="CanSee"/>.
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

        var desks = await _desks.GetAllAsync(cancellationToken);
        var requester = viewer.Party.ToString();
        var accountId = viewer.AdminAccountId;
        var byAccount = organization && !string.IsNullOrEmpty(accountId);

        // The account's tickets on desks that serve customers; one's own also on internal desks when one is staff.
        var deskIds = desks
            .Where(desk => desk.Audience == TicketDeskAudience.Customers || (!byAccount && viewer.IsStaff && desk.Audience == TicketDeskAudience.Internal))
            .Select(desk => desk.ItemId)
            .ToArray();

        if (deskIds.Length == 0)
        {
            return ([], 0);
        }

        var query = byAccount
            ? _session.Query<ContentItem, TicketIndex>(index => index.AccountId == accountId && index.DeskId.IsIn(deskIds))
            : _session.Query<ContentItem, TicketIndex>(index => index.Requester == requester && index.DeskId.IsIn(deskIds));

        var total = await query.CountAsync(cancellationToken);
        var tickets = await query.OrderByDescending(index => index.CreatedUtc).ThenByDescending(index => index.Id).Skip(skip).Take(take).ListAsync(cancellationToken);

        return ([.. tickets], total);
    }

    /// <summary>
    /// Creates the token of a guest's link to a ticket, bound to its requester and its links' version.
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <returns>The token.</returns>
    public string CreateGuestToken(ContentItem ticket)
        => _tokens.Protect(GuestPurpose, Payload(ticket), GuestLinkLifetime);

    /// <summary>
    /// Returns whether a token is a working guest link to a ticket: not expired, for this ticket, its current requester
    /// and its links' current version.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="ticket">The ticket.</param>
    /// <returns>Whether it is.</returns>
    public bool IsGuestTokenFor(string token, ContentItem ticket)
        => Matches(_tokens.Unprotect(GuestPurpose, token), ticket);

    /// <summary>
    /// Creates the token of a link to rate a ticket, bound like a guest's link.
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <returns>The token.</returns>
    public string CreateRatingToken(ContentItem ticket)
        => _tokens.Protect(RatingPurpose, Payload(ticket), RatingLinkLifetime);

    /// <summary>
    /// Returns whether a token is a working link to rate a ticket.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="ticket">The ticket.</param>
    /// <returns>Whether it is.</returns>
    public bool IsRatingTokenFor(string token, ContentItem ticket)
        => Matches(_tokens.Unprotect(RatingPurpose, token), ticket);

    /// <summary>
    /// Whether the portal offers a desk to a requester: a desk that serves customers to anyone, an internal desk (IT,
    /// HR) to members of staff.
    /// </summary>
    /// <param name="desk">The desk.</param>
    /// <param name="isStaff">Whether the requester is a member of staff.</param>
    /// <returns>Whether they may open requests on it.</returns>
    public static bool Offers(TicketDesk desk, bool isStaff)
        => desk is not null && (desk.Audience == TicketDeskAudience.Customers || (isStaff && desk.Audience == TicketDeskAudience.Internal));

    // A link is for one ticket, the requester it had (their user, or their e-mail) and the links' version then: a new
    // requester, a merge or an erasure leaves every earlier link dead.
    private static string Payload(ContentItem ticket)
    {
        var part = ticket.GetOrCreate<TicketPart>();

        return string.Join('|', ticket.ContentItemId, RequesterKey(part), part.AccessVersion.ToString(CultureInfo.InvariantCulture));
    }

    private static bool Matches(string payload, ContentItem ticket)
        => !string.IsNullOrEmpty(payload)
            && ticket is not null
            && string.Equals(payload, Payload(ticket), StringComparison.Ordinal)
            && !string.IsNullOrEmpty(RequesterKey(ticket.GetOrCreate<TicketPart>()));

    private static string RequesterKey(TicketPart part)
        => !part.Requester.IsEmpty
            ? part.Requester.ToString()
            : string.IsNullOrWhiteSpace(part.RequesterContact?.Email) ? null : "mailto:" + part.RequesterContact.Email.Trim().ToLowerInvariant();
}

/// <summary>
/// A signed-in requester, and the account they administer, if any.
/// </summary>
/// <param name="UserId">The user's id.</param>
/// <param name="UserName">The user's name.</param>
/// <param name="Party">The user as a party.</param>
/// <param name="AdminAccountId">The account they administer, or <see langword="null"/>.</param>
/// <param name="AdminAccountName">That account's name.</param>
public sealed record TicketPortalViewer(string UserId, string UserName, PartyRef Party, string AdminAccountId, string AdminAccountName)
{
    /// <summary>
    /// Gets the account the user is a member of, in any role, which their requests are filed under.
    /// </summary>
    public string MemberAccountId { get; init; }

    /// <summary>
    /// Gets whether the user is a member of staff.
    /// </summary>
    public bool IsStaff { get; init; }
}
