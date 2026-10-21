using System.Security.Claims;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Drivers;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Moq;
using OrchardCore.Data;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// The database sold sites are created on carries its credentials, so the editor keeps them encrypted, never shows
/// them back and lets only those who manage subscription settings change them.
/// </summary>
public sealed class SubscriptionOnboardingSettingsDisplayDriverTests
{
    private static readonly DatabaseProvider[] _providers =
    [
        new() { Name = "Sqlite", Value = "Sqlite", HasConnectionString = false, IsDefault = true },
        new() { Name = "Sql Server", Value = "SqlConnection", HasConnectionString = true },
        new() { Name = "Postgres", Value = "Postgres", HasConnectionString = true },
    ];

    private readonly IDataProtectionProvider _protection = new EphemeralDataProtectionProvider();

    [Fact]
    public async Task UpdateAsync_WithANewConnectionString_StoresItProtected()
    {
        // Arrange
        var settings = new SubscriptionOnboardingSettings();
        var context = UpdateContext(new SubscriptionOnboardingSettingsViewModel { DatabaseProvider = "Postgres", ConnectionString = " Host=db;Password=secret " });

        // Act
        await Driver(authorized: true).UpdateAsync(Mock.Of<ISite>(), settings, context);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        Assert.Equal("Postgres", settings.DatabaseProvider);
        Assert.DoesNotContain("secret", settings.ProtectedConnectionString, StringComparison.Ordinal);
        Assert.Equal("Host=db;Password=secret", Unprotect(settings.ProtectedConnectionString));
    }

    [Fact]
    public async Task UpdateAsync_WithABlankConnectionString_KeepsTheStoredOneForTheSameProvider()
    {
        // Arrange
        var stored = Protect("Host=db;Password=secret");
        var settings = new SubscriptionOnboardingSettings { DatabaseProvider = "Postgres", ProtectedConnectionString = stored };
        var context = UpdateContext(new SubscriptionOnboardingSettingsViewModel { DatabaseProvider = "Postgres", Schema = "saas" });

        // Act
        await Driver(authorized: true).UpdateAsync(Mock.Of<ISite>(), settings, context);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        Assert.Equal(stored, settings.ProtectedConnectionString);
        Assert.Equal("saas", settings.Schema);
    }

    [Fact]
    public async Task UpdateAsync_WhenTheProviderChangesWithoutAConnectionString_IsRefused()
    {
        // Arrange
        var stored = Protect("Host=db;Password=secret");
        var settings = new SubscriptionOnboardingSettings { DatabaseProvider = "Postgres", ProtectedConnectionString = stored };
        var context = UpdateContext(new SubscriptionOnboardingSettingsViewModel { DatabaseProvider = "SqlConnection" });

        // Act
        await Driver(authorized: true).UpdateAsync(Mock.Of<ISite>(), settings, context);

        // Assert
        Assert.False(context.Updater.ModelState.IsValid);
        Assert.Equal("Postgres", settings.DatabaseProvider);
        Assert.Equal(stored, settings.ProtectedConnectionString);
    }

    [Fact]
    public async Task UpdateAsync_WithAProviderOrchardCoreDoesNotOffer_IsRefused()
    {
        // Arrange
        var settings = new SubscriptionOnboardingSettings();
        var context = UpdateContext(new SubscriptionOnboardingSettingsViewModel { DatabaseProvider = "Oracle", ConnectionString = "Data Source=db" });

        // Act
        await Driver(authorized: true).UpdateAsync(Mock.Of<ISite>(), settings, context);

        // Assert
        Assert.False(context.Updater.ModelState.IsValid);
        Assert.Null(settings.DatabaseProvider);
        Assert.Null(settings.ProtectedConnectionString);
    }

    [Fact]
    public async Task UpdateAsync_SwitchingToAProviderWithoutAConnectionString_DropsTheStoredOne()
    {
        // Arrange
        var settings = new SubscriptionOnboardingSettings { DatabaseProvider = "Postgres", ProtectedConnectionString = Protect("Host=db") };
        var context = UpdateContext(new SubscriptionOnboardingSettingsViewModel { DatabaseProvider = string.Empty });

        // Act
        await Driver(authorized: true).UpdateAsync(Mock.Of<ISite>(), settings, context);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        Assert.Null(settings.DatabaseProvider);
        Assert.Null(settings.ProtectedConnectionString);
    }

    [Fact]
    public async Task EditAndUpdate_WithoutThePermission_ShowAndChangeNothing()
    {
        // Arrange
        var settings = new SubscriptionOnboardingSettings();
        var context = UpdateContext(new SubscriptionOnboardingSettingsViewModel { DatabaseProvider = "Postgres", ConnectionString = "Host=db" });
        var driver = Driver(authorized: false);

        // Act
        var updated = await driver.UpdateAsync(Mock.Of<ISite>(), settings, context);
        var edited = await driver.EditAsync(Mock.Of<ISite>(), settings, context);

        // Assert
        Assert.Null(updated);
        Assert.Null(edited);
        Assert.Null(settings.DatabaseProvider);
        Assert.Null(settings.ProtectedConnectionString);
    }

    private SubscriptionOnboardingSettingsDisplayDriver Driver(bool authorized)
    {
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(authorized ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("Test")) },
        };

        return new SubscriptionOnboardingSettingsDisplayDriver(
            httpContextAccessor,
            authorizationService.Object,
            _protection,
            _providers,
            new PassThroughStringLocalizer<SubscriptionOnboardingSettingsDisplayDriver>());
    }

    private static UpdateEditorContext UpdateContext(SubscriptionOnboardingSettingsViewModel submitted)
        => new(null, string.Empty, false, string.Empty, null, null, new FakeUpdateModel(submitted));

    private string Protect(string value)
        => _protection.CreateProtector(SubscriptionConstants.ProtectorPurposes.OnboardingConnectionString).Protect(value);

    private string Unprotect(string value)
        => _protection.CreateProtector(SubscriptionConstants.ProtectorPurposes.OnboardingConnectionString).Unprotect(value);
}
