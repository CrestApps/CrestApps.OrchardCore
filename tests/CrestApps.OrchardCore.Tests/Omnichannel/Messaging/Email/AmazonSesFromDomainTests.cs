using System.Text.Json;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Webhooks;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email;

public sealed class AmazonSesFromDomainTests
{
    [Theory]
    [InlineData("\"Jane\" <jane@bigcorp.com>", "jane@bigcorp.com", true)]
    [InlineData("jane@BigCorp.com", "jane@bigcorp.com", true)]
    [InlineData("\"Jane\" <jane@attacker.com>", "jane@bigcorp.com", false)]
    [InlineData("'jane@bigcorp.com'", "'jane@bigcorp.com'", false)]
    [InlineData("\"jane@bigcorp.com\"", "\"jane@bigcorp.com\"", false)]
    public void IsSameFromDomain_TakesTheVerdictOnlyForTheDomainSesChecked(string sesFrom, string parsedFrom, bool expected)
    {
        // Arrange
        using var notification = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            mail = new { commonHeaders = new { from = new[] { sesFrom } } },
        }));

        // Act
        var isSame = AmazonSesInboundEmailParser.IsSameFromDomain(notification.RootElement, parsedFrom);

        // Assert
        Assert.Equal(expected, isSame);
    }

    [Fact]
    public void IsSameFromDomain_WhenSesNamedNoFrom_IsFalse()
    {
        // Arrange
        using var notification = JsonDocument.Parse("""{ "mail": { "commonHeaders": {} } }""");

        // Act & Assert
        Assert.False(AmazonSesInboundEmailParser.IsSameFromDomain(notification.RootElement, "jane@bigcorp.com"));
    }
}
