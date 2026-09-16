using CrestApps.OrchardCore.DncRegistry.Services;
using CrestApps.OrchardCore.PhoneNumbers;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Pins the rule that a do-not-call row the query matched is reported as a match. The registry used to report
/// the stored string parsed a second time, so a stored value the database still matched but the parser no
/// longer accepted was dropped — and a dropped match is indistinguishable from "this number is not listed",
/// which is a dialed call. What the caller asked about is what comes back.
/// </summary>
public sealed class LocalDncRegistryMatchResolutionTests
{
    [Fact]
    public void AMatchedRow_ReportsTheNumberTheCallerAskedAbout()
    {
        // Arrange
        var queried = PhoneNumber.FromE164("+14255551212");
        var queriedNumbers = new Dictionary<string, PhoneNumber>(StringComparer.Ordinal)
        {
            [queried.Value] = queried,
        };

        // Act
        var resolved = LocalDncRegistry.TryResolveQueriedNumber(queriedNumbers, "+14255551212", out var registeredNumber);

        // Assert
        Assert.True(resolved);
        Assert.Equal(queried, registeredNumber);
    }

    [Theory]
    [InlineData("+14255551212 ")]
    [InlineData(" +14255551212")]
    [InlineData("\t+14255551212\n")]
    public void AMatchedRow_IsStillAMatch_WhenTheStoredValueCarriesPadding(string storedNumber)
    {
        // Arrange
        // SQL Server ignores trailing blanks in an IN comparison, so a padded stored value really does come
        // back from the query. Dropping it here would mean the number was on a registry and was dialed.
        var queried = PhoneNumber.FromE164("+14255551212");
        var queriedNumbers = new Dictionary<string, PhoneNumber>(StringComparer.Ordinal)
        {
            [queried.Value] = queried,
        };

        // Act
        var resolved = LocalDncRegistry.TryResolveQueriedNumber(queriedNumbers, storedNumber, out var registeredNumber);

        // Assert
        Assert.True(resolved);
        Assert.Equal(queried, registeredNumber);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("+442071838750")]
    public void ARowThatIsNotOneOfTheQueriedNumbers_IsNotReported(string storedNumber)
    {
        // Arrange
        var queried = PhoneNumber.FromE164("+14255551212");
        var queriedNumbers = new Dictionary<string, PhoneNumber>(StringComparer.Ordinal)
        {
            [queried.Value] = queried,
        };

        // Act
        var resolved = LocalDncRegistry.TryResolveQueriedNumber(queriedNumbers, storedNumber, out var registeredNumber);

        // Assert
        // Reporting a number nobody asked about would suppress a dial the caller never screened.
        Assert.False(resolved);
        Assert.False(registeredNumber.HasValue);
    }
}
