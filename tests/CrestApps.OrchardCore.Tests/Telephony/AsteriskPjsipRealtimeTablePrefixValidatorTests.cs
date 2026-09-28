using CrestApps.OrchardCore.Asterisk.Services;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class AsteriskPjsipRealtimeTablePrefixValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("asterisk_")]
    [InlineData("ps")]
    [InlineData("tenant1_")]
    [InlineData("my_schema.")]
    [InlineData("my_schema.tenant1_")]
    public void IsValid_AcceptsEmptyOrStrictIdentifierPrefixes(string prefix)
    {
        // Act & Assert
        Assert.True(AsteriskPjsipRealtimeTablePrefixValidator.IsValid(prefix));
    }

    [Theory]
    [InlineData("ps_auths; DROP TABLE ps_auths;--")]
    [InlineData("ps auths")]
    [InlineData("tenant-1_")]
    [InlineData("a.b.c")]
    [InlineData("\"ps_")]
    [InlineData("ps`")]
    [InlineData(".ps_")]
    [InlineData("ps%")]
    public void IsValid_RejectsPrefixesThatAreNotStrictSqlIdentifiers(string prefix)
    {
        // Act & Assert
        Assert.False(AsteriskPjsipRealtimeTablePrefixValidator.IsValid(prefix));
    }

    [Fact]
    public void EnsureValid_WhenPrefixIsEmpty_ReturnsEmptyString()
    {
        // Act
        var result = AsteriskPjsipRealtimeTablePrefixValidator.EnsureValid("  ");

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void EnsureValid_WhenPrefixIsValid_ReturnsTrimmedPrefix()
    {
        // Act
        var result = AsteriskPjsipRealtimeTablePrefixValidator.EnsureValid("  tenant1_  ");

        // Assert
        Assert.Equal("tenant1_", result);
    }

    [Fact]
    public void EnsureValid_WhenPrefixIsInjection_ThrowsInsteadOfConcatenating()
    {
        // This is the SQL-boundary guard used by AsteriskPjsipRealtimeCredentialStore.Table so an
        // unvalidated tenant-supplied prefix can never be concatenated into a command.
        Assert.Throws<InvalidOperationException>(() =>
            AsteriskPjsipRealtimeTablePrefixValidator.EnsureValid("ps_auths; DROP TABLE ps_auths;--"));
    }
}
