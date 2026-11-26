using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// Orchard merges the menu items with the same caption into the first one it meets and keeps that item's id. Every
/// provider that adds the Messaging item names it the same way, so the icon template keyed on the id
/// (<c>NavigationItemText-messaging.Id</c>) applies whichever provider runs first.
/// </summary>
public sealed class MessagingAdminMenuIconTests
{
    public static TheoryData<Type> MessagingProviders => new()
    {
        typeof(MessagingAdminMenu),
        typeof(MessagingAdminMenu).Assembly.GetType("CrestApps.OrchardCore.Omnichannel.Messaging.Services.MessagingRoutingRulesAdminMenu", throwOnError: true),
    };

    [Theory]
    [MemberData(nameof(MessagingProviders))]
    public async Task TheMessagingItem_HasTheIdItsIconTemplateIsKeyedOn(Type providerType)
    {
        // Arrange
        var builder = new NavigationBuilder();

        // Act
        await Create(providerType).BuildNavigationAsync(NavigationConstants.AdminId, builder);

        // Assert
        var messaging = Assert.Single(builder.Build(), item => item.Text.Value == "Messaging");
        Assert.Equal("messaging", messaging.Id);
        Assert.Contains("messaging", messaging.Classes);
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
