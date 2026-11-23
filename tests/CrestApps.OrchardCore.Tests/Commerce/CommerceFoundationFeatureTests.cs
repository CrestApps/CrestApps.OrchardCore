using System.Reflection;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Commerce.Controllers;
using CrestApps.OrchardCore.Commerce.Navigation;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.ExchangeRates;
using CrestApps.OrchardCore.Invoicing;
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Users;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules.Manifest;
using OrchardCore.Navigation;
using FeatureAttribute = OrchardCore.Modules.Manifest.FeatureAttribute;
using StoreStartup = CrestApps.OrchardCore.Commerce.Startup;
using TypeFeatureAttribute = OrchardCore.Modules.FeatureAttribute;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// Billing does not turn on the store. What several features share (the Commerce menu and its icon, the admin list
/// helpers, customers' access to their own admin pages, personal data) is the Commerce Foundation feature, which billing
/// depends on, and the customer's account is the Users module's My Account feature, which the foundation depends on; the store's setup guide stays in the Commerce feature, which only the
/// store modules depend on.
/// </summary>
public sealed class CommerceFoundationFeatureTests
{
    private static readonly Lazy<Dictionary<string, FeatureAttribute>> _features = new(LoadFeatures);

    public static TheoryData<string> BillingFeatures => new()
    {
        InvoicingConstants.Features.Area,
        InvoicingConstants.Features.Estimates,
        InvoicingConstants.Features.Billing,
        InvoicingConstants.Features.Api,
        InvoicingConstants.Features.Collections,
        InvoicingConstants.Features.Recurring,
        TransactionsConstants.Features.Area,
        TransactionsConstants.Features.Notification,
        TransactionsConstants.Features.PaymentTerms,
        TransactionsConstants.Features.Receipts,
        ExchangeRatesConstants.Features.Area,
        CustomersConstants.Features.Customers,
    };

    [Theory]
    [MemberData(nameof(BillingFeatures))]
    public void ABillingFeature_PullsInTheCommerceFoundation_NotTheStore(string featureId)
    {
        // Act
        var closure = DependencyClosure(featureId);

        // Assert
        Assert.Contains(CommerceConstants.Features.Foundation, closure);
        Assert.DoesNotContain(CommerceConstants.Features.Area, closure);
    }

    [Theory]
    [InlineData(TaxationConstants.Feature.Taxation)]
    [InlineData(TaxationConstants.Feature.Returns)]
    [InlineData(TaxationConstants.Feature.EFiling)]
    public void SalesTaxAndTaxReturns_NeedNoCommerceFeature(string featureId)
    {
        // Act: a site that keeps its own books files its sales tax returns without a store or the Commerce menu.
        var closure = DependencyClosure(featureId);

        // Assert
        Assert.DoesNotContain(CommerceConstants.Features.Foundation, closure);
        Assert.DoesNotContain(CommerceConstants.Features.Area, closure);
    }

    [Fact]
    public void TheCommerceFeature_IsTheStoreLayer_OnTopOfTheFoundation()
    {
        // Arrange
        var commerce = _features.Value[CommerceConstants.Features.Area];
        var foundation = _features.Value[CommerceConstants.Features.Foundation];

        // Assert: both turn on only as another feature's dependency, and the store layer brings the foundation.
        Assert.True(commerce.EnabledByDependencyOnly);
        Assert.True(foundation.EnabledByDependencyOnly);
        Assert.Contains(CommerceConstants.Features.Foundation, commerce.Dependencies);
        Assert.DoesNotContain(CommerceConstants.Features.Area, DependencyClosure(CommerceConstants.Features.Foundation));
    }

    [Fact]
    public void TheFoundationsStartup_BelongsToTheFoundation_AndTheAccountPagesToMyAccount()
    {
        // A type without [Feature] belongs to the module's main feature, the store layer.
        Assert.Equal(CommerceConstants.Features.Foundation, typeof(FoundationStartup).GetCustomAttribute<TypeFeatureAttribute>()?.FeatureName);
        Assert.Equal(UsersConstants.Feature.Account, typeof(CrestApps.OrchardCore.Users.Controllers.AccountController).GetCustomAttribute<TypeFeatureAttribute>()?.FeatureName);
        Assert.Contains(UsersConstants.Feature.Account, _features.Value[CommerceConstants.Features.Foundation].Dependencies);
        Assert.Null(typeof(StoreStartup).GetCustomAttribute<TypeFeatureAttribute>());
        Assert.Null(typeof(StoreSetupController).GetCustomAttribute<TypeFeatureAttribute>());
    }

    [Fact]
    public void OnlyTheCommerceFeature_RegistersTheStoreSetupMenuItem()
    {
        // Arrange
        var foundation = new ServiceCollection();
        var store = new ServiceCollection();

        // Act
        new FoundationStartup().ConfigureServices(foundation);
        new StoreStartup().ConfigureServices(store);

        // Assert
        Assert.Contains(foundation, service => service.ImplementationType == typeof(CommerceAdminMenu));
        Assert.DoesNotContain(foundation, service => service.ImplementationType == typeof(StoreSetupAdminMenu));
        Assert.Contains(store, service => service.ImplementationType == typeof(StoreSetupAdminMenu));
        Assert.DoesNotContain(store, service => service.ImplementationType == typeof(CommerceAdminMenu));
    }

    [Fact]
    public async Task TheFoundationsMenu_IsTheCommerceNode_WithoutTheStoreSetupGuide()
    {
        // Arrange
        var foundation = new NavigationBuilder();
        var store = new NavigationBuilder();

        // Act
        await new CommerceAdminMenu(new PassThroughLocalizer<CommerceAdminMenu>()).BuildNavigationAsync(NavigationConstants.AdminId, foundation);
        await new StoreSetupAdminMenu(new PassThroughLocalizer<StoreSetupAdminMenu>()).BuildNavigationAsync(NavigationConstants.AdminId, store);

        // Assert
        var commerce = Assert.Single(foundation.Build());
        Assert.Equal("commerce", commerce.Id);
        Assert.Empty(commerce.Items);

        var setup = Assert.Single(Assert.Single(store.Build(), item => item.Id == "commerce").Items);
        Assert.Equal("commerceSetup", setup.Id);
    }

    private static HashSet<string> DependencyClosure(string featureId)
    {
        var closure = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>([featureId]);

        while (pending.TryPop(out var id))
        {
            // Orchard Core's own features, not in these assemblies, never depend on CrestApps features.
            if (!_features.Value.TryGetValue(id, out var feature))
            {
                continue;
            }

            foreach (var dependency in feature.Dependencies ?? [])
            {
                if (closure.Add(dependency))
                {
                    pending.Push(dependency);
                }
            }
        }

        return closure;
    }

    // Every CrestApps module's features, from the assemblies next to the tests: its [Feature]s, and its [Module] when it
    // declares no feature of the module's own id.
    private static Dictionary<string, FeatureAttribute> LoadFeatures()
    {
        var features = new Dictionary<string, FeatureAttribute>(StringComparer.Ordinal);

        foreach (var path in Directory.GetFiles(AppContext.BaseDirectory, "CrestApps.OrchardCore.*.dll"))
        {
            Assembly assembly;

            try
            {
                assembly = Assembly.LoadFrom(path);
            }
            catch (BadImageFormatException)
            {
                continue;
            }

            var attributes = assembly.GetCustomAttributes<FeatureAttribute>().ToArray();

            foreach (var feature in attributes.Where(attribute => attribute.GetType() == typeof(FeatureAttribute) && !string.IsNullOrEmpty(attribute.Id)))
            {
                features.TryAdd(feature.Id, feature);
            }

            foreach (var module in attributes.OfType<ModuleAttribute>())
            {
                features.TryAdd(string.IsNullOrEmpty(module.Id) ? assembly.GetName().Name : module.Id, module);
            }
        }

        return features;
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
