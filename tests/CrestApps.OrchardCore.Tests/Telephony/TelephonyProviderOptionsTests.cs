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
        options.TryAddProvider("DialPad", new TelephonyProviderTypeOptions(typeof(FakeTelephonyProviderA)) { IsEnabled = true });

        // Assert
        Assert.True(options.Providers.ContainsKey("DialPad"));
        Assert.True(options.Providers["DialPad"].IsEnabled);
        Assert.Equal(typeof(FakeTelephonyProviderA), options.Providers["DialPad"].Type);
    }

    [Fact]
    public void TryAddProvider_WhenDuplicateSameType_IsIdempotent()
    {
        // Arrange
        var options = new TelephonyProviderOptions();
        options.TryAddProvider("DialPad", new TelephonyProviderTypeOptions(typeof(FakeTelephonyProviderA)) { IsEnabled = true });

        // Act
        options.TryAddProvider("DialPad", new TelephonyProviderTypeOptions(typeof(FakeTelephonyProviderA)));

        // Assert
        Assert.Single(options.Providers);
        Assert.Equal(typeof(FakeTelephonyProviderA), options.Providers["DialPad"].Type);
        Assert.True(options.Providers["DialPad"].IsEnabled);
    }

    [Fact]
    public void TryAddProvider_WhenDuplicateDifferentType_Throws()
    {
        // Arrange
        var options = new TelephonyProviderOptions();
        options.TryAddProvider("DialPad", new TelephonyProviderTypeOptions(typeof(FakeTelephonyProviderA)));

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            options.TryAddProvider("DialPad", new TelephonyProviderTypeOptions(typeof(FakeTelephonyProviderB))));
    }

    [Fact]
    public void TryAddProvider_ResolvesProviderCaseInsensitively()
    {
        // Arrange
        var options = new TelephonyProviderOptions();

        // Act
        options.TryAddProvider("Asterisk", new TelephonyProviderTypeOptions(typeof(FakeTelephonyProviderA)) { IsEnabled = true });

        // Assert
        Assert.True(options.Providers.ContainsKey("asterisk"));
        Assert.True(options.Providers.ContainsKey("ASTERISK"));
        Assert.Equal(typeof(FakeTelephonyProviderA), options.Providers["asterisk"].Type);
    }

    [Fact]
    public void TryAddProvider_WhenNameCollidesOnlyByCase_WithDifferentType_Throws()
    {
        // Arrange
        var options = new TelephonyProviderOptions();
        options.TryAddProvider("Asterisk", new TelephonyProviderTypeOptions(typeof(FakeTelephonyProviderA)));

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            options.TryAddProvider("asterisk", new TelephonyProviderTypeOptions(typeof(FakeTelephonyProviderB))));
    }

    [Fact]
    public void TryAddProvider_TrimsSurroundingWhitespaceFromName()
    {
        // Arrange
        var options = new TelephonyProviderOptions();

        // Act
        options.TryAddProvider("  DialPad  ", new TelephonyProviderTypeOptions(typeof(FakeTelephonyProviderA)));

        // Assert
        Assert.Single(options.Providers);
        Assert.True(options.Providers.ContainsKey("DialPad"));
    }

    [Fact]
    public void ReplaceProvider_WhenExisting_ReplacesRegistration()
    {
        // Arrange
        var options = new TelephonyProviderOptions();
        options.TryAddProvider("DialPad", new TelephonyProviderTypeOptions(typeof(FakeTelephonyProviderA)));

        // Act
        options.ReplaceProvider("dialpad", new TelephonyProviderTypeOptions(typeof(FakeTelephonyProviderB)));

        // Assert
        Assert.Single(options.Providers);
        Assert.Equal(typeof(FakeTelephonyProviderB), options.Providers["DialPad"].Type);
    }

    [Fact]
    public void RemoveProvider_WhenExisting_RemovesRegistration()
    {
        // Arrange
        var options = new TelephonyProviderOptions();
        options.TryAddProvider("DialPad", new TelephonyProviderTypeOptions(typeof(FakeTelephonyProviderA)));

        // Act
        options.RemoveProvider("dialpad");

        // Assert
        Assert.False(options.Providers.ContainsKey("DialPad"));
    }

    [Fact]
    public void TryAddProvider_WithNullName_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new TelephonyProviderOptions();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            options.TryAddProvider(null, new TelephonyProviderTypeOptions(typeof(FakeTelephonyProviderA))));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
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
