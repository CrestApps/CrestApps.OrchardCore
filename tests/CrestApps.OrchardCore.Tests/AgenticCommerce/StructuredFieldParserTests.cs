using CrestApps.OrchardCore.AgenticCommerce.Core.Security;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// RFC 8941 dictionaries parse and serialize back the way RFC 9421 and UCP headers need.
/// </summary>
public sealed class StructuredFieldParserTests
{
    [Fact]
    public void TryParseDictionary_SignatureInput_ReadsTheInnerListAndParameters()
    {
        // Arrange
        var value = "sig-b24=(\"@status\" \"content-type\" \"content-digest\" \"content-length\");created=1618884473;keyid=\"test-key-ecc-p256\"";

        // Act
        var parsed = StructuredFieldParser.TryParseDictionary(value, out var dictionary);
        var member = dictionary["sig-b24"];

        // Assert
        Assert.True(parsed);
        Assert.True(member.IsInnerList);
        Assert.Equal(["@status", "content-type", "content-digest", "content-length"], member.InnerList.Select(item => (string)item.Value));
        Assert.Equal(1618884473L, member.Parameters["created"]);
        Assert.Equal("test-key-ecc-p256", member.Parameters["keyid"]);
        Assert.Equal(value, "sig-b24=" + StructuredFieldSerializer.SerializeMember(member));
    }

    [Fact]
    public void TryParseDictionary_MixedMembers_ReadsEveryBareItemType()
    {
        // Arrange
        var value = "a=1, b=?0, c, d=:AQID:, e=tok/en, f=\"q\\\"s\", g=-1.5;p=x";

        // Act
        var parsed = StructuredFieldParser.TryParseDictionary(value, out var dictionary);

        // Assert
        Assert.True(parsed);
        Assert.Equal(["a", "b", "c", "d", "e", "f", "g"], dictionary.Keys);
        Assert.Equal(1L, dictionary["a"].Item.Value);
        Assert.Equal(false, dictionary["b"].Item.Value);
        Assert.Equal(true, dictionary["c"].Item.Value);
        Assert.Equal(new byte[] { 1, 2, 3 }, dictionary["d"].Item.Value);
        Assert.True(dictionary["e"].Item.IsToken);
        Assert.Equal("tok/en", dictionary.GetString("e"));
        Assert.Equal("q\"s", dictionary.GetString("f"));
        Assert.Equal(-1.5m, dictionary["g"].Item.Value);
        Assert.Equal(new StructuredFieldToken("x"), dictionary["g"].Parameters["p"]);
        Assert.Equal(value, StructuredFieldSerializer.SerializeDictionary(dictionary));
    }

    [Fact]
    public void TryParseDictionary_UcpAgentHeader_ReadsTheProfile()
    {
        // Act
        var parsed = StructuredFieldParser.TryParseDictionary("profile=\"https://agent.example/.well-known/ucp\"", out var dictionary);

        // Assert
        Assert.True(parsed);
        Assert.Equal("https://agent.example/.well-known/ucp", dictionary.GetString("profile"));
        Assert.Null(dictionary.GetString("missing"));
    }

    [Theory]
    [InlineData("a=1,")]
    [InlineData("A=1")]
    [InlineData("a=(\"x\"")]
    [InlineData("a=\"unterminated")]
    [InlineData("a=:not base64:")]
    [InlineData("a=1 b=2")]
    [InlineData("a=1234567890123456")]
    [InlineData("a=1.2345")]
    public void TryParseDictionary_InvalidValues_ReturnsFalse(string value)
    {
        Assert.False(StructuredFieldParser.TryParseDictionary(value, out _));
    }
}
