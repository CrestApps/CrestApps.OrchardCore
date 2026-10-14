using CrestApps.OrchardCore.TenantHierarchy;
using CrestApps.OrchardCore.TenantHierarchy.Core;
using CrestApps.OrchardCore.TenantHierarchy.Core.Guards;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Removing;

namespace CrestApps.OrchardCore.Tests.TenantHierarchy;

public sealed class TenantHierarchyGuardsTests
{
    [Theory]
    [InlineData("/Admin", true)]
    [InlineData("/", true)]
    [InlineData("~/Admin", true)]
    [InlineData("~/", true)]
    [InlineData("//evil.example", false)]
    [InlineData("/\\evil.example", false)]
    [InlineData("~//evil.example", false)]
    [InlineData("https://evil.example", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("/Admin\r\nLocation: x", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsLocalUrl_AcceptsOnlyLocalPaths(string url, bool expected)
    {
        // Assert
        Assert.Equal(expected, TenantHierarchyUrls.IsLocalUrl(url));
    }

    [Fact]
    public void GetBaseAddress_UsesTheFirstHostAndThePrefix()
    {
        // Arrange
        var settings = TestShellSettings.Ordinary("tenant");
        settings.RequestUrlHost = "one.example.com,two.example.com";

        // Act + Assert
        Assert.Equal("https://one.example.com", TenantHierarchyUrls.GetBaseAddress(settings, "https"));

        settings.RequestUrlPrefix = "prefix";
        Assert.Equal("https://one.example.com/prefix", TenantHierarchyUrls.GetBaseAddress(settings, "https"));
        Assert.Null(TenantHierarchyUrls.GetBaseAddress(TestShellSettings.Ordinary("nohost"), "https"));
    }

    [Fact]
    public void GetScheme_PrefersConfigurationThenHttpsOutsideDevelopment()
    {
        // Arrange
        var request = new DefaultHttpContext().Request;
        request.Scheme = "http";
        var development = Mock.Of<IWebHostEnvironment>(environment => environment.EnvironmentName == Environments.Development);
        var production = Mock.Of<IWebHostEnvironment>(environment => environment.EnvironmentName == Environments.Production);

        // Assert
        Assert.Equal("http", TenantHierarchyUrls.GetScheme(new TenantHierarchyOptions { Scheme = "HTTP" }, production, request));
        Assert.Equal("https", TenantHierarchyUrls.GetScheme(new TenantHierarchyOptions(), production, request));
        Assert.Equal("http", TenantHierarchyUrls.GetScheme(new TenantHierarchyOptions(), development, request));
    }

    [Theory]
    [InlineData(null, null, true, false)]
    [InlineData("navigate", "same-site", true, false)]
    [InlineData("cors", "same-site", false, true)]
    [InlineData("no-cors", "same-site", false, true)]
    [InlineData("cors", "same-origin", false, false)]
    [InlineData("cors", "cross-site", false, false)]
    public void FetchMetadata_ClassifiesTheRequest(string mode, string site, bool isNavigation, bool isSameSiteSubresource)
    {
        // Arrange
        var request = new DefaultHttpContext().Request;

        if (mode is not null)
        {
            request.Headers[FetchMetadataPolicy.ModeHeader] = mode;
        }

        if (site is not null)
        {
            request.Headers[FetchMetadataPolicy.SiteHeader] = site;
        }

        // Assert
        Assert.Equal(isNavigation, FetchMetadataPolicy.IsNavigationOrUnknown(request));
        Assert.Equal(isSameSiteSubresource, FetchMetadataPolicy.IsSameSiteSubresource(request));
    }

    [Fact]
    public void HostPrefixedCookies_InAHierarchyTenant_GetTheHostPrefix()
    {
        // Arrange
        var setup = CreateCookieSetup(TestShellSettings.Parent("firma"), useHostPrefixedCookies: true);
        var cookie = new CookieAuthenticationOptions();
        cookie.Cookie.Name = "orchauth_firma";
        cookie.Cookie.Path = "/firma";
        var antiforgery = new AntiforgeryOptions();
        antiforgery.Cookie.Name = "orchantiforgery_firma";

        // Act
        setup.PostConfigure(IdentityConstants.ApplicationScheme, cookie);
        setup.PostConfigure(Options.DefaultName, antiforgery);

        // Assert
        Assert.Equal("__Host-orchauth_firma", cookie.Cookie.Name);
        Assert.Equal("/", cookie.Cookie.Path);
        Assert.Null(cookie.Cookie.Domain);
        Assert.Equal(CookieSecurePolicy.Always, cookie.Cookie.SecurePolicy);
        Assert.Equal("__Host-orchantiforgery_firma", antiforgery.Cookie.Name);
    }

    [Fact]
    public void HostPrefixedCookies_InAnOrdinaryTenantOrAnotherScheme_AreUntouched()
    {
        // Arrange
        var ordinary = CreateCookieSetup(TestShellSettings.Ordinary("plain"), useHostPrefixedCookies: true);
        var parent = CreateCookieSetup(TestShellSettings.Parent("firma"), useHostPrefixedCookies: true);
        var plainCookie = new CookieAuthenticationOptions();
        plainCookie.Cookie.Name = "orchauth_plain";
        var externalCookie = new CookieAuthenticationOptions();
        externalCookie.Cookie.Name = "external";

        // Act
        ordinary.PostConfigure(IdentityConstants.ApplicationScheme, plainCookie);
        parent.PostConfigure(IdentityConstants.ExternalScheme, externalCookie);

        // Assert
        Assert.Equal("orchauth_plain", plainCookie.Cookie.Name);
        Assert.Equal("external", externalCookie.Cookie.Name);
    }

    [Fact]
    public void HostPrefixedCookies_AreOffInDevelopmentUnlessConfigured()
    {
        // Arrange
        var development = Mock.Of<IHostEnvironment>(environment => environment.EnvironmentName == Environments.Development);
        var production = Mock.Of<IHostEnvironment>(environment => environment.EnvironmentName == Environments.Production);

        // Assert
        Assert.False(HostPrefixedCookieOptionsSetup.IsEnabled(new TenantHierarchyOptions(), development));
        Assert.True(HostPrefixedCookieOptionsSetup.IsEnabled(new TenantHierarchyOptions(), production));
        Assert.True(HostPrefixedCookieOptionsSetup.IsEnabled(new TenantHierarchyOptions { UseHostPrefixedCookies = true }, development));
        Assert.False(HostPrefixedCookieOptionsSetup.IsEnabled(new TenantHierarchyOptions { UseHostPrefixedCookies = false }, production));
    }

    [Fact]
    public void ParentRemovalGuard_ParentWithChildren_SetsAnError()
    {
        // Arrange
        var parent = TestShellSettings.Parent("firma");
        var child = TestShellSettings.Child("child", parent);
        var context = new ShellRemovingContext { ShellSettings = parent };

        // Act
        ParentRemovalGuard.Check(context, CreateShellHost(parent, child));

        // Assert
        Assert.False(context.Success);
        Assert.Equal(ParentRemovalGuard.ErrorMessage, context.ErrorMessage);
    }

    [Fact]
    public void ParentRemovalGuard_ParentWithoutChildrenOrAChild_IsAllowed()
    {
        // Arrange
        var parent = TestShellSettings.Parent("firma");
        var child = TestShellSettings.Child("child", parent);
        var emptyParent = new ShellRemovingContext { ShellSettings = parent };
        var removingChild = new ShellRemovingContext { ShellSettings = child };

        // Act
        ParentRemovalGuard.Check(emptyParent, CreateShellHost(parent));
        ParentRemovalGuard.Check(removingChild, CreateShellHost(parent, child));

        // Assert
        Assert.True(emptyParent.Success);
        Assert.True(removingChild.Success);
    }

    [Fact]
    public void ParentRemovalGuard_LocalResourcesOnly_IsAllowed()
    {
        // Arrange: another node already removed the parent from the shared settings.
        var parent = TestShellSettings.Parent("firma");
        var context = new ShellRemovingContext { ShellSettings = parent, LocalResourcesOnly = true };

        // Act
        ParentRemovalGuard.Check(context, CreateShellHost(parent, TestShellSettings.Child("child", parent)));

        // Assert
        Assert.True(context.Success);
    }

    [Fact]
    public async Task BrokerCallContext_IsSetOnlyDuringTheCall()
    {
        // Arrange
        string inside = null;

        // Act
        await BrokerCallContext.RunAsync("target", () =>
        {
            inside = BrokerCallContext.TargetTenantName;

            return Task.CompletedTask;
        });

        var synchronous = BrokerCallContext.Run("other", () => BrokerCallContext.TargetTenantName);

        // Assert
        Assert.Equal("target", inside);
        Assert.Equal("other", synchronous);
        Assert.Null(BrokerCallContext.TargetTenantName);
    }

    [Fact]
    public async Task BrokerCallContext_IsRestoredWhenTheCallThrows()
    {
        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => BrokerCallContext.RunAsync("target", () => throw new InvalidOperationException()));

        // Assert
        Assert.Null(BrokerCallContext.TargetTenantName);
    }

    [Fact]
    public void GuardedShellHost_OutsideAnyTenant_ForwardsEveryCall()
    {
        // Arrange
        var parent = TestShellSettings.Parent("firma");
        var inner = new Mock<IShellHost>();
        inner.Setup(host => host.GetAllSettings()).Returns([parent]);
        inner.Setup(host => host.TryGetSettings("firma", out parent)).Returns(true);
        var guarded = new GuardedShellHost(inner.Object, NullLogger<GuardedShellHost>.Instance);

        // Act
        var all = guarded.GetAllSettings().ToList();
        var found = guarded.TryGetSettings("firma", out var settings);
        guarded.ReloadShellContextAsync(parent);
        guarded.LoadingAsync = null;

        // Assert
        Assert.Single(all);
        Assert.True(found);
        Assert.Same(parent, settings);
        inner.Verify(host => host.ReloadShellContextAsync(parent, true), Times.Once);
        inner.VerifySet(host => host.LoadingAsync = null, Times.Once);
        Assert.Same(inner.Object, guarded.Inner);
    }

    private static HostPrefixedCookieOptionsSetup CreateCookieSetup(ShellSettings settings, bool useHostPrefixedCookies)
    {
        return new HostPrefixedCookieOptionsSetup(
            settings,
            Options.Create(new TenantHierarchyOptions { UseHostPrefixedCookies = useHostPrefixedCookies }),
            Mock.Of<IHostEnvironment>());
    }

    private static IShellHost CreateShellHost(params ShellSettings[] tenants)
    {
        var host = new Mock<IShellHost>();
        host.Setup(candidate => candidate.GetAllSettings()).Returns(tenants);

        return host.Object;
    }
}
