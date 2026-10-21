using System.Security.Cryptography;
using CrestApps.OrchardCore.Core.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Core.Services;

public sealed class ProtectedSettingExtensionsTests
{
    private const string Purpose = "Tests.ProtectedSetting";

    [Fact]
    public void ProtectSetting_ThenUnprotectSetting_GivesTheSecretBack()
    {
        // Arrange
        var provider = new EphemeralDataProtectionProvider();

        // Act
        var stored = provider.ProtectSetting(Purpose, "sk_test_123");

        // Assert
        Assert.NotEqual("sk_test_123", stored);
        Assert.Equal("sk_test_123", provider.UnprotectSetting(Purpose, stored));
    }

    [Fact]
    public void AnEmptySetting_StaysEmpty()
    {
        // Arrange
        var provider = new EphemeralDataProtectionProvider();

        // Act & Assert
        Assert.Null(provider.ProtectSetting(Purpose, null));
        Assert.Equal(string.Empty, provider.ProtectSetting(Purpose, string.Empty));
        Assert.Null(provider.UnprotectSetting(Purpose, null));
        Assert.Null(provider.UnprotectSettingOrDefault(Purpose, string.Empty, NullLogger.Instance, "secret"));
    }

    [Fact]
    public void ASettingProtectedWithOtherKeys_ThrowsOrIsTreatedAsNotEntered()
    {
        // Arrange
        var stored = new EphemeralDataProtectionProvider().ProtectSetting(Purpose, "sk_test_123");
        var provider = new EphemeralDataProtectionProvider();

        // Act & Assert
        Assert.ThrowsAny<CryptographicException>(() => provider.UnprotectSetting(Purpose, stored));
        Assert.Null(provider.UnprotectSettingOrDefault(Purpose, stored, NullLogger.Instance, "secret"));
    }
}
