using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Tests.Learning.Assessments;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

/// <summary>
/// The package content host: which host answers which path, the policies the package files carry in each mode, and
/// the player origin the launch token carries to the content host.
/// </summary>
public sealed class PackageContentHostTests
{
    private const string ContentOrigin = "https://packages.example.com";

    [Theory]
    [InlineData("https://packages.example.com", "https://packages.example.com")]
    [InlineData("https://Packages.Example.com/", "https://packages.example.com")]
    [InlineData("https://packages.example.com:443", "https://packages.example.com")]
    [InlineData("http://127.0.0.1:5138", "http://127.0.0.1:5138")]
    [InlineData("  https://packages.example.com:8443/  ", "https://packages.example.com:8443")]
    public void TryNormalizeOrigin_KeepsTheSchemeHostAndPortOnly(string url, string expected)
    {
        Assert.True(PackageContentHost.TryNormalizeOrigin(url, out var origin));
        Assert.Equal(expected, origin);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("packages.example.com")]
    [InlineData("/packages")]
    [InlineData("ftp://packages.example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://packages.example.com/path")]
    [InlineData("https://packages.example.com/?a=b")]
    [InlineData("https://packages.example.com/#top")]
    [InlineData("https://user:secret@packages.example.com")]
    [InlineData("https://packages.example.com; frame-ancestors *")]
    public void TryNormalizeOrigin_RefusesAnythingButAnOrigin(string url)
    {
        Assert.False(PackageContentHost.TryNormalizeOrigin(url, out var origin));
        Assert.Null(origin);
    }

    [Theory]
    [InlineData("packages.example.com", true)]
    [InlineData("PACKAGES.example.com", true)]
    [InlineData("packages.example.com:443", true)]
    [InlineData("packages.example.com:8443", false)]
    [InlineData("learn.example.com", false)]
    [InlineData("packages.example.com.evil.test", false)]
    public void IsContentHost_ComparesTheHostAndPort(string host, bool expected)
    {
        Assert.Equal(expected, PackageContentHost.IsContentHost(ContentOrigin, new HostString(host)));
    }

    [Fact]
    public void IsContentHost_IsFalseWithoutAContentHost()
    {
        Assert.False(PackageContentHost.IsContentHost(null, new HostString("packages.example.com")));
    }

    [Theory]
    [InlineData("/learning/packages/run/token/index.html", true)]
    [InlineData("/learning/packages/frame/token", true)]
    [InlineData("/Learning/Packages/Run/token/index.html", true)]
    [InlineData("/learning/packages/runtime/e1/i1", false)]
    [InlineData("/learning/packages/i1/launch", false)]
    [InlineData("/learning/cmi5/return", false)]
    [InlineData("/Admin", false)]
    [InlineData("/", false)]
    public void IsPackagePath_MatchesThePackageFileAndFramePathsOnly(string path, bool expected)
    {
        Assert.Equal(expected, PackageContentHost.IsPackagePath(new PathString(path)));
    }

    [Theory]
    // Without a content host: the files are on the site's own host, and the frame page does not exist.
    [InlineData(null, "learn.example.com", "/learning/packages/run/t/index.html", PackageHostRoute.Site)]
    [InlineData(null, "learn.example.com", "/learning/packages/frame/t", PackageHostRoute.Refused)]
    [InlineData(null, "learn.example.com", "/Admin", PackageHostRoute.Site)]

    // With one: the package files and the frame page only on the content host, the site only on its other hosts.
    [InlineData(ContentOrigin, "packages.example.com", "/learning/packages/run/t/index.html", PackageHostRoute.PackageContent)]
    [InlineData(ContentOrigin, "packages.example.com", "/learning/packages/frame/t", PackageHostRoute.PackageContent)]
    [InlineData(ContentOrigin, "packages.example.com", "/Admin", PackageHostRoute.Refused)]
    [InlineData(ContentOrigin, "packages.example.com", "/", PackageHostRoute.Refused)]
    [InlineData(ContentOrigin, "packages.example.com", "/Login", PackageHostRoute.Refused)]
    [InlineData(ContentOrigin, "packages.example.com", "/learning/packages/i1/launch", PackageHostRoute.Refused)]
    [InlineData(ContentOrigin, "packages.example.com", "/learning/packages/runtime/e1/i1", PackageHostRoute.Refused)]
    [InlineData(ContentOrigin, "packages.example.com", "/api/content/x", PackageHostRoute.Refused)]
    [InlineData(ContentOrigin, "learn.example.com", "/learning/packages/run/t/index.html", PackageHostRoute.Refused)]
    [InlineData(ContentOrigin, "learn.example.com", "/learning/packages/frame/t", PackageHostRoute.Refused)]
    [InlineData(ContentOrigin, "learn.example.com", "/learning/packages/i1/launch", PackageHostRoute.Site)]
    [InlineData(ContentOrigin, "learn.example.com", "/Admin", PackageHostRoute.Site)]
    public void Route_ServesPackageFilesOnlyOnTheContentHost_AndTheSiteNeverThere(string contentOrigin, string host, string path, PackageHostRoute expected)
    {
        Assert.Equal(expected, PackageContentHost.Route(contentOrigin, new HostString(host), new PathString(path)));
    }

    [Fact]
    public void StrictPolicy_HasNoSameOrigin()
    {
        Assert.Equal("sandbox allow-scripts allow-forms allow-popups", PackageContentHost.StrictSandboxPolicy);
        Assert.Equal("allow-scripts allow-forms allow-popups", PackageContentHost.StrictFrameSandbox);
    }

    [Fact]
    public void ContentHostPolicy_KeepsTheFramesOfAPackageTogether_AndOnlyTheSiteMayFrameThem()
    {
        var policy = PackageContentHost.ContentHostPolicy("https://learn.example.com");

        Assert.Equal(
            "sandbox allow-scripts allow-same-origin allow-forms allow-popups allow-popups-to-escape-sandbox; frame-ancestors 'self' https://learn.example.com",
            policy);
        Assert.Equal("allow-scripts allow-same-origin allow-forms allow-popups allow-popups-to-escape-sandbox", PackageContentHost.ContentHostFrameSandbox);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://learn.example.com; script-src *")]
    [InlineData("https://learn.example.com/path")]
    [InlineData("*")]
    public void ContentHostPolicy_WithoutAValidPlayerOrigin_LetsOnlyTheContentHostFrameThem(string playerOrigin)
    {
        Assert.Equal(
            "sandbox allow-scripts allow-same-origin allow-forms allow-popups allow-popups-to-escape-sandbox; frame-ancestors 'self'",
            PackageContentHost.ContentHostPolicy(playerOrigin));
    }

    [Fact]
    public void Token_CarriesThePlayerOrigin_ToTheContentHost()
    {
        var tokens = new PackageLaunchTokens(new EphemeralDataProtectionProvider(), new TestClock { UtcNow = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc) });
        var launch = new PackageLaunch("launch1", "enrollment1", "package1", "file1", "user1") { PlayerOrigin = "https://learn.example.com" };

        var read = tokens.Read(tokens.Create(launch));

        Assert.Equal(launch, read);
        Assert.Equal("https://learn.example.com", read.PlayerOrigin);
    }

    [Fact]
    public void Token_WithoutAPlayerOrigin_ReadsBackWithout()
    {
        var tokens = new PackageLaunchTokens(new EphemeralDataProtectionProvider(), new TestClock { UtcNow = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc) });
        var launch = new PackageLaunch("launch1", "enrollment1", "package1", "file1", "user1");

        var read = tokens.Read(tokens.Create(launch));

        Assert.Equal(launch, read);
        Assert.Null(read.PlayerOrigin);
    }

    [Fact]
    public void Token_RefusesAPlayerOriginThatIsNotAnOrigin()
    {
        var tokens = new PackageLaunchTokens(new EphemeralDataProtectionProvider(), new TestClock { UtcNow = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc) });

        Assert.Throws<ArgumentException>(() => tokens.Create(new PackageLaunch("l", "e", "p", "f", "u") { PlayerOrigin = "https://learn.example.com|x" }));
    }
}
