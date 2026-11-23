using CrestApps.OrchardCore.MailingLists.Mailchimp.Models;

namespace CrestApps.OrchardCore.Tests.MailingLists.Mailchimp;

public sealed class MailchimpCredentialsTests
{
    [Theory]
    [InlineData("abc123-us21", null, "https://us21.api.mailchimp.com/3.0/")]
    [InlineData("abc123-US7", null, "https://us7.api.mailchimp.com/3.0/")]
    [InlineData("abc123-us21", "us6", "https://us6.api.mailchimp.com/3.0/")]
    [InlineData("abc123", null, "https://api.mailchimp.com/3.0/")]
    [InlineData("abc123", "", "https://api.mailchimp.com/3.0/")]
    public void Create_UsesTheServerPrefix_ThenTheKeySuffix_ThenTheGenericHost(string apiKey, string serverPrefix, string expected)
    {
        // Act
        var credentials = MailchimpCredentials.Create(apiKey, serverPrefix);

        // Assert
        Assert.Equal(expected, credentials.BaseAddress.ToString());
    }

    [Theory]
    [InlineData("abc123-evil.example.com/x", null)]
    [InlineData("abc123-us21", "evil.example.com#")]
    [InlineData("abc123-us21@evil", null)]
    public void Create_NeverPutsAnythingButADataCenterNameInTheHost(string apiKey, string serverPrefix)
    {
        // Act
        var credentials = MailchimpCredentials.Create(apiKey, serverPrefix);

        // Assert
        Assert.EndsWith(".mailchimp.com", credentials.BaseAddress.Host, StringComparison.Ordinal);
        Assert.DoesNotContain("evil", credentials.BaseAddress.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_NeverPrintsTheKey()
    {
        // Arrange
        var credentials = MailchimpCredentials.Create("secret-key-value-us21", null);

        // Act
        var text = credentials.ToString();

        // Assert
        Assert.DoesNotContain("secret-key-value", text, StringComparison.Ordinal);
        Assert.Contains("us21.api.mailchimp.com", text, StringComparison.Ordinal);
    }
}
