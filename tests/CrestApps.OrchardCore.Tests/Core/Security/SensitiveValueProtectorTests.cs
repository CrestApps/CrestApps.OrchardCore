using System.Security.Cryptography;
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.SensitiveData;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.OrchardCore.Tests.Core.Security;

public sealed class SensitiveValueProtectorTests
{
    private const string Purpose = "Tests.SensitiveValue";

    [Fact]
    public void Protect_ThenUnprotect_GivesTheValueBack()
    {
        // Arrange
        var protector = new DataProtectionSensitiveValueProtector(new EphemeralDataProtectionProvider());

        // Act
        var stored = protector.Protect(Purpose, "123456789");

        // Assert
        Assert.True(stored.HasValue);
        Assert.DoesNotContain("123456789", stored.Cipher, StringComparison.Ordinal);
        Assert.Equal("123456789", protector.Unprotect(Purpose, stored));
    }

    [Fact]
    public void Protect_KeepsTheLastCharactersAsTheHint()
    {
        // Arrange
        var protector = new DataProtectionSensitiveValueProtector(new EphemeralDataProtectionProvider());

        // Act
        var stored = protector.Protect(Purpose, "123456789");

        // Assert
        Assert.Equal("6789", stored.Hint);
        Assert.Equal("••••6789", SensitiveMask.Mask(stored));
    }

    [Theory]
    [InlineData("1234", "34")]
    [InlineData("123", "3")]
    [InlineData("1", "")]
    public void Protect_AShortValue_NeverKeepsMoreThanHalfOfIt(string value, string expectedHint)
    {
        // Arrange
        var protector = new DataProtectionSensitiveValueProtector(new EphemeralDataProtectionProvider());

        // Act
        var stored = protector.Protect(Purpose, value);

        // Assert
        Assert.Equal(expectedHint, stored.Hint);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Protect_AnEmptyValue_StoresNothing(string value)
    {
        // Arrange
        var protector = new DataProtectionSensitiveValueProtector(new EphemeralDataProtectionProvider());

        // Act
        var stored = protector.Protect(Purpose, value);

        // Assert
        Assert.Null(stored);
        Assert.Null(protector.Unprotect(Purpose, stored));
        Assert.Equal(string.Empty, SensitiveMask.Mask(stored));
    }

    [Fact]
    public void Unprotect_WithAnotherPurpose_Throws()
    {
        // Arrange
        var protector = new DataProtectionSensitiveValueProtector(new EphemeralDataProtectionProvider());
        var stored = protector.Protect(Purpose, "123456789");

        // Act & Assert
        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect("Tests.Other", stored));
    }

    [Fact]
    public void TryUnprotect_AValueProtectedWithOtherKeys_ReturnsFalse()
    {
        // Arrange
        var stored = new DataProtectionSensitiveValueProtector(new EphemeralDataProtectionProvider()).Protect(Purpose, "123456789");
        var otherSite = new DataProtectionSensitiveValueProtector(new EphemeralDataProtectionProvider());

        // Act
        var read = otherSite.TryUnprotect(Purpose, stored, out var value);

        // Assert
        Assert.False(read);
        Assert.Null(value);
    }

    [Fact]
    public void ToString_OfAProtectedValue_ShowsOnlyTheMask()
    {
        // Arrange
        var protector = new DataProtectionSensitiveValueProtector(new EphemeralDataProtectionProvider());

        // Act
        var text = protector.Protect(Purpose, "123456789").ToString();

        // Assert
        Assert.Equal("••••6789", text);
    }

    [Theory]
    [InlineData("123456789", 4, "••••6789")]
    [InlineData("12", 4, "••••2")]
    [InlineData("", 4, "")]
    [InlineData(null, 4, "")]
    public void MaskValue_ShowsOnlyTheLastCharacters(string value, int visible, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, SensitiveMask.MaskValue(value, visible));
    }
}
