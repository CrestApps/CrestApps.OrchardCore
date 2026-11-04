using System.Security.Claims;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Accounts.B2B.Services;

/// <summary>
/// The account a signed-in user buys for, and what they may do for it.
/// </summary>
public sealed class B2BAccountService
{
    private readonly IB2BAccountStore _store;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly Dictionary<string, B2BAccount> _byUser = new(StringComparer.Ordinal);

    public B2BAccountService(IB2BAccountStore store, IHttpContextAccessor httpContextAccessor)
    {
        _store = store;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Returns the account a user buys for, when it's on, or <see langword="null"/>.
    /// </summary>
    public async Task<B2BAccount> GetForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        if (!_byUser.TryGetValue(userId, out var account))
        {
            account = await _store.FindByBuyerAsync(userId, cancellationToken);
            _byUser[userId] = account is { IsEnabled: true } ? account : null;
        }

        return _byUser[userId];
    }

    /// <summary>
    /// Returns the account the signed-in user buys for and the buyer they are, or nulls.
    /// </summary>
    public async Task<(B2BAccount Account, AccountBuyer Buyer)> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var account = await GetForUserAsync(userId, cancellationToken);

        return (account, account?.FindBuyer(userId));
    }
}
