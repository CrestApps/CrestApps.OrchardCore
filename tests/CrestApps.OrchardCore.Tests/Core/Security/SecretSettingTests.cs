using CrestApps.OrchardCore.Core.Security;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.OrchardCore.Tests.Core.Security;

public sealed class SecretSettingTests
{
    private const string Purpose = "Tests.Secret";

    private readonly IDataProtectionProvider _provider = new EphemeralDataProtectionProvider();

    [Fact]
    public void ApplySecret_ProtectsANewValue()
    {
        // Act
        var stored = _provider.ApplySecret(Purpose, existing: null, posted: "  new-key  ");

        // Assert
        Assert.NotEqual("new-key", stored);
        Assert.Equal("new-key", _provider.UnprotectSetting(Purpose, stored));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ApplySecret_KeepsTheStoredValue_WhenNothingIsPosted(string posted)
    {
        // Arrange
        var existing = _provider.ProtectSetting(Purpose, "old-key");

        // Act
        var stored = _provider.ApplySecret(Purpose, existing, posted);

        // Assert
        Assert.Equal(existing, stored);
    }

    [Fact]
    public void ApplySecret_ClearsTheStoredValue_WhenAskedToRemoveIt()
    {
        // Arrange
        var existing = _provider.ProtectSetting(Purpose, "old-key");

        // Act
        var stored = _provider.ApplySecret(Purpose, existing, posted: "ignored", remove: true);

        // Assert
        Assert.Null(stored);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("CfDJ8...", true)]
    public void HasSecret_TellsWhetherASecretIsStored(string stored, bool expected)
    {
        // Act
        var actual = ProtectedSettingExtensions.HasSecret(stored);

        // Assert
        Assert.Equal(expected, actual);
    }
}
