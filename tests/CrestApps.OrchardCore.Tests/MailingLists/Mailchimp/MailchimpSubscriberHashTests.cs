using CrestApps.OrchardCore.MailingLists.Mailchimp.Services;

namespace CrestApps.OrchardCore.Tests.MailingLists.Mailchimp;

public sealed class MailchimpSubscriberHashTests
{
    [Theory]
    [InlineData("urist.mcvankab@freddiesjokes.com", "62eeb292278cc15f5817cb78f7790b08")]
    [InlineData("Urist.McVankab@FreddiesJokes.com", "62eeb292278cc15f5817cb78f7790b08")]
    [InlineData("  person@example.com ", "7de8517bce4457e8390aa4006a1880fb")]
    public void Compute_HashesTheLowerCaseTrimmedAddress_AsLowerHexMd5(string email, string expected)
    {
        // Act
        var hash = MailchimpSubscriberHash.Compute(email);

        // Assert
        Assert.Equal(expected, hash);
        Assert.True(MailchimpSubscriberHash.IsHash(hash));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Compute_WithoutAnAddress_Throws(string email)
    {
        // Act and assert
        Assert.ThrowsAny<ArgumentException>(() => MailchimpSubscriberHash.Compute(email));
    }

    [Theory]
    [InlineData("62eeb292278cc15f5817cb78f7790b08", true)]
    [InlineData("62EEB292278CC15F5817CB78F7790B08", false)]
    [InlineData("62eeb292278cc15f5817cb78f7790b0", false)]
    [InlineData("person@example.com", false)]
    [InlineData(null, false)]
    public void IsHash_AcceptsOnly32LowerHexCharacters(string value, bool expected)
    {
        // Act
        var result = MailchimpSubscriberHash.IsHash(value);

        // Assert
        Assert.Equal(expected, result);
    }
}
