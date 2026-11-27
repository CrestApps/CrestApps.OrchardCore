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
    [InlineData("autodiscover")]
    [InlineData("owa")]
    [InlineData("ews")]
    [InlineData("wpad")]
    [InlineData("isatap")]
    [InlineData("localhost")]
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
    [InlineData("wpad")]
    [InlineData("isatap")]
    [InlineData("localhost")]
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

    /// <summary>
    /// Round 3 (Low): a site's name is its folder's name, so a Windows device name (a folder that can't be created or
    /// removed there) is refused, and so is a name longer than a host label.
    /// </summary>
    [Theory]
    [InlineData("CON")]
    [InlineData("nul")]
    [InlineData("Aux")]
    [InlineData("LPT1")]
    [InlineData("com9")]
    [InlineData("A123456789012345678901234567890123456789012345678901234567890123")]
    public void Validate_ADeviceNameOrAnOverlongTenantName_IsInvalid(string tenantName)
    {
        // Act
        var errors = TenantPurchaseValidator.Validate(Request(tenantName: tenantName, prefix: "shop1"), Rules(allowCustomPrefix: true), _tenants);

        // Assert
        Assert.Contains(errors, error => error.Code == TenantPurchaseErrorCode.InvalidName);
    }

    [Fact]
    public void Validate_ATenantNameAsLongAsAHostLabel_IsValid()
    {
        // Act
        var errors = TenantPurchaseValidator.Validate(Request(tenantName: "A" + new string('1', TenantPurchaseValidator.MaxNameLength - 1), prefix: "shop1"), Rules(allowCustomPrefix: true), _tenants);

        // Assert
        Assert.DoesNotContain(errors, error => error.Field == nameof(TenantPurchaseRequest.TenantName));
    }

    [Fact]
    public void Normalize_WithABaseDomain_DerivesTheHost()
    {
        var request = TenantPurchaseValidator.Normalize(Request(tenantName: "Shop1", prefix: null), Rules(baseDomain: "sites.operator.com"));

        Assert.Equal(["shop1.sites.operator.com"], request.Domains);
        Assert.True(string.IsNullOrEmpty(request.Prefix));
        Assert.Empty(TenantPurchaseValidator.Validate(request, Rules(baseDomain: "sites.operator.com"), _tenants));
    }

    /// <summary>
    /// M12 (final security review): an IP address passed the host pattern, so requests addressed to the server by address,
    /// health probes included, went to the buyer's site.
    /// </summary>
    [Theory]
    [InlineData("203.0.113.10")]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    public void Validate_AnIpAddress_IsRefused(string domain)
    {
        var errors = TenantPurchaseValidator.Validate(Request(domains: [domain]), Rules(allowCustomDomains: true), _tenants);

        Assert.Contains(errors, error => error.Code == TenantPurchaseErrorCode.InvalidHost);
    }

    /// <summary>
    /// M12: a name under another customer's domain, or a parent of it, was free to take, so a buyer could serve
    /// <c>www.v.com</c> beside customer V's <c>v.com</c>.
    /// </summary>
    [Theory]
    [InlineData("www.acme.example.org")]
    [InlineData("example.org")]
    public void Validate_ANameUnderOrAboveAnotherTenantsHost_IsRefused(string domain)
    {
        var errors = TenantPurchaseValidator.Validate(Request(domains: [domain]), Rules(allowCustomDomains: true), _tenants);

        Assert.Contains(errors, error => error.Code == TenantPurchaseErrorCode.HostTaken);
    }

    /// <summary>
    /// Selling-sites Mild (fix verification): with the under/above rule, one purchase listing a public suffix blocked every
    /// later buyer under it. A public suffix is not a domain anybody owns, so it is refused.
    /// </summary>
    [Theory]
    [InlineData("co.uk")]
    [InlineData("CO.UK")]
    [InlineData("github.io")]
    [InlineData("azurewebsites.net")]
    [InlineData("com.au")]
    [InlineData("foo.ck")]
    [InlineData("ec2.compute.amazonaws.com")]
    [InlineData("xn--55qx5d.cn")]
    public void Validate_APublicSuffix_IsRefused(string domain)
    {
        var errors = TenantPurchaseValidator.Validate(Request(domains: [domain.ToLowerInvariant()]), Rules(allowCustomDomains: true), _tenants);

        Assert.Contains(errors, error => error.Code == TenantPurchaseErrorCode.InvalidHost);
    }

    [Theory]
    [InlineData("myshop.co.uk")]
    [InlineData("myshop.github.io")]
    [InlineData("shop.foo.ck")]
    [InlineData("www.ck")]
    public void Validate_ANameUnderAPublicSuffixBesideAnotherTenants_IsAllowed(string domain)
    {
        ShellSettings[] tenants = [.. _tenants, new ShellSettings { Name = "Other", RequestUrlHost = "other.co.uk,other.github.io" }];

        var errors = TenantPurchaseValidator.Validate(Request(domains: [domain]), Rules(allowCustomDomains: true), tenants);

        Assert.Empty(errors);
    }

    /// <summary>
    /// Selling-sites Mild (fix verification): the operator's hosts were reserved with every name under them, but not with
    /// the names above them, so a buyer could take <c>operator.com</c> beside the store's <c>shop.operator.com</c>.
    /// </summary>
    [Theory]
    [InlineData("operator.com")]
    [InlineData("eu.operator.com")]
    public void Validate_AParentOfTheOperatorsHost_IsRefused(string domain)
    {
        var errors = TenantPurchaseValidator.Validate(
            Request(domains: [domain]),
            Rules(allowCustomDomains: true, reservedHosts: ["shop.eu.operator.com"]),
            _tenants);

        Assert.Contains(errors, error => error.Code == TenantPurchaseErrorCode.ReservedHost);
    }

    /// <summary>
    /// Review 11 (reserved hosts): the store answers on <c>shop.eu.operator.com</c>, so every name under its registrable
    /// domain <c>operator.com</c> is the operator's, the siblings of its host included (<c>mail.operator.com</c>). A public
    /// suffix shared by unrelated customers holds only the store's own name (<c>other.azurewebsites.net</c> stays free).
    /// </summary>
    [Theory]
    [InlineData("mail.operator.com", true)]
    [InlineData("autodiscover.operator.com", true)]
    [InlineData("sso.eu.operator.com", true)]
    [InlineData("other.azurewebsites.net", false)]
    public void Validate_ASiblingUnderTheOperatorsRegistrableDomain_IsRefused(string domain, bool refused)
    {
        var errors = TenantPurchaseValidator.Validate(
            Request(domains: [domain]),
            Rules(allowCustomDomains: true, reservedHosts: ["shop.eu.operator.com", "store.azurewebsites.net"]),
            _tenants);

        Assert.Equal(refused, errors.Any(error => error.Code == TenantPurchaseErrorCode.ReservedHost));
    }

    /// <summary>
    /// Review 11 (reserved hosts): with a base domain the site name is a host label, so the labels mail, sign-in and hosting
    /// panel services answer on are held back too (<c>autodiscover.{base domain}</c> would receive mail clients' set-up
    /// requests).
    /// </summary>
    [Theory]
    [InlineData("mail")]
    [InlineData("autodiscover")]
    [InlineData("sso")]
    [InlineData("cpanel")]
    [InlineData("smtp")]
    public void Validate_AnInfrastructureLabelAsAHostLabel_IsRefused(string tenantName)
    {
        var rules = Rules(baseDomain: "sites.operator.com");
        var request = TenantPurchaseValidator.Normalize(Request(tenantName: tenantName, prefix: null), rules);

        var errors = TenantPurchaseValidator.Validate(request, rules, _tenants);

        Assert.Contains(errors, error => error.Code == TenantPurchaseErrorCode.ReservedHost);
    }

    /// <summary>
    /// Review 11 (reserved hosts): the base domain is the operator's. A buyer's own domain may not be the base domain itself,
    /// a name under it (which another buyer's site name would derive), or a name above it.
    /// </summary>
    [Theory]
    [InlineData("sites.operator.com")]
    [InlineData("shop2.sites.operator.com")]
    [InlineData("operator.com")]
    public void Validate_TheBaseDomainAsACustomDomain_IsRefused(string domain)
    {
        var rules = Rules(allowCustomDomains: true, reservedHosts: ["store.example.net"], baseDomain: "sites.operator.com");

        var errors = TenantPurchaseValidator.Validate(Request(domains: [domain]), rules, _tenants);

        Assert.Contains(errors, error => error.Code == TenantPurchaseErrorCode.ReservedHost);
    }

    /// <summary>
    /// Review 11 C6: Orchard splits a tenant's profile setting on commas, and a part it can't find allows every feature, so a
    /// profile whose name holds a comma can never restrict a site.
    /// </summary>
    [Fact]
    public void Validate_AFeatureProfileWithAComma_IsRefused()
    {
        var rules = Rules();
        rules.KnownFeatureProfiles = ["sold,tenant"];

        var errors = TenantPurchaseValidator.Validate(Request(featureProfile: "sold,tenant"), rules, _tenants);

        Assert.Contains(errors, error => error.Code == TenantPurchaseErrorCode.FeatureProfileNotFound);
    }

    /// <summary>
    /// Selling-sites Mild (fix verification): a tenant answering on <c>*.wild.example.com</c> answers every name under it,
    /// but a buyer could still take <c>a.wild.example.com</c>.
    /// </summary>
    [Theory]
    [InlineData("a.wild.example.com")]
    [InlineData("b.a.wild.example.com")]
    [InlineData("wild.example.com")]
    public void Validate_ANameUnderAnotherTenantsWildcardHost_IsRefused(string domain)
    {
        ShellSettings[] tenants = [.. _tenants, new ShellSettings { Name = "Wild", RequestUrlHost = "*.wild.example.com" }];

        var errors = TenantPurchaseValidator.Validate(Request(domains: [domain]), Rules(allowCustomDomains: true), tenants);

        Assert.Contains(errors, error => error.Code == TenantPurchaseErrorCode.HostTaken);
    }

    /// <summary>
    /// Selling-sites Mild (fix verification): the under/above rule held a buyer's own site against them, so the owner of
    /// <c>acme.example.org</c> could not buy a second site on <c>shop.acme.example.org</c>. The exact host is still taken.
    /// </summary>
    [Fact]
    public void Validate_ANameBesideTheBuyersOwnSite_IsAllowed_ButItsExactHostIsNot()
    {
        var beside = TenantPurchaseValidator.Validate(Request(domains: ["shop.acme.example.org"]), Rules(allowCustomDomains: true), _tenants, ownedTenants: ["Acme"]);
        var exact = TenantPurchaseValidator.Validate(Request(domains: ["acme.example.org"]), Rules(allowCustomDomains: true), _tenants, ownedTenants: ["Acme"]);

        Assert.Empty(beside);
        Assert.Contains(exact, error => error.Code == TenantPurchaseErrorCode.HostTaken);
    }

    [Fact]
    public void Validate_MoreDomainsThanTheCap_IsRefused()
    {
        var domains = Enumerable.Range(1, TenantPurchaseValidator.DefaultMaxDomains + 1).Select(index => $"site{index}.example.net").ToArray();

        var errors = TenantPurchaseValidator.Validate(Request(domains: domains), Rules(allowCustomDomains: true), _tenants);

        Assert.Contains(errors, error => error.Code == TenantPurchaseErrorCode.TooManyDomains);
    }

    /// <summary>
    /// M10 (final security review): a site on a prefix of the store's own address runs in the store's origin. New stores
    /// don't sell it unless the operator turns it on; a site on a base domain or the buyer's own domain is unaffected.
    /// </summary>
    [Fact]
    public void Validate_APrefixSiteOnTheStoresAddress_IsRefusedUnlessTheOperatorAllowsIt()
    {
        var refused = new SubscriptionOnboardingSettings().ToRules("Blog", "Admin", []);
        refused.KnownFeatureProfiles = ["sold-tenant"];
        var allowed = new SubscriptionOnboardingSettings { AllowPrefixOnStoreHost = true }.ToRules("Blog", "Admin", []);
        allowed.KnownFeatureProfiles = ["sold-tenant"];
        var baseDomain = new SubscriptionOnboardingSettings { BaseDomain = "sites.operator.com" }.ToRules("Blog", "Admin", []);
        baseDomain.KnownFeatureProfiles = ["sold-tenant"];

        Assert.Contains(TenantPurchaseValidator.Validate(Request(), refused, _tenants), error => error.Code == TenantPurchaseErrorCode.OwnHostRequired);
        Assert.Empty(TenantPurchaseValidator.Validate(Request(), allowed, _tenants));
        Assert.Empty(TenantPurchaseValidator.Validate(TenantPurchaseValidator.Normalize(Request(prefix: null), baseDomain), baseDomain, _tenants));
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

    /// <summary>
    /// M10 and review 11 C5: a store that sold sites on a prefix of its own address keeps doing so after the upgrade, as a
    /// choice visible in its settings; one that sells only on its base domain, or never sold such a site, is left as it is.
    /// </summary>
    [Fact]
    public void KeepPrefixSitesOnStoreHost_KeepsWhatTheStoreSold()
    {
        TenantProvisioningJob[] prefixSold = [new() { TenantName = "Shop1", Prefix = "shop1", Status = TenantProvisioningStatus.Succeeded }];
        var prefixStore = new SubscriptionOnboardingSettings();
        var customDomainStore = new SubscriptionOnboardingSettings { BaseDomain = "sites.operator.com", AllowCustomDomains = true };
        var baseDomainStore = new SubscriptionOnboardingSettings { BaseDomain = "sites.operator.com" };
        var neverSoldStore = new SubscriptionOnboardingSettings();

        Assert.True(SubscriptionOnboardingUpgrade.KeepPrefixSitesOnStoreHost(prefixStore, prefixSold));
        Assert.True(prefixStore.AllowPrefixOnStoreHost);
        Assert.True(SubscriptionOnboardingUpgrade.KeepPrefixSitesOnStoreHost(customDomainStore, prefixSold));
        Assert.False(SubscriptionOnboardingUpgrade.KeepPrefixSitesOnStoreHost(baseDomainStore, prefixSold));
        Assert.False(baseDomainStore.AllowPrefixOnStoreHost);
        Assert.False(SubscriptionOnboardingUpgrade.KeepPrefixSitesOnStoreHost(neverSoldStore, []));
        Assert.False(neverSoldStore.AllowPrefixOnStoreHost);
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
