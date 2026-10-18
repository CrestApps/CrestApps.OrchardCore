using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Commerce.Navigation;
using CrestApps.OrchardCore.Customers.Navigation;
using CrestApps.OrchardCore.Downloads.Navigation;
using CrestApps.OrchardCore.Orders.Navigation;
using CrestApps.OrchardCore.Stripe.Navigation;
using CrestApps.OrchardCore.Subscriptions.Navigation;
using CrestApps.OrchardCore.Transactions.Navigation;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// The storefront account's pages come from navigation providers that add to the account menu only, never to the
/// admin menu or any other named menu.
/// </summary>
public sealed class CommerceAccountMenuTests
{
    public static TheoryData<Type> Providers => new()
    {
        typeof(CommerceAccountMenu),
        typeof(OrdersAccountMenu),
        typeof(CustomersAccountMenu),
        typeof(DownloadsAccountMenu),
        typeof(SubscriptionsAccountMenu),
        typeof(TransactionsAccountMenu),
        typeof(StripeAccountMenu),
    };

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task AddsToTheAccountMenuOnly(Type providerType)
    {
        var provider = Create(providerType);

        var admin = new NavigationBuilder();
        await provider.BuildNavigationAsync(NavigationConstants.AdminId, admin);

        var other = new NavigationBuilder();
        await provider.BuildNavigationAsync("main", other);

        var account = new NavigationBuilder();
        await provider.BuildNavigationAsync(CommerceConstants.Account.MenuName, account);

        Assert.Empty(admin.Build());
        Assert.Empty(other.Build());
        Assert.NotEmpty(account.Build());
    }

    [Fact]
    public async Task EveryAccountPageHasItsOwnId()
    {
        var builder = new NavigationBuilder();

        foreach (var row in Providers)
        {
            await Create(row.Data).BuildNavigationAsync(CommerceConstants.Account.MenuName, builder);
        }

        var ids = builder.Build().Select(item => item.Id).ToList();

        Assert.All(ids, id => Assert.False(string.IsNullOrEmpty(id)));
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    private static INavigationProvider Create(Type providerType)
    {
        var localizer = Activator.CreateInstance(typeof(PassThroughLocalizer<>).MakeGenericType(providerType));

        return (INavigationProvider)Activator.CreateInstance(providerType, localizer);
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
