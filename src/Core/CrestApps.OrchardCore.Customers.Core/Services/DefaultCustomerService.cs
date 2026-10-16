using CrestApps.Core;
using CrestApps.OrchardCore.Customers.Core.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Customers.Core.Services;

public sealed class DefaultCustomerService : ICustomerService
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);

    private readonly ICustomerStore _store;
    private readonly IUserService _userService;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public DefaultCustomerService(
        ICustomerStore store,
        IUserService userService,
        IDistributedLock distributedLock,
        IClock clock,
        ILogger<DefaultCustomerService> logger)
    {
        _store = store;
        _userService = userService;
        _distributedLock = distributedLock;
        _clock = clock;
        _logger = logger;
    }

    public Task<Customer> FindForUserAsync(string userId, CancellationToken cancellationToken = default)
        => _store.FindByUserIdAsync(userId, cancellationToken);

    public async Task<Customer> GetOrCreateForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var existing = await _store.FindByUserIdAsync(userId, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        if (await _userService.GetUserByUniqueIdAsync(userId) is not IUser user)
        {
            return null;
        }

        // Two requests of the same user (two tabs checking out) must not each make a customer.
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync("CUSTOMER_FOR_USER_" + userId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            _logger.LogWarning("The customer of a user could not be locked; the order goes ahead without one.");

            return null;
        }

        await using (locker)
        {
            existing = await _store.FindByUserIdAsync(userId, cancellationToken);

            if (existing is not null)
            {
                return existing;
            }

            var now = _clock.UtcNow;
            var customer = new Customer
            {
                ItemId = UniqueId.GenerateId(),
                UserId = userId,
                Kind = CustomerKind.Person,
                DisplayName = user.UserName,
                Email = (user as User)?.Email,
                CreatedUtc = now,
                ModifiedUtc = now,
                LastActivityUtc = now,
            };

            await _store.CreateAsync(customer, cancellationToken);

            return customer;
        }
    }

    public async Task TouchAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customer);

        customer.LastActivityUtc = _clock.UtcNow;

        await _store.UpdateAsync(customer, cancellationToken);
    }
}
