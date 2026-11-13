using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Models;
using OrchardCore.Environment.Shell;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// SEC-C2 and SEC-C18: a tenant purchase let the buyer choose any URL prefix, any host name (the operator's own
/// included) and any setup recipe on the host, and provisioned the tenant with no feature profile. The purchase is
/// now held to Orchard's tenant rules, the operator's reserved prefixes and hosts, the plan's recipe, and a required
/// feature profile.
/// </summary>
public sealed class TenantPurchaseValidatorTests
{
    private static readonly ShellSettings[] _tenants =
    [
        new ShellSettings { Name = ShellSettings.DefaultShellName, RequestUrlHost = null, RequestUrlPrefix = null },
        new ShellSettings { Name = "Acme", RequestUrlPrefix = "acme", RequestUrlHost = "acme.example.org" },
    ];

    [Fact]
    public void Validate_AValidRequest_HasNoErrors()
    {
        var errors = TenantPurchaseValidator.Validate(Request(), Rules(), _tenants);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("Admin")]
    [InlineData("login")]
    [InlineData("stripe")]
    public void Validate_AReservedPrefix_IsRefused(string prefix)
    {
        var errors = TenantPurchaseValidator.Validate(Request(prefix: prefix), Rules(allowCustomPrefix: true), _tenants);

        Assert.Contains(errors, error => error.Code == TenantPurchaseErrorCode.ReservedPrefix);
    }

    [Fact]
    public void Validate_AnEmptyPrefixWithoutADomain_IsRefused()
    {
        var errors = TenantPurchaseValidator.Validate(Request(prefix: ""), Rules(allowCustomPrefix: true), _tenants);

        Assert.Contains(errors, error => error.Code == TenantPurchaseErrorCode.PrefixRequired);
    }

    [Fact]
    public void Validate_APrefixOtherThanTheDerivedOne_IsRefusedUnlessTheOperatorAllowsAChoice()
    {
        var errors = TenantPurchaseValidator.Validate(Request(tenantName: "Shop1", prefix: "other"), Rules(), _tenants);

        Assert.Contains(errors, error => error.Code == TenantPurchaseErrorCode.PrefixNotAllowed);
    }

    [Fact]
    public void Normalize_DerivesThePrefixFromTheTenantName()
    {
        var request = TenantPurchaseValidator.Normalize(Request(tenantName: "Shop1", prefix: null), Rules());

        Assert.Equal("shop1", request.Prefix);
    }

    [Fact]
    public void Validate_TheOperatorsOwnHost_IsRefused()
    {
        var errors = TenantPurchaseValidator.Validate(
            Request(domains: ["shop.operator.com"]),
            Rules(allowCustomDomains: true, reservedHosts: ["shop.operator.com"]),
            _tenants);

        Assert.Contains(errors, error => error.Code == TenantPurchaseErrorCode.ReservedHost);
    }

    [Fact]
    public void Validate_AWildcardHost_IsRefused()
    {
        var errors = TenantPurchaseValidator.Validate(Request(domains: ["*.operator.com"]), Rules(allowCustomDomains: true), _tenants);

        Assert.Contains(errors, error => error.Code == TenantPurchaseErrorCode.InvalidHost);
    }

    [Fact]
    public void Validate_ADomainWhenCustomDomainsAreOff_IsRefused()
    {
        var errors = TenantPurchaseValidator.Validate(Request(domains: ["mine.example.net"]), Rules(), _tenants);

        Assert.Contains(errors, error => error.Code == TenantPurchaseErrorCode.CustomDomainsNotAllowed);
    }

    [Fact]
    public void Validate_AHostAnotherTenantUses_IsRefused()
    {
        var errors = TenantPurchaseValidator.Validate(Request(domains: ["acme.example.org"]), Rules(allowCustomDomains: true), _tenants);

        Assert.Contains(errors, error => error.Code == TenantPurchaseErrorCode.HostTaken);
    }

    [Fact]
    public void Validate_ARecipeThePlanDoesNotName_IsRefused()
    {
        var errors = TenantPurchaseValidator.Validate(Request(recipeName: "SaaS"), Rules(planRecipe: "Blog"), _tenants);

        Assert.Contains(errors, error => error.Code == TenantPurchaseErrorCode.RecipeNotAllowed);
    }

    [Fact]
    public void Validate_NoFeatureProfile_IsRefused()
    {
        var errors = TenantPurchaseValidator.Validate(Request(featureProfile: null), Rules(), _tenants);

        Assert.Contains(errors, error => error.Code == TenantPurchaseErrorCode.FeatureProfileRequired);
    }

    /// <summary>
    /// SEC-C18: Orchard treats a profile it can't find as "everything allowed", so a renamed or deleted profile, or a plan
    /// that arrived by recipe, import or API naming one that never existed, would sell an unrestricted site.
    /// </summary>
    [Fact]
    public void Validate_AFeatureProfileThatDoesNotExist_IsRefused()
    {
        var errors = TenantPurchaseValidator.Validate(Request(featureProfile: "deleted-profile"), Rules(), _tenants);

        Assert.Contains(errors, error => error.Code == TenantPurchaseErrorCode.FeatureProfileNotFound);
    }

    [Fact]
    public void Validate_WhenFeatureProfilesCannotBeChecked_IsRefused()
    {
        var rules = Rules();
        rules.KnownFeatureProfiles = null;

        var errors = TenantPurchaseValidator.Validate(Request(), rules, _tenants);

        Assert.Contains(errors, error => error.Code == TenantPurchaseErrorCode.FeatureProfileNotFound);
    }

    /// <summary>
    /// SEC-C2 regression: an upgraded store that sells sites on the buyer's own domain kept custom prefixes off, so the
    /// prefix was derived from the name and the buyer of <c>buyer.com</c> got <c>buyer.com/shop1</c>.
    /// </summary>
    [Fact]
    public void Normalize_ACustomDomainWithoutAPrefix_DerivesNoPrefix()
    {
        var rules = Rules(allowCustomDomains: true);
        var request = TenantPurchaseValidator.Normalize(Request(prefix: null, domains: ["buyer.com"]), rules);

        Assert.True(string.IsNullOrEmpty(request.Prefix));
        Assert.Empty(TenantPurchaseValidator.Validate(request, rules, _tenants));
    }

    /// <summary>
    /// The step says "Leave blank to use the site name" where the buyer may choose a prefix, and a blank one was refused.
    /// </summary>
    [Fact]
    public void Normalize_ABlankPrefixWhereTheBuyerMayChoose_IsTheSiteName()
    {
        var rules = Rules(allowCustomPrefix: true);
        var request = TenantPurchaseValidator.Normalize(Request(prefix: null), rules);

        Assert.Equal("shop1", request.Prefix);
        Assert.Empty(TenantPurchaseValidator.Validate(request, rules, _tenants));
    }

    /// <summary>
    /// SEC-C2: with a base domain the name becomes a host label, so a name such as <c>admin</c> or <c>www</c> would take
    /// <c>admin.{base domain}</c>.
    /// </summary>
    [Theory]
    [InlineData("admin")]
    [InlineData("login")]
    [InlineData("www")]
    public void Validate_AReservedNameAsAHostLabel_IsRefused(string tenantName)
    {
        var rules = Rules(baseDomain: "sites.operator.com");
        var request = TenantPurchaseValidator.Normalize(Request(tenantName: tenantName, prefix: null), rules);

        var errors = TenantPurchaseValidator.Validate(request, rules, _tenants);

        Assert.Contains(errors, error => error.Code == TenantPurchaseErrorCode.ReservedHost);
    }

    /// <summary>
    /// SEC-C2: the operator's domains are reserved with every name under them, so a buyer who checks out through one alias
    /// can't take another (<c>www.operator.com</c>, <c>shop.operator.com</c>).
    /// </summary>
    [Theory]
    [InlineData("operator.com")]
    [InlineData("www.operator.com")]
    [InlineData("shop.operator.com")]
    public void Validate_AHostUnderTheOperatorsDomain_IsRefused(string domain)
    {
        var errors = TenantPurchaseValidator.Validate(
            Request(domains: [domain]),
            Rules(allowCustomDomains: true, reservedHosts: ["operator.com"]),
            _tenants);

        Assert.Contains(errors, error => error.Code == TenantPurchaseErrorCode.ReservedHost);
    }

    [Fact]
    public void Validate_TheDerivedHostUnderAReservedBaseDomain_IsAllowed()
    {
        var rules = Rules(baseDomain: "operator.com", reservedHosts: ["operator.com"]);
        var request = TenantPurchaseValidator.Normalize(Request(prefix: null), rules);

        Assert.Empty(TenantPurchaseValidator.Validate(request, rules, _tenants));
    }

    [Theory]
    [InlineData("Default")]
    [InlineData("Acme")]
    [InlineData("1site")]
    [InlineData("bad-name")]
    public void Validate_AnInvalidOrTakenTenantName_IsRefused(string tenantName)
    {
        var errors = TenantPurchaseValidator.Validate(Request(tenantName: tenantName), Rules(allowCustomPrefix: true), _tenants);

        Assert.Contains(errors, error => error.Field == nameof(TenantPurchaseRequest.TenantName));
    }

    [Fact]
    public void Normalize_WithABaseDomain_DerivesTheHost()
    {
        var request = TenantPurchaseValidator.Normalize(Request(tenantName: "Shop1", prefix: null), Rules(baseDomain: "sites.operator.com"));

        Assert.Equal(["shop1.sites.operator.com"], request.Domains);
        Assert.True(string.IsNullOrEmpty(request.Prefix));
        Assert.Empty(TenantPurchaseValidator.Validate(request, Rules(baseDomain: "sites.operator.com"), _tenants));
    }

    private static TenantPurchaseRequest Request(
        string tenantName = "Shop1",
        string prefix = "shop1",
        string[] domains = null,
        string recipeName = "Blog",
        string featureProfile = "sold-tenant")
        => new()
        {
            TenantName = tenantName,
            Prefix = prefix,
            Domains = domains ?? [],
            RecipeName = recipeName,
            FeatureProfile = featureProfile,
        };

    private static TenantPurchaseRules Rules(
        bool allowCustomPrefix = false,
        bool allowCustomDomains = false,
        string[] reservedHosts = null,
        string planRecipe = "Blog",
        string baseDomain = null)
        => new()
        {
            PlanRecipeName = planRecipe,
            AllowCustomPrefix = allowCustomPrefix,
            AllowCustomDomains = allowCustomDomains,
            ReservedPrefixes = SubscriptionOnboardingSettings.DefaultReservedPrefixes,
            ReservedHosts = reservedHosts ?? [],
            BaseDomain = baseDomain,
            AdminPrefix = "Admin",
            KnownFeatureProfiles = ["sold-tenant"],
        };
}

/// <summary>
/// SEC-C2's upgrade: a store that already sold sites with their own domains or prefixes keeps offering them.
/// </summary>
public sealed class SubscriptionOnboardingUpgradeTests
{
    [Fact]
    public void Apply_ACompletedSiteWithDomains_KeepsCustomDomainsOn()
    {
        var settings = new SubscriptionOnboardingSettings { ReservedHosts = ["operator.com"] };

        var changed = SubscriptionOnboardingUpgrade.Apply(settings,
        [
            new() { TenantName = "Shop1", Prefix = "", Domains = ["shop1.example.net"], Status = TenantProvisioningStatus.Succeeded },
        ]);

        Assert.True(changed);
        Assert.True(settings.AllowCustomDomains);
        Assert.False(settings.AllowCustomPrefix);
    }

    /// <summary>
    /// SEC-C2: the settings editor refuses custom domains while no reserved host is listed, because the default tenant
    /// normally has no host names and the operator's other aliases are known only from that list. The upgrade turned them
    /// on regardless.
    /// </summary>
    [Fact]
    public void Apply_ACompletedSiteWithDomains_LeavesCustomDomainsOff_WhileNoReservedHostIsListed()
    {
        var settings = new SubscriptionOnboardingSettings();

        SubscriptionOnboardingUpgrade.Apply(settings,
        [
            new() { TenantName = "Shop1", Prefix = "", Domains = ["shop1.example.net"], Status = TenantProvisioningStatus.Succeeded },
        ]);

        Assert.False(settings.AllowCustomDomains);
    }

    /// <summary>
    /// SEC-C2's scenario: the store answers on operator.com and on its platform host name, and Base URL is unset or names
    /// the first. A buyer who checks out through operator.com and enters the platform host name matched no reserved host,
    /// and the new tenant took it. Custom domains set on (by a recipe, an import, or the upgrade) with no reserved host
    /// listed are refused at run time.
    /// </summary>
    [Fact]
    public void Validate_ACustomDomain_IsRefused_WhileNoReservedHostIsListed()
    {
        var settings = new SubscriptionOnboardingSettings { AllowCustomDomains = true };
        var rules = settings.ToRules("Blog", "Admin", ["operator.com"]);
        rules.KnownFeatureProfiles = ["sold-tenant"];

        var request = TenantPurchaseValidator.Normalize(
            new TenantPurchaseRequest { TenantName = "Shop1", Domains = ["platform-host.example.net"], RecipeName = "Blog", FeatureProfile = "sold-tenant" },
            rules);

        var error = Assert.Single(TenantPurchaseValidator.Validate(request, rules, []));

        Assert.Equal(TenantPurchaseErrorCode.CustomDomainsNotAllowed, error.Code);
    }

    [Fact]
    public void Apply_ACompletedSiteWithItsOwnPrefix_KeepsCustomPrefixesOn()
    {
        var settings = new SubscriptionOnboardingSettings();

        SubscriptionOnboardingUpgrade.Apply(settings,
        [
            new() { TenantName = "Shop1", Prefix = "my-shop", Status = TenantProvisioningStatus.Succeeded },
        ]);

        Assert.True(settings.AllowCustomPrefix);
    }

    [Fact]
    public void Apply_OnlyFailedJobsOrDerivedPrefixes_ChangesNothing()
    {
        var settings = new SubscriptionOnboardingSettings();

        var changed = SubscriptionOnboardingUpgrade.Apply(settings,
        [
            new() { TenantName = "Shop1", Prefix = "shop1", Status = TenantProvisioningStatus.Succeeded },
            new() { TenantName = "Shop2", Domains = ["x.example.net"], Status = TenantProvisioningStatus.Failed },
        ]);

        Assert.False(changed);
        Assert.False(settings.AllowCustomDomains);
        Assert.False(settings.AllowCustomPrefix);
    }
}
