using CrestApps.OrchardCore.Numbering;

namespace CrestApps.OrchardCore.Tests.Abstractions.Numbering;

public sealed class ReadableCodesTests
{
    [Fact]
    public void Generate_UsesOnlyTheReadableAlphabet()
    {
        // Act
        var code = ReadableCodes.Generate(64);

        // Assert
        Assert.Equal(64, code.Length);
        Assert.All(code, character => Assert.Contains(character, ReadableCodes.Alphabet));
        Assert.DoesNotContain('0', code);
        Assert.DoesNotContain('O', code);
        Assert.DoesNotContain('1', code);
        Assert.DoesNotContain('I', code);
    }

    [Fact]
    public void FromNumber_GivesEveryNumberItsOwnCode()
    {
        // Act
        var codes = Enumerable.Range(1, 5000).Select(number => ReadableCodes.FromNumber(number, 4)).ToList();

        // Assert
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, code => Assert.Equal(4, code.Length));
        Assert.All(codes, code => Assert.All(code, character => Assert.Contains(character, ReadableCodes.Alphabet)));
    }

    [Fact]
    public void FromNumber_IsStable_AndDoesNotLookConsecutive()
    {
        // Act
        var first = ReadableCodes.FromNumber(1, 6);
        var second = ReadableCodes.FromNumber(2, 6);

        // Assert
        Assert.Equal(first, ReadableCodes.FromNumber(1, 6));
        Assert.NotEqual(first[..5], second[..5]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void FromNumber_RejectsALengthItCannotKeepUnique(int length)
    {
        // Act and assert
        Assert.Throws<ArgumentOutOfRangeException>(() => ReadableCodes.FromNumber(1, length));
    }
}
