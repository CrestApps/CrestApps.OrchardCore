using CrestApps.OrchardCore.PhoneNumbers;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Pins the separation between identifying a number and matching one. <see cref="PhoneNumber"/> answers "what
/// is this number", and a value it cannot answer for has no canonical form. Matching an uploaded spreadsheet
/// against contacts typed in over years is a looser question, and the looser answer used to be written inline
/// at each call site — which is how the same fallback ended up on the compliance path, where it made an
/// unidentifiable number look like a screened one. It lives here now, under a name that says it compares
/// rather than identifies.
/// </summary>
public sealed class PhoneNumberComparisonKeyTests
{
    [Fact]
    public void TheKey_IsTheCanonicalNumber_WhenThereIsOne()
    {
        // Arrange
        var canonical = PhoneNumber.FromE164("+14255551212");

        // Act
        var key = PhoneNumberComparisonKey.For(canonical, "(425) 555-1212");

        // Assert
        // Two people writing the same line differently produce the same key, which is the entire point.
        Assert.Equal("+14255551212", key);
        Assert.Equal(key, PhoneNumberComparisonKey.For(canonical, "425.555.1212"));
    }

    [Theory]
    [InlineData("ext. 4501", "4501")]
    [InlineData("425-555-1212", "4255551212")]
    [InlineData("  0118 999 881  ", "0118999881")]
    public void TheKey_FallsBackToTheDigits_WhenTheNumberCannotBeIdentified(string rawValue, string expected)
    {
        // Act
        var key = PhoneNumberComparisonKey.For(default, rawValue);

        // Assert
        Assert.Equal(expected, key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a number")]
    public void TheKey_IsEmpty_WhenThereIsNothingToCompare(string rawValue)
    {
        // Act
        var key = PhoneNumberComparisonKey.For(default, rawValue);

        // Assert
        // An empty key must never be stored as an owner, or every unmatched row would collide onto one entry
        // and be reported as a duplicate of the last row that had no number either.
        Assert.Equal(string.Empty, key);
    }

    [Fact]
    public void EveryShapeAValueMayHaveBeenStoredIn_IsAMatchableKey()
    {
        // Arrange
        var canonical = PhoneNumber.FromE164("+14255551212");

        // Act
        var keys = PhoneNumberComparisonKey.AllFor(canonical, "(425) 555-1212");

        // Assert
        // A contact stored before canonicalization existed still matches the same line in a new file.
        Assert.Contains("+14255551212", keys);
        Assert.Contains("4255551212", keys);
        Assert.Contains("(425) 555-1212", keys);
    }

    [Fact]
    public void TheKeys_AreDistinct_WhenTheShapesCoincide()
    {
        // Arrange
        var canonical = PhoneNumber.FromE164("+14255551212");

        // Act
        var keys = PhoneNumberComparisonKey.AllFor(canonical, "+14255551212");

        // Assert
        // The canonical value and the raw text are the same string here, so they collapse; the digits are a
        // genuinely different shape and stay, because a contact may well have been stored without the plus.
        Assert.Equal(2, keys.Length);
        Assert.Equal(keys.Length, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains("+14255551212", keys);
        Assert.Contains("14255551212", keys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TheKeys_AreEmpty_WhenThereIsNothingToCompare(string rawValue)
    {
        // Act
        var keys = PhoneNumberComparisonKey.AllFor(default, rawValue);

        // Assert
        Assert.Empty(keys);
    }

    [Fact]
    public void AnUnidentifiableValue_StillOffersItsOwnShapesAsKeys()
    {
        // Act
        var keys = PhoneNumberComparisonKey.AllFor(default, " ext. 4501 ");

        // Assert
        // The number cannot be identified, so it is never screened as though it had been; it can still be
        // recognized as the same text the contact was stored with.
        Assert.Contains("4501", keys);
        Assert.Contains("ext. 4501", keys);
        Assert.DoesNotContain(keys, key => key.Length == 0);
    }
}
