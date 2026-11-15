using System.Reflection;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.ExchangeRates.Navigation;
using CrestApps.OrchardCore.Subscriptions.Navigation;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules.Manifest;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// Every top-level admin menu item has an icon: Subscriptions through the template keyed on its id, whichever of the
/// module's features adds it, and Commerce through the Commerce feature each module adding to it depends on.
/// </summary>
public sealed class TopLevelAdminMenuIconTests
{
    public static TheoryData<Type> SubscriptionsProviders => new()
    {
        typeof(SubscriptionsAdminMenu),
        typeof(InstallmentPlansAdminMenu),
        typeof(TenantProvisioningAdminMenu),
    };

    [Theory]
    [MemberData(nameof(SubscriptionsProviders))]
    public async Task TheSubscriptionsItem_HasTheIdItsIconTemplateIsKeyedOn(Type providerType)
    {
        // Arrange
        var builder = new NavigationBuilder();

        // Act
        await Create(providerType).BuildNavigationAsync(NavigationConstants.AdminId, builder);

        // Assert: NavigationItemText-subscriptions.Id.cshtml draws its icon.
        var subscriptions = Assert.Single(builder.Build(), item => item.Text.Value == "Subscriptions");
        Assert.Equal("subscriptions", subscriptions.Id);
    }

    [Fact]
    public void ExchangeRates_AddingToTheCommerceMenu_DependsOnTheCommerceFeatureThatDrawsItsIcon()
    {
        // Arrange
        var feature = typeof(ExchangeRatesAdminMenu).Assembly
            .GetCustomAttributes<FeatureAttribute>()
            .Single(attribute => attribute.GetType() == typeof(FeatureAttribute) && attribute.Id == CrestApps.OrchardCore.ExchangeRates.ExchangeRatesConstants.Features.Area);

        // Assert
        Assert.Contains(CommerceConstants.Features.Area, feature.Dependencies);
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
