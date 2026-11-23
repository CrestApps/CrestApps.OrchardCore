using CrestApps.OrchardCore.MailingLists.Core.Coupons;

namespace CrestApps.OrchardCore.Tests.MailingLists;

/// <summary>
/// The welcome coupon's idempotency key is the same for every alias of one mailbox and never uses the member id, so
/// unsubscribing and signing up again, or signing up with an alias, gets the code issued before and no second code.
/// </summary>
public sealed class MailingListCouponKeysTests
{
    private static readonly byte[] _secret = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();

    [Theory]
    [InlineData("ada.lovelace@gmail.com")]
    [InlineData("AdaLovelace@Gmail.com")]
    [InlineData("ada.lovelace+news@gmail.com")]
    [InlineData(" adalovelace@googlemail.com ")]
    public void Build_TheAliasesOfOneMailbox_GiveTheSameKey(string alias)
    {
        // Arrange
        var expected = MailingListCouponKeys.Build("list1", "adalovelace@gmail.com", _secret);

        // Act
        var key = MailingListCouponKeys.Build("list1", alias, _secret);

        // Assert
        Assert.Equal(expected, key);
    }

    [Fact]
    public void Build_TheSameAddressAfterUnsubscribingAndSigningUpAgain_GivesTheSameKey()
    {
        // Arrange
        var first = MailingListCouponKeys.Build("list1", "grace@example.com", _secret);

        // Act
        var again = MailingListCouponKeys.Build("list1", "Grace@Example.com", _secret);

        // Assert
        Assert.Equal(first, again);
    }

    [Fact]
    public void Build_AnotherList_OrAnotherTenantSecret_GivesAnotherKey()
    {
        // Arrange
        var key = MailingListCouponKeys.Build("list1", "grace@example.com", _secret);
        var otherSecret = _secret.Select(value => (byte)(value + 1)).ToArray();

        // Act
        var otherList = MailingListCouponKeys.Build("list2", "grace@example.com", _secret);
        var otherTenant = MailingListCouponKeys.Build("list1", "grace@example.com", otherSecret);

        // Assert
        Assert.NotEqual(key, otherList);
        Assert.NotEqual(key, otherTenant);
    }

    [Fact]
    public void Build_NeverHoldsTheAddress_AndFitsTheCouponKeyLength()
    {
        // Act
        var key = MailingListCouponKeys.Build("01jlist00000000000000000ab", "grace@example.com", _secret);

        // Assert
        Assert.StartsWith(MailingListCouponKeys.Prefix + "01jlist00000000000000000ab:", key, StringComparison.Ordinal);
        Assert.DoesNotContain("grace", key, StringComparison.OrdinalIgnoreCase);
        Assert.True(key.Length <= 256);
    }

    [Fact]
    public void Build_AnEmptyAddress_GivesNoKey()
    {
        // Act
        var key = MailingListCouponKeys.Build("list1", " ", _secret);

        // Assert
        Assert.Null(key);
    }
}
