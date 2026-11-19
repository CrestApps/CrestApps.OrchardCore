using System.Security.Claims;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Entities;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tickets.Services;

/// <summary>
/// Keeps each user's pinned boards on their user record, so the pins follow them to any browser.
/// </summary>
internal sealed class UserTicketBoardPins : ITicketBoardPins
{
    private readonly UserManager<IUser> _userManager;

    // The menu reads the pins on every admin page; one read per request is enough.
    private IReadOnlyList<string> _pins;
    private string _pinsUserId;

    public UserTicketBoardPins(UserManager<IUser> userManager)
    {
        _userManager = userManager;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var userId = user is null ? null : _userManager.GetUserId(user);

        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        if (_pins is not null && _pinsUserId == userId)
        {
            return _pins;
        }

        _pinsUserId = userId;
        _pins = await _userManager.FindByIdAsync(userId) is User record ? [.. record.GetOrCreate<TicketBoardPinsPart>().BoardIds] : [];

        return _pins;
    }

    /// <inheritdoc/>
    public async Task<bool> SetAsync(ClaimsPrincipal user, string boardId, bool pinned, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(boardId) || user is null || await _userManager.GetUserAsync(user) is not User record)
        {
            return false;
        }

        var part = record.GetOrCreate<TicketBoardPinsPart>();
        part.BoardIds.RemoveAll(id => string.Equals(id, boardId, StringComparison.Ordinal));

        if (pinned)
        {
            part.BoardIds.Add(boardId);
        }

        record.Put(part);
        _pins = null;

        return (await _userManager.UpdateAsync(record)).Succeeded;
    }
}

/// <summary>
/// The boards a user pinned to the admin menu, kept on their user record.
/// </summary>
public sealed class TicketBoardPinsPart
{
    /// <summary>
    /// Gets or sets the boards' ids, in the order pinned.
    /// </summary>
    public List<string> BoardIds { get; set; } = [];
}
