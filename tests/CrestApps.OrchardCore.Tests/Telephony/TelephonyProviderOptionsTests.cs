using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;

namespace CrestApps.OrchardCore.Tests.Telephony;

public sealed class TelephonyProviderOptionsTests
{
    [Fact]
    public void TryAddProvider_WhenNew_AddsProvider()
    {
        // Arrange
        var options = new TelephonyProviderOptions();

        // Act
        options.TryAddProvider("Dialpad", new TelephonyProviderTypeOptions(typeof(FakeTelephonyProviderA)) { IsEnabled = true });

        // Assert
        Assert.True(options.Providers.ContainsKey("Dialpad"));
        Assert.True(options.Providers["Dialpad"].IsEnabled);
        Assert.Equal(typeof(FakeTelephonyProviderA), options.Providers["Dialpad"].Type);
    }

    [Fact]
    public void TryAddProvider_WhenDuplicate_KeepsFirstRegistration()
    {
        // Arrange
        var options = new TelephonyProviderOptions();
        options.TryAddProvider("Dialpad", new TelephonyProviderTypeOptions(typeof(FakeTelephonyProviderA)));

        // Act
        options.TryAddProvider("Dialpad", new TelephonyProviderTypeOptions(typeof(FakeTelephonyProviderB)));

        // Assert
        Assert.Equal(typeof(FakeTelephonyProviderA), options.Providers["Dialpad"].Type);
    }

    [Fact]
    public void ReplaceProvider_WhenExisting_ReplacesRegistration()
    {
        // Arrange
        var options = new TelephonyProviderOptions();
        options.TryAddProvider("Dialpad", new TelephonyProviderTypeOptions(typeof(FakeTelephonyProviderA)));

        // Act
        options.ReplaceProvider("Dialpad", new TelephonyProviderTypeOptions(typeof(FakeTelephonyProviderB)));

        // Assert
        Assert.Equal(typeof(FakeTelephonyProviderB), options.Providers["Dialpad"].Type);
    }

    [Fact]
    public void RemoveProvider_WhenExisting_RemovesRegistration()
    {
        // Arrange
        var options = new TelephonyProviderOptions();
        options.TryAddProvider("Dialpad", new TelephonyProviderTypeOptions(typeof(FakeTelephonyProviderA)));

        // Act
        options.RemoveProvider("Dialpad");

        // Assert
        Assert.False(options.Providers.ContainsKey("Dialpad"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TryAddProvider_WithMissingName_Throws(string name)
    {
        // Arrange
        var options = new TelephonyProviderOptions();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            options.TryAddProvider(name, new TelephonyProviderTypeOptions(typeof(FakeTelephonyProviderA))));
    }

    [Fact]
    public void TelephonyProviderTypeOptions_WithNonProviderType_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new TelephonyProviderTypeOptions(typeof(object)));
    }
}
