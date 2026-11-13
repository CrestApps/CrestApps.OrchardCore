using CrestApps.OrchardCore.Checks.Core.Services;
using CrestApps.OrchardCore.Checks.Models;

namespace CrestApps.OrchardCore.Tests.Checks;

public sealed class UnitedStatesCheckMicrFormatTests
{
    private const char T = MicrSymbols.Transit;
    private const char U = MicrSymbols.OnUs;

    [Fact]
    public void Format_ABusinessCheck_PlacesEachFieldAtItsStandardPositions()
    {
        // Arrange
        var format = new UnitedStatesCheckMicrFormat();

        // Act
        var line = format.Format(new MicrLineRequest
        {
            RoutingNumber = "021000021",
            AccountNumber = "123456789",
            CheckNumber = 1001,
            UseAuxiliaryOnUs = true,
        });

        // Assert
        // The amount field (1 to 12) and position 13 are blank.
        for (var position = 1; position <= 13; position++)
        {
            Assert.Equal(' ', line.At(position));
        }

        // The on-us field: the account number ends at 15, the on-us symbol at 14.
        Assert.Equal(U, line.At(14));
        Assert.Equal("123456789", Read(line, 23, 15));

        // The routing number between transit symbols at 43 and 33.
        Assert.Equal(T, line.At(43));
        Assert.Equal("021000021", Read(line, 42, 34));
        Assert.Equal(T, line.At(33));

        // Position 44, the external processing code, is blank.
        Assert.Equal(' ', line.At(44));

        // The auxiliary on-us field: the check number on six digits between on-us symbols, from 45.
        Assert.Equal(U, line.At(45));
        Assert.Equal("001001", Read(line, 51, 46));
        Assert.Equal(U, line.At(52));
        Assert.Equal(U + "001001" + U + " " + T + "021000021" + T + "         123456789" + U, line.ToString());
    }

    [Fact]
    public void Format_APersonalCheck_PutsTheCheckNumberAfterTheAccountNumber()
    {
        // Arrange
        var format = new UnitedStatesCheckMicrFormat();

        // Act
        var line = format.Format(new MicrLineRequest
        {
            RoutingNumber = "021000021",
            AccountNumber = "987654",
            CheckNumber = 215,
            UseAuxiliaryOnUs = false,
        });

        // Assert
        Assert.Equal("215", Read(line, 16, 14));
        Assert.Equal(U, line.At(17));
        Assert.Equal("987654", Read(line, 23, 18));
        Assert.Equal(' ', line.At(44));
        Assert.Equal(' ', line.At(45));
    }

    [Fact]
    public void LeftOf_PositionOne_IsFiveSixteenthsOfAnInchAndOneEighthFromTheRightEdge()
    {
        // Act
        var left = MicrLine.LeftOf(1, 612);

        // Assert
        Assert.Equal(612 - 22.5 - 9, left);
    }

    [Theory]
    [InlineData("02100002")]
    [InlineData("")]
    public void Format_ARoutingNumberWithoutNineDigits_IsRefused(string routing)
    {
        // Arrange
        var format = new UnitedStatesCheckMicrFormat();

        // Act and assert
        Assert.Throws<ArgumentException>(() => format.Format(new MicrLineRequest { RoutingNumber = routing, AccountNumber = "1234", CheckNumber = 1 }));
    }

    [Fact]
    public void Format_AnAccountNumberTooLongForTheOnUsField_IsRefused()
    {
        // Arrange
        var format = new UnitedStatesCheckMicrFormat();

        // Act and assert
        Assert.Throws<ArgumentException>(() => format.Format(new MicrLineRequest { RoutingNumber = "021000021", AccountNumber = new string('9', 19), CheckNumber = 1 }));
    }

    private static string Read(MicrLine line, int from, int to)
        => new(Enumerable.Range(to, from - to + 1).Reverse().Select(line.At).ToArray());
}
