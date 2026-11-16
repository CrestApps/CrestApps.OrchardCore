using CrestApps.OrchardCore.Subscriptions.Core.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// Review MD2: the tenant purchase validator refused only the ~150 public suffixes of a short built-in list, so a buyer
/// could claim any other name nobody owns (a wildcard suffix such as <c>foo.ck</c>, or a platform the list left out). It
/// now reads the full Public Suffix List, ICANN and private sections, with its wildcard and exception rules.
/// </summary>
public sealed class PublicSuffixListTests
{
    [Theory]
    [InlineData("com")]
    [InlineData("co.uk")]
    [InlineData("CO.UK")]
    [InlineData("com.au")]
    [InlineData("github.io")]
    [InlineData("azurewebsites.net")]
    [InlineData("blob.core.windows.net")]
    [InlineData("ck")]
    [InlineData("foo.ck")]
    [InlineData("anything.kawasaki.jp")]
    [InlineData("ec2.compute.amazonaws.com")]
    [InlineData("xn--55qx5d.cn")]
    [InlineData("unlistedtld")]
    public void IsPublicSuffix_ARuleOfTheList_IsTrue(string domain)
    {
        Assert.True(PublicSuffixList.IsPublicSuffix(domain));
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("www.example.com")]
    [InlineData("myshop.co.uk")]
    [InlineData("myshop.github.io")]
    [InlineData("www.ck")]
    [InlineData("city.kawasaki.jp")]
    [InlineData("kawasaki.jp")]
    [InlineData("amazonaws.com")]
    [InlineData("example.unlistedtld")]
    [InlineData("")]
    [InlineData(null)]
    public void IsPublicSuffix_ANameSomebodyCanOwn_IsFalse(string domain)
    {
        Assert.False(PublicSuffixList.IsPublicSuffix(domain));
    }

    [Theory]
    [InlineData("example.com", "example.com")]
    [InlineData("www.example.com", "example.com")]
    [InlineData("a.b.example.co.uk", "example.co.uk")]
    [InlineData("shop.myshop.github.io", "myshop.github.io")]
    [InlineData("b.foo.ck", "b.foo.ck")]
    [InlineData("a.b.foo.ck", "b.foo.ck")]
    [InlineData("www.ck", "www.ck")]
    [InlineData("a.www.ck", "www.ck")]
    [InlineData("city.kawasaki.jp", "city.kawasaki.jp")]
    [InlineData("a.city.kawasaki.jp", "city.kawasaki.jp")]
    [InlineData("WWW.Example.COM", "example.com")]
    [InlineData("example.unlistedtld", "example.unlistedtld")]
    public void GetRegistrableDomain_FollowsTheListsAlgorithm(string domain, string expected)
    {
        Assert.Equal(expected, PublicSuffixList.GetRegistrableDomain(domain));
    }

    [Theory]
    [InlineData("co.uk")]
    [InlineData("foo.ck")]
    [InlineData("com")]
    [InlineData("")]
    [InlineData(null)]
    public void GetRegistrableDomain_APublicSuffix_IsNull(string domain)
    {
        Assert.Null(PublicSuffixList.GetRegistrableDomain(domain));
    }

    [Fact]
    public void Parse_ReadsRulesWildcardsAndExceptionsAndSkipsComments()
    {
        using var reader = new StringReader(
            """
            // A comment.
            // ===BEGIN ICANN DOMAINS===
            test

            *.wild.test
            !keep.wild.test
            // ===END ICANN DOMAINS===
            """);

        var rules = PublicSuffixList.Parse(reader);

        Assert.True(rules.IsPublicSuffix("test"));
        Assert.True(rules.IsPublicSuffix("any.wild.test"));
        Assert.False(rules.IsPublicSuffix("wild.test"));
        Assert.False(rules.IsPublicSuffix("keep.wild.test"));
        Assert.Equal("keep.wild.test", rules.GetRegistrableDomain("a.keep.wild.test"));
        Assert.Equal("b.any.wild.test", rules.GetRegistrableDomain("a.b.any.wild.test"));
        Assert.False(rules.IsPublicSuffix("a.comment"));
    }

    [Fact]
    public void EmbeddedList_IsTheFullListWithItsLicenseNotice()
    {
        var text = PublicSuffixList.ReadEmbeddedList();

        Assert.Contains("Mozilla Public", text);
        Assert.Contains("https://mozilla.org/MPL/2.0/", text);
        Assert.Contains("// ===BEGIN ICANN DOMAINS===", text);
        Assert.Contains("// ===END ICANN DOMAINS===", text);
        Assert.Contains("// ===BEGIN PRIVATE DOMAINS===", text);
        Assert.Contains("// ===END PRIVATE DOMAINS===", text);
        Assert.True(PublicSuffixList.RuleCount > 9000, $"The embedded list has only {PublicSuffixList.RuleCount} rules.");
    }
}
