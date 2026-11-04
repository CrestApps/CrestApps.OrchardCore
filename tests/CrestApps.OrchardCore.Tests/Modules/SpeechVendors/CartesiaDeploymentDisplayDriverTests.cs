using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Infrastructure;
using CrestApps.OrchardCore.Cartesia.Drivers;
using CrestApps.OrchardCore.Cartesia.ViewModels;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;

namespace CrestApps.OrchardCore.Tests.Modules.SpeechVendors;

public sealed class CartesiaDeploymentDisplayDriverTests
{
    private readonly EphemeralDataProtectionProvider _dataProtectionProvider = new();

    [Fact]
    public async Task UpdateAsync_NewApiKey_StoresItProtectedSoTheProviderReadsIt()
    {
        // Arrange
        var deployment = new AIDeployment { ClientName = "Cartesia" };
        var context = CreateContext(new CartesiaDeploymentViewModel { ApiKey = "ca-secret", Endpoint = "https://api.cartesia.ai" });

        // Act
        await CreateDriver().UpdateAsync(deployment, context);

        // Assert
        Assert.Equal(0, context.Updater.ModelState.ErrorCount);
        Assert.NotEqual("ca-secret", deployment.Properties["ApiKey"]);
        var entry = AIDeploymentConnectionEntryFactory.Create(deployment, _dataProtectionProvider);
        Assert.Equal("ca-secret", entry.GetApiKey());
        Assert.Equal(new Uri("https://api.cartesia.ai"), entry.GetEndpoint(throwException: false));
    }

    [Fact]
    public async Task UpdateAsync_NoApiKeyAndNoneStored_IsRefused()
    {
        // Arrange
        var deployment = new AIDeployment { ClientName = "Cartesia" };
        var context = CreateContext(new CartesiaDeploymentViewModel());

        // Act
        await CreateDriver().UpdateAsync(deployment, context);

        // Assert
        Assert.Equal(1, context.Updater.ModelState.ErrorCount);
    }

    [Fact]
    public async Task UpdateAsync_AnotherProvidersDeployment_IsLeftAlone()
    {
        // Arrange
        var deployment = new AIDeployment { ClientName = "ElevenLabs" };
        var context = CreateContext(new CartesiaDeploymentViewModel { ApiKey = "ca-secret" });

        // Act
        var result = await CreateDriver().UpdateAsync(deployment, context);

        // Assert
        Assert.Null(result);
        Assert.Empty(deployment.Properties);
    }

    private CartesiaDeploymentDisplayDriver CreateDriver()
    {
        return new CartesiaDeploymentDisplayDriver(_dataProtectionProvider, new PassThroughLocalizer<CartesiaDeploymentDisplayDriver>());
    }

    private static UpdateEditorContext CreateContext(CartesiaDeploymentViewModel posted)
        => new(null, string.Empty, false, string.Empty, null, null, new FakeUpdateModel(posted));

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
