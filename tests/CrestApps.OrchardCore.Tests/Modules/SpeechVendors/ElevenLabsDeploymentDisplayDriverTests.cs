using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Infrastructure;
using CrestApps.OrchardCore.ElevenLabs.Drivers;
using CrestApps.OrchardCore.ElevenLabs.ViewModels;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;

namespace CrestApps.OrchardCore.Tests.Modules.SpeechVendors;

public sealed class ElevenLabsDeploymentDisplayDriverTests
{
    private readonly EphemeralDataProtectionProvider _dataProtectionProvider = new();

    [Fact]
    public async Task UpdateAsync_NewApiKey_StoresItProtectedSoTheProviderReadsIt()
    {
        // Arrange
        var deployment = new AIDeployment { ClientName = "ElevenLabs" };
        var context = CreateContext(new ElevenLabsDeploymentViewModel { ApiKey = "el-secret" });

        // Act
        await CreateDriver().UpdateAsync(deployment, context);

        // Assert
        Assert.Equal(0, context.Updater.ModelState.ErrorCount);
        Assert.NotEqual("el-secret", deployment.Properties["ApiKey"]);
        Assert.Equal("el-secret", AIDeploymentConnectionEntryFactory.Create(deployment, _dataProtectionProvider).GetApiKey());
    }

    [Fact]
    public async Task UpdateAsync_BlankApiKeyWithAStoredOne_KeepsTheStoredKey()
    {
        // Arrange
        var stored = _dataProtectionProvider.CreateProtector("AIProviderConnection").Protect("el-secret");
        var deployment = new AIDeployment
        {
            ClientName = "ElevenLabs",
            Properties = new Dictionary<string, object> { ["ApiKey"] = stored },
        };
        var context = CreateContext(new ElevenLabsDeploymentViewModel());

        // Act
        await CreateDriver().UpdateAsync(deployment, context);

        // Assert
        Assert.Equal(0, context.Updater.ModelState.ErrorCount);
        Assert.Equal(stored, deployment.Properties["ApiKey"]);
    }

    [Fact]
    public async Task UpdateAsync_NoApiKeyAndNoneStored_IsRefused()
    {
        // Arrange
        var deployment = new AIDeployment { ClientName = "ElevenLabs" };
        var context = CreateContext(new ElevenLabsDeploymentViewModel());

        // Act
        await CreateDriver().UpdateAsync(deployment, context);

        // Assert
        Assert.Equal(1, context.Updater.ModelState.ErrorCount);
    }

    [Theory]
    [InlineData("https://api.eu.residency.elevenlabs.io", true)]
    [InlineData("not an address", false)]
    public async Task UpdateAsync_Endpoint_IsStoredOnlyWhenItIsAnAbsoluteAddress(string endpoint, bool stored)
    {
        // Arrange
        var deployment = new AIDeployment { ClientName = "ElevenLabs" };
        var context = CreateContext(new ElevenLabsDeploymentViewModel { ApiKey = "el-secret", Endpoint = endpoint });

        // Act
        await CreateDriver().UpdateAsync(deployment, context);

        // Assert
        Assert.Equal(stored, deployment.Properties.ContainsKey("Endpoint"));
        Assert.Equal(stored ? 0 : 1, context.Updater.ModelState.ErrorCount);
    }

    [Fact]
    public async Task UpdateAsync_BlankEndpoint_RemovesTheStoredOne()
    {
        // Arrange
        var deployment = new AIDeployment
        {
            ClientName = "ElevenLabs",
            Properties = new Dictionary<string, object> { ["Endpoint"] = "https://api.elevenlabs.io" },
        };
        var context = CreateContext(new ElevenLabsDeploymentViewModel { ApiKey = "el-secret" });

        // Act
        await CreateDriver().UpdateAsync(deployment, context);

        // Assert
        Assert.False(deployment.Properties.ContainsKey("Endpoint"));
    }

    [Fact]
    public async Task UpdateAsync_AnotherProvidersDeployment_IsLeftAlone()
    {
        // Arrange
        var deployment = new AIDeployment { ClientName = "OpenAI" };
        var context = CreateContext(new ElevenLabsDeploymentViewModel { ApiKey = "el-secret" });

        // Act
        var result = await CreateDriver().UpdateAsync(deployment, context);

        // Assert
        Assert.Null(result);
        Assert.Empty(deployment.Properties);
    }

    private ElevenLabsDeploymentDisplayDriver CreateDriver()
    {
        return new ElevenLabsDeploymentDisplayDriver(_dataProtectionProvider, new PassThroughLocalizer<ElevenLabsDeploymentDisplayDriver>());
    }

    private static UpdateEditorContext CreateContext(ElevenLabsDeploymentViewModel posted)
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
