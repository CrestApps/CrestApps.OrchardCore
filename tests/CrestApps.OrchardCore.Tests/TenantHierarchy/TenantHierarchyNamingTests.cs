using CrestApps.OrchardCore.TenantHierarchy.Core.Services;

namespace CrestApps.OrchardCore.Tests.TenantHierarchy;

public sealed class TenantHierarchyNamingTests
{
    [Theory]
    [InlineData("business1")]
    [InlineData("abc")]
    [InlineData("north-wind-traders")]
    [InlineData("a1b2c3")]
    public void ValidateSlug_ValidDnsLabel_IsValid(string slug)
    {
        // Act
        var result = TenantHierarchyNaming.ValidateSlug(slug);

        // Assert
        Assert.Equal(SlugValidationResult.Valid, result);
    }

    [Theory]
    [InlineData(null, SlugValidationResult.Empty)]
    [InlineData("", SlugValidationResult.Empty)]
    [InlineData("   ", SlugValidationResult.Empty)]
    [InlineData("ab", SlugValidationResult.TooShort)]
    [InlineData("a234567890123456789012345678901234567890x", SlugValidationResult.TooLong)]
    [InlineData("-abc", SlugValidationResult.InvalidCharacters)]
    [InlineData("abc-", SlugValidationResult.InvalidCharacters)]
    [InlineData("ABC", SlugValidationResult.InvalidCharacters)]
    [InlineData("a_bc", SlugValidationResult.InvalidCharacters)]
    [InlineData("a.bc", SlugValidationResult.InvalidCharacters)]
    [InlineData("a bc", SlugValidationResult.InvalidCharacters)]
    [InlineData("*.abc", SlugValidationResult.InvalidCharacters)]
    [InlineData("xn--abc", SlugValidationResult.InvalidCharacters)]
    [InlineData("admin", SlugValidationResult.Reserved)]
    [InlineData("www", SlugValidationResult.Reserved)]
    [InlineData("default", SlugValidationResult.Reserved)]
    public void ValidateSlug_InvalidOrReserved_ReturnsTheReason(string slug, SlugValidationResult expected)
    {
        // Act
        var result = TenantHierarchyNaming.ValidateSlug(slug);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ValidateSlug_ConfiguredReservedSlug_IsReserved()
    {
        // Act
        var result = TenantHierarchyNaming.ValidateSlug("billing", ["Billing"]);

        // Assert
        Assert.Equal(SlugValidationResult.Reserved, result);
    }

    [Theory]
    [InlineData("Northwind Traders", "northwind-traders")]
    [InlineData("  Café  Crème & Co. ", "cafe-creme-co")]
    [InlineData("ACME 2026", "acme-2026")]
    [InlineData("---", "")]
    [InlineData(null, "")]
    public void SuggestSlug_DisplayName_ReturnsLowercaseHyphenatedSlug(string displayName, string expected)
    {
        // Act
        var slug = TenantHierarchyNaming.SuggestSlug(displayName);

        // Assert
        Assert.Equal(expected, slug);
    }

    [Fact]
    public void SuggestSlug_LongName_IsCutToTheMaximumLength()
    {
        // Act
        var slug = TenantHierarchyNaming.SuggestSlug(new string('a', 100));

        // Assert
        Assert.True(slug.Length <= TenantHierarchyNaming.MaxSlugLength);
    }

    [Theory]
    [InlineData("{business}.firma.platform.com", true)]
    [InlineData("{business}.firma.localhost:5320", true)]
    [InlineData("{business}.clients.example.org", true)]
    [InlineData("firma.platform.com", false)]
    [InlineData("{business}-firma.platform.com", false)]
    [InlineData("www.{business}.platform.com", false)]
    [InlineData("{business}.{business}.platform.com", false)]
    [InlineData("{business}.*.platform.com", false)]
    [InlineData("{business}.platform.com,other.com", false)]
    [InlineData("{business}.platform.com/path", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidHostPattern_ReturnsWhetherThePatternIsSafe(string pattern, bool expected)
    {
        // Act
        var valid = TenantHierarchyNaming.IsValidHostPattern(pattern);

        // Assert
        Assert.Equal(expected, valid);
    }

    [Fact]
    public void BuildHost_ReplacesThePlaceholder()
    {
        // Act
        var host = TenantHierarchyNaming.BuildHost("{business}.firma.platform.com", "business1");

        // Assert
        Assert.Equal("business1.firma.platform.com", host);
    }

    [Fact]
    public void GetDefaultHostPattern_PrefixesTheParentHost()
    {
        // Act
        var pattern = TenantHierarchyNaming.GetDefaultHostPattern("firma.platform.com");

        // Assert
        Assert.Equal("{business}.firma.platform.com", pattern);
        Assert.True(TenantHierarchyNaming.IsValidHostPattern(pattern));
    }

    [Theory]
    [InlineData("platform.com", "firma.platform.com")]
    [InlineData(".platform.com", "firma.platform.com")]
    [InlineData("localhost:5320", "firma.localhost:5320")]
    public void BuildParentHost_JoinsSlugAndPlatformDomain(string domain, string expected)
    {
        // Act
        var host = TenantHierarchyNaming.BuildParentHost("firma", domain);

        // Assert
        Assert.Equal(expected, host);
    }

    [Theory]
    [InlineData("firma.platform.com", true)]
    [InlineData("localhost:5320", true)]
    [InlineData("a-b.c", true)]
    [InlineData("firma.platform.com:0", false)]
    [InlineData("firma.platform.com:70000", false)]
    [InlineData("-firma.com", false)]
    [InlineData("firma..com", false)]
    [InlineData("*.com", false)]
    [InlineData("firma.com/x", false)]
    [InlineData("a b.com", false)]
    public void IsValidHost_ReturnsWhetherTheValueIsOneHost(string host, bool expected)
    {
        // Act
        var valid = TenantHierarchyNaming.IsValidHost(host);

        // Assert
        Assert.Equal(expected, valid);
    }

    [Fact]
    public void GenerateTenantName_IsOpaqueAndUnique()
    {
        // Act
        var names = Enumerable.Range(0, 200).Select(_ => TenantHierarchyNaming.GenerateTenantName()).ToList();

        // Assert
        Assert.All(names, name => Assert.Matches("^u_[a-z0-9]{10}$", name));
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Theory]
    [InlineData("alice", "firma", 1, "alice+firma")]
    [InlineData("alice", "firma", 3, "alice+firma-3")]
    [InlineData("al ice@mail", "fir/ma", 1, "alicemail+firma")]
    [InlineData("", "firma", 1, "user+firma")]
    [InlineData("alice", null, 1, "alice")]
    public void BuildLinkedUserName_KeepsTheAllowedCharacters(string userName, string slug, int attempt, string expected)
    {
        // Act
        var result = TenantHierarchyNaming.BuildLinkedUserName(userName, slug, attempt);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("firma", "firma.platform.com", "platform.com", "firma")]
    [InlineData("firma", "Contoso.Platform.com", "platform.com", "contoso")]
    [InlineData("firma", "firma.localhost:5000", "localhost:5000", "firma")]
    [InlineData("Contoso Books", "contoso.example.org", "platform.com", "contoso-books")]
    [InlineData("Contoso Books", null, "platform.com", "contoso-books")]
    [InlineData("Contoso Books", "deep.firma.platform.com", "platform.com", "contoso-books")]
    [InlineData("Contoso Books", "www.platform.com", "platform.com", "contoso-books")]
    public void SuggestParentSlug_KeepsTheCurrentAddressWhenItIsUnderThePlatformDomain(string tenantName, string currentHost, string platformDomain, string expected)
    {
        // Act
        var slug = TenantHierarchyNaming.SuggestParentSlug(tenantName, currentHost, platformDomain);

        // Assert
        Assert.Equal(expected, slug);
    }
}
