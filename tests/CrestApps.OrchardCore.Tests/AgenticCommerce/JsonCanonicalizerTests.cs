using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// The JSON Canonicalization Scheme matches the RFC 8785 test vectors.
/// </summary>
public sealed class JsonCanonicalizerTests
{
    [Theory]
    [InlineData("0000000000000000", "0")]
    [InlineData("8000000000000000", "0")]
    [InlineData("0000000000000001", "5e-324")]
    [InlineData("8000000000000001", "-5e-324")]
    [InlineData("7fefffffffffffff", "1.7976931348623157e+308")]
    [InlineData("ffefffffffffffff", "-1.7976931348623157e+308")]
    [InlineData("4340000000000000", "9007199254740992")]
    [InlineData("c340000000000000", "-9007199254740992")]
    [InlineData("4430000000000000", "295147905179352830000")]
    [InlineData("44b52d02c7e14af5", "9.999999999999997e+22")]
    [InlineData("44b52d02c7e14af6", "1e+23")]
    [InlineData("44b52d02c7e14af7", "1.0000000000000001e+23")]
    [InlineData("444b1ae4d6e2ef4e", "999999999999999700000")]
    [InlineData("444b1ae4d6e2ef4f", "999999999999999900000")]
    [InlineData("444b1ae4d6e2ef50", "1e+21")]
    [InlineData("3eb0c6f7a0b5ed8c", "9.999999999999997e-7")]
    [InlineData("3eb0c6f7a0b5ed8d", "0.000001")]
    [InlineData("41b3de4355555553", "333333333.3333332")]
    [InlineData("41b3de4355555554", "333333333.33333325")]
    [InlineData("41b3de4355555555", "333333333.3333333")]
    [InlineData("41b3de4355555556", "333333333.3333334")]
    [InlineData("41b3de4355555557", "333333333.33333343")]
    [InlineData("becbf647612f3696", "-0.0000033333333333333333")]
    [InlineData("43143ff3c1cb0959", "1424953923781206.2")]
    public void Canonicalize_Rfc8785NumberSample_MatchesTheAppendix(string ieee754, string expected)
    {
        // Arrange
        var value = BitConverter.Int64BitsToDouble(long.Parse(ieee754, NumberStyles.HexNumber, CultureInfo.InvariantCulture));

        // Act
        var canonical = JsonCanonicalizer.Canonicalize(JsonValue.Create(value));

        // Assert
        Assert.Equal(expected, canonical);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void FormatNumber_NotFinite_Throws(double value)
    {
        Assert.Throws<ArgumentException>(() => JsonCanonicalizer.FormatNumber(value));
    }

    [Fact]
    public void Canonicalize_Rfc8785PrimitiveSample_MatchesTheSpecificationBytes()
    {
        // Arrange
        var input = JsonNode.Parse(
            "{\"numbers\": [333333333.33333329, 1E30, 4.50, 2e-3, 0.000000000000000000000000001], " +
            "\"string\": \"\\u20ac$\\u000F\\u000aA'\\u0042\\u0022\\u005c\\\\\\\"\\/\", " +
            "\"literals\": [null, true, false]}");
        var expectedHex = string.Concat(
            "7b 22 6c 69 74 65 72 61 6c 73 22 3a 5b 6e 75 6c 6c 2c 74 72 ",
            "75 65 2c 66 61 6c 73 65 5d 2c 22 6e 75 6d 62 65 72 73 22 3a ",
            "5b 33 33 33 33 33 33 33 33 33 2e 33 33 33 33 33 33 33 2c 31 ",
            "65 2b 33 30 2c 34 2e 35 2c 30 2e 30 30 32 2c 31 65 2d 32 37 ",
            "5d 2c 22 73 74 72 69 6e 67 22 3a 22 e2 82 ac 24 5c 75 30 30 ",
            "30 66 5c 6e 41 27 42 5c 22 5c 5c 5c 5c 5c 22 2f 22 7d").Replace(" ", string.Empty);

        // Act
        var canonical = JsonCanonicalizer.Canonicalize(input);
        var bytes = JsonCanonicalizer.CanonicalizeToUtf8(input);

        // Assert
        Assert.Equal("{\"literals\":[null,true,false],\"numbers\":[333333333.3333333,1e+30,4.5,0.002,1e-27],\"string\":\"€$\\u000f\\nA'B\\\"\\\\\\\\\\\"/\"}", canonical);
        Assert.Equal(expectedHex, Convert.ToHexStringLower(bytes));
    }

    [Fact]
    public void Canonicalize_Rfc8785SortingSample_OrdersByUtf16CodeUnits()
    {
        // Arrange
        var input = JsonNode.Parse(
            "{\"\\u20ac\": \"Euro Sign\", \"\\r\": \"Carriage Return\", \"\\ufb33\": \"Hebrew Letter Dalet With Dagesh\", " +
            "\"1\": \"One\", \"\\ud83d\\ude00\": \"Emoji: Grinning Face\", \"\\u0080\": \"Control\", " +
            "\"\\u00f6\": \"Latin Small Letter O With Diaeresis\"}");

        // Act
        var canonical = JsonNode.Parse(JsonCanonicalizer.Canonicalize(input)).AsObject();
        var values = canonical.Select(property => property.Value.GetValue<string>()).ToArray();

        // Assert
        Assert.Equal(
            [
                "Carriage Return",
                "One",
                "Control",
                "Latin Small Letter O With Diaeresis",
                "Euro Sign",
                "Emoji: Grinning Face",
                "Hebrew Letter Dalet With Dagesh",
            ],
            values);
    }

    [Fact]
    public void Canonicalize_NestedObjectsAndNull_SortsEveryLevelWithoutWhitespace()
    {
        // Arrange
        var input = new JsonObject
        {
            ["b"] = new JsonArray(1, "x", null, new JsonObject { ["z"] = true, ["a"] = false }),
            ["a"] = 10L,
            ["c"] = 0.5m,
        };

        // Act
        var canonical = JsonCanonicalizer.Canonicalize(input);

        // Assert
        Assert.Equal("{\"a\":10,\"b\":[1,\"x\",null,{\"a\":false,\"z\":true}],\"c\":0.5}", canonical);
        Assert.Equal("null", JsonCanonicalizer.Canonicalize(null));
    }
}
