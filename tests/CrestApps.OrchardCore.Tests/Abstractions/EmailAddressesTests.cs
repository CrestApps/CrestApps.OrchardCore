using CrestApps.OrchardCore.Emails;

namespace CrestApps.OrchardCore.Tests.Abstractions;

/// <summary>
/// The one rule for e-mail addresses: one stored form, a plain address only, and a key that is the same for the
/// aliases of one mailbox (a <c>+tag</c>, and Gmail's dots and second domain).
/// </summary>
public sealed class EmailAddressesTests
{
    [Theory]
    [InlineData(" Dana@Example.COM ", "dana@example.com")]
    [InlineData("lee@example.com", "lee@example.com")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public void Normalize_TrimsAndLowersTheAddress(string address, string expected)
    {
        Assert.Equal(expected, EmailAddresses.Normalize(address));
    }

    [Theory]
    [InlineData("dana@example.com")]
    [InlineData(" dana@example.com ")]
    [InlineData("Dana.Doe+news@Example.co.uk")]
    [InlineData("a@b.io")]
    public void IsValid_AcceptsAPlainAddress(string address)
    {
        Assert.True(EmailAddresses.IsValid(address));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("dana")]
    [InlineData("dana@")]
    [InlineData("@example.com")]
    [InlineData("dana@localhost")]
    [InlineData("dana@.example.com")]
    [InlineData("dana@example.com.")]
    [InlineData("da na@example.com")]
    [InlineData("Dana <dana@example.com>")]
    [InlineData("dana@example.com, lee@example.com")]
    public void IsValid_RejectsWhatIsNotOnePlainAddress(string address)
    {
        Assert.False(EmailAddresses.IsValid(address));
    }

    [Fact]
    public void IsValid_RejectsAnAddressLongerThanAMailServerTakes()
    {
        // Arrange
        var tooLong = new string('a', EmailAddresses.MaxLength) + "@example.com";

        // Act
        var valid = EmailAddresses.IsValid(tooLong);

        // Assert
        Assert.False(valid);
    }

    [Theory]
    [InlineData("lee+promo@example.com", "lee@example.com")]
    [InlineData(" Lee+Promo+2026@Example.com ", "lee@example.com")]
    [InlineData("l.ee@example.com", "l.ee@example.com")]
    [InlineData("dana.doe@gmail.com", "danadoe@gmail.com")]
    [InlineData("Dana.Doe+news@GoogleMail.com", "danadoe@gmail.com")]
    [InlineData("d.a.n.a@googlemail.com", "dana@gmail.com")]
    public void AbuseKey_IsTheSameForTheAliasesOfOneMailbox(string address, string expected)
    {
        Assert.Equal(expected, EmailAddresses.AbuseKey(address));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void AbuseKey_OfNoAddress_IsNull(string address)
    {
        Assert.Null(EmailAddresses.AbuseKey(address));
    }

    [Fact]
    public void AbuseKey_KeepsDifferentMailboxesApart()
    {
        Assert.NotEqual(EmailAddresses.AbuseKey("dana@example.com"), EmailAddresses.AbuseKey("dana@example.org"));
        Assert.NotEqual(EmailAddresses.AbuseKey("dana.doe@example.com"), EmailAddresses.AbuseKey("danadoe@example.com"));
    }
}
