using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using OrchardCore.Environment.Shell;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tickets.Services;

/// <summary>
/// What checking a user's password before showing protected information found.
/// </summary>
public enum TicketPasswordCheck
{
    /// <summary>
    /// The password is right.
    /// </summary>
    Confirmed,

    /// <summary>
    /// The password is wrong.
    /// </summary>
    Wrong,

    /// <summary>
    /// Too many wrong passwords: the account is locked out for a while.
    /// </summary>
    LockedOut,

    /// <summary>
    /// The user signs in without a password on this site (an external login only), so there is nothing to confirm; the
    /// protected information isn't shown to them rather than shown without a check.
    /// </summary>
    NoLocalPassword,

    /// <summary>
    /// The user isn't signed in, or their account is gone.
    /// </summary>
    NoUser,
}

/// <summary>
/// Confirms the signed-in user's password before protected information is shown.
/// </summary>
public interface ITicketPasswordConfirmation
{
    /// <summary>
    /// Checks a user's current password. A wrong one counts toward the account's lockout, as at sign-in.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="password">The password they typed.</param>
    /// <returns>What the check found.</returns>
    Task<TicketPasswordCheck> CheckAsync(ClaimsPrincipal user, string password);
}

/// <summary>
/// Checks the password with Orchard Core's sign-in manager, without signing in: a wrong password counts toward the
/// account's lockout, and a locked account isn't let through.
/// </summary>
internal sealed class TicketPasswordConfirmation : ITicketPasswordConfirmation
{
    private readonly UserManager<IUser> _userManager;
    private readonly SignInManager<IUser> _signInManager;

    public TicketPasswordConfirmation(UserManager<IUser> userManager, SignInManager<IUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    /// <inheritdoc/>
    public async Task<TicketPasswordCheck> CheckAsync(ClaimsPrincipal user, string password)
    {
        if (user is null || await _userManager.GetUserAsync(user) is not { } account)
        {
            return TicketPasswordCheck.NoUser;
        }

        if (!await _userManager.HasPasswordAsync(account))
        {
            return TicketPasswordCheck.NoLocalPassword;
        }

        if (string.IsNullOrEmpty(password))
        {
            return TicketPasswordCheck.Wrong;
        }

        var result = await _signInManager.CheckPasswordSignInAsync(account, password, lockoutOnFailure: true);

        return result.Succeeded ? TicketPasswordCheck.Confirmed : result.IsLockedOut ? TicketPasswordCheck.LockedOut : TicketPasswordCheck.Wrong;
    }
}

/// <summary>
/// Remembers, on the server, that a user confirmed their password to see a ticket's protected information, for a short
/// window (<see cref="Window"/>); nothing in the browser can say so. It is per user, per ticket and per tenant.
/// </summary>
public sealed class TicketProtectedReveals
{
    /// <summary>
    /// How long the values stay shown after the password was confirmed.
    /// </summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    private readonly IDistributedCache _cache;
    private readonly string _tenant;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketProtectedReveals"/> class.
    /// </summary>
    public TicketProtectedReveals(IDistributedCache cache, ShellSettings shellSettings)
    {
        ArgumentNullException.ThrowIfNull(shellSettings);

        _cache = cache;
        _tenant = shellSettings.Name;
    }

    /// <summary>
    /// Opens the window for a user and a ticket.
    /// </summary>
    /// <param name="userId">The user, or a requester's party.</param>
    /// <param name="ticketId">The ticket.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    public Task GrantAsync(string userId, string ticketId, CancellationToken cancellationToken = default)
        => _cache.SetAsync(Key(userId, ticketId), [1], new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Window }, cancellationToken);

    /// <summary>
    /// Gets whether the window is open for a user and a ticket.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="ticketId">The ticket.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether it is.</returns>
    public async Task<bool> IsGrantedAsync(string userId, string ticketId, CancellationToken cancellationToken = default)
        => !string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(ticketId) && await _cache.GetAsync(Key(userId, ticketId), cancellationToken) is { Length: > 0 };

    /// <summary>
    /// Closes the window (Hide).
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="ticketId">The ticket.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    public Task RevokeAsync(string userId, string ticketId, CancellationToken cancellationToken = default)
        => _cache.RemoveAsync(Key(userId, ticketId), cancellationToken);

    private string Key(string userId, string ticketId)
        => $"tickets:protected-reveal:{_tenant}:{userId}:{ticketId}";
}
