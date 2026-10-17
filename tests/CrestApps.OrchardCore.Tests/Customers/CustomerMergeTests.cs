using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Tests.Customers;

/// <summary>
/// A guest's history joins their account only once the account's email is verified: an email typed at checkout proves
/// nothing.
/// </summary>
public sealed class CustomerMergeTests
{
    private static readonly DateTime _now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryCustomerStore _store = new();
    private readonly List<CustomerMergeContext> _handled = [];

    private DefaultCustomerMergeService CreateService(User user)
    {
        var users = new Mock<IUserService>();
        users.Setup(service => service.GetUserByUniqueIdAsync(It.IsAny<string>())).ReturnsAsync((IUser)user);

        var locker = new Mock<ILocker>();
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(locking => locking.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((locker.Object, true));

        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);

        var handler = new Mock<ICustomerMergeHandler>();
        handler
            .Setup(h => h.MergingAsync(It.IsAny<CustomerMergeContext>(), It.IsAny<CancellationToken>()))
            .Callback<CustomerMergeContext, CancellationToken>((context, _) => _handled.Add(context))
            .Returns(Task.CompletedTask);

        var customers = new DefaultCustomerService(_store, users.Object, distributedLock.Object, clock.Object, NullLogger<DefaultCustomerService>.Instance);

        return new DefaultCustomerMergeService(_store, customers, users.Object, [handler.Object], distributedLock.Object, clock.Object, NullLogger<DefaultCustomerMergeService>.Instance);
    }

    private Customer AddGuest(string id, string email, Address address = null)
    {
        var guest = new Customer { ItemId = id, DisplayName = "Pat Guest", Email = email, LastActivityUtc = _now.AddDays(-5) };

        if (address is not null)
        {
            guest.Addresses = [new CustomerAddress { AddressId = id + "-address", Address = address }];
            guest.DefaultBillingAddressId = id + "-address";
        }

        _store.Customers.Add(guest);

        return guest;
    }

    private static Address Home(string city = "Springfield")
        => new() { AddressLine1 = "1 Main St", City = city, Region = "IL", PostalCode = "62701", Country = "US" };

    [Fact]
    public async Task AVerifiedEmail_MergesTheGuestsWhoBoughtWithIt()
    {
        var first = AddGuest("g1", "pat@example.test", Home());
        var second = AddGuest("g2", "PAT@example.test", Home());
        AddGuest("g3", "someone@example.test");

        var merged = await CreateService(new User { UserId = "user-1", UserName = "pat", Email = "pat@example.test", EmailConfirmed = true })
            .MergeVerifiedGuestsAsync("user-1", TestContext.Current.CancellationToken);

        var account = _store.Customers.Single(customer => customer.UserId == "user-1");

        Assert.Equal(2, merged);
        Assert.Equal(["g1", "g2"], _handled.Select(context => context.Guest.ItemId));
        Assert.All(_handled, context => Assert.Same(account, context.Into));
        Assert.Equal(CustomerStatus.Merged, first.Status);
        Assert.Equal(account.ItemId, second.MergedIntoCustomerId);
        Assert.Equal(_now, first.MergedUtc);
        Assert.Equal(["g1", "g2"], account.MergedCustomerIds);

        // The same address from two checkouts is kept once, and becomes the account's default billing address.
        var address = Assert.Single(account.Addresses);
        Assert.Equal(address.AddressId, account.DefaultBillingAddressId);
    }

    [Fact]
    public async Task AnUnverifiedEmail_MergesNothing()
    {
        var guest = AddGuest("g1", "pat@example.test");

        var merged = await CreateService(new User { UserId = "user-1", UserName = "pat", Email = "pat@example.test", EmailConfirmed = false })
            .MergeVerifiedGuestsAsync("user-1", TestContext.Current.CancellationToken);

        Assert.Equal(0, merged);
        Assert.Equal(CustomerStatus.Active, guest.Status);
        Assert.Empty(_handled);
    }

    [Fact]
    public async Task AGuestIsMergedOnce()
    {
        AddGuest("g1", "pat@example.test");
        var service = CreateService(new User { UserId = "user-1", UserName = "pat", Email = "pat@example.test", EmailConfirmed = true });

        await service.MergeVerifiedGuestsAsync("user-1", TestContext.Current.CancellationToken);
        var again = await service.MergeVerifiedGuestsAsync("user-1", TestContext.Current.CancellationToken);

        Assert.Equal(0, again);
        Assert.Single(_handled);
    }
}
