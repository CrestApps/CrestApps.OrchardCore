using CrestApps.OrchardCore.Telnyx.Services;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// The soft phone renews its credential within ten minutes of expiry. A lifetime inside that window, which the settings
/// screen used to accept, renewed the credential every few minutes, and calls rung while it was replaced were refused.
/// </summary>
public sealed class TelnyxCredentialLifetimeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Normalize_WhenNoLifetimeIsSet_UsesTheDefault(int minutes)
    {
        Assert.Equal(TelnyxCredentialLifetime.DefaultMinutes, TelnyxCredentialLifetime.Normalize(minutes));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(29)]
    public void Normalize_WhenTheLifetimeIsTooShort_RaisesItToTheMinimum(int minutes)
    {
        Assert.Equal(TelnyxCredentialLifetime.MinimumMinutes, TelnyxCredentialLifetime.Normalize(minutes));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(180)]
    [InlineData(600)]
    public void Normalize_KeepsALifetimeAtOrAboveTheMinimum(int minutes)
    {
        Assert.Equal(minutes, TelnyxCredentialLifetime.Normalize(minutes));
    }

    [Fact]
    public void TheMinimum_StaysWellClearOfTheSoftPhonesTenMinuteRenewalWindow()
    {
        Assert.True(TelnyxCredentialLifetime.MinimumMinutes >= 20);
        Assert.True(TelnyxCredentialLifetime.DefaultMinutes >= TelnyxCredentialLifetime.MinimumMinutes);
    }
}
