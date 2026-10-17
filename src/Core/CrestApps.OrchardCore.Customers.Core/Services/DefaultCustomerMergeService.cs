using CrestApps.OrchardCore.Customers.Core.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Customers.Core.Services;

/// <summary>
/// Merges a verified user's guest customers into their customer. An email typed at checkout proves nothing; a verified
/// email proves the user owns it, so only then does a guest's history become theirs.
/// </summary>
public sealed class DefaultCustomerMergeService : ICustomerMergeService
{
    private const int MaxAddresses = 20;

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(60);

    private readonly ICustomerStore _store;
    private readonly ICustomerService _customerService;
    private readonly IUserService _userService;
    private readonly IEnumerable<ICustomerMergeHandler> _handlers;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public DefaultCustomerMergeService(
        ICustomerStore store,
        ICustomerService customerService,
        IUserService userService,
        IEnumerable<ICustomerMergeHandler> handlers,
        IDistributedLock distributedLock,
        IClock clock,
        ILogger<DefaultCustomerMergeService> logger)
    {
        _store = store;
        _customerService = customerService;
        _userService = userService;
        _handlers = handlers;
        _distributedLock = distributedLock;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> MergeVerifiedGuestsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) ||
            await _userService.GetUserByUniqueIdAsync(userId) is not User { EmailConfirmed: true } user ||
            string.IsNullOrWhiteSpace(user.Email))
        {
            return 0;
        }

        var guests = await _store.ListGuestsByEmailAsync(user.Email, cancellationToken);

        if (guests.Count == 0)
        {
            return 0;
        }

        // Signing in on two devices at once must not merge the same guest twice.
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync("CUSTOMER_MERGE_" + userId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            _logger.LogWarning("The guest customers of a user could not be locked for merging; they merge on the next sign-in.");

            return 0;
        }

        await using (locker)
        {
            var into = await _customerService.GetOrCreateForUserAsync(userId, cancellationToken);

            if (into is null)
            {
                return 0;
            }

            var now = _clock.UtcNow;
            var merged = 0;

            foreach (var guest in await _store.ListGuestsByEmailAsync(user.Email, cancellationToken))
            {
                if (string.Equals(guest.ItemId, into.ItemId, StringComparison.Ordinal))
                {
                    continue;
                }

                var context = new CustomerMergeContext(guest, into, userId);

                foreach (var handler in _handlers)
                {
                    await handler.MergingAsync(context, cancellationToken);
                }

                MoveAddresses(guest, into);

                guest.Status = CustomerStatus.Merged;
                guest.MergedIntoCustomerId = into.ItemId;
                guest.MergedUtc = now;
                guest.ModifiedUtc = now;

                into.MergedCustomerIds ??= [];
                into.MergedCustomerIds.Add(guest.ItemId);

                if (guest.LastActivityUtc > into.LastActivityUtc)
                {
                    into.LastActivityUtc = guest.LastActivityUtc;
                }

                await _store.UpdateAsync(guest, cancellationToken);
                merged++;
            }

            if (merged > 0)
            {
                into.ModifiedUtc = now;

                await _store.UpdateAsync(into, cancellationToken);
            }

            return merged;
        }
    }

    // The guest's addresses join the account's book, without repeating one already there, up to the book's size.
    internal static void MoveAddresses(Customer guest, Customer into)
    {
        into.Addresses ??= [];

        foreach (var address in guest.Addresses ?? [])
        {
            if (into.Addresses.Count >= MaxAddresses || into.Addresses.Any(existing => IsSameAddress(existing.Address, address.Address)))
            {
                continue;
            }

            into.Addresses.Add(address);

            if (into.DefaultBillingAddressId is null && string.Equals(guest.DefaultBillingAddressId, address.AddressId, StringComparison.Ordinal))
            {
                into.DefaultBillingAddressId = address.AddressId;
            }
        }
    }

    private static bool IsSameAddress(Addresses.Models.Address left, Addresses.Models.Address right)
        => left is not null && right is not null &&
            Same(left.AddressLine1, right.AddressLine1) &&
            Same(left.AddressLine2, right.AddressLine2) &&
            Same(left.City, right.City) &&
            Same(left.Region, right.Region) &&
            Same(left.PostalCode, right.PostalCode) &&
            Same(left.Country, right.Country);

    private static bool Same(string left, string right)
        => string.Equals(left?.Trim() ?? string.Empty, right?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
}
