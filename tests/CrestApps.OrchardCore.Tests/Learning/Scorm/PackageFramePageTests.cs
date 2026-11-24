using CrestApps.OrchardCore.Learning.Packages;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Services;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

/// <summary>
/// The frame page of a launch on the package content host: the API with the bridge flag, and the launch file in a child
/// frame on the same origin.
/// </summary>
public sealed class PackageFramePageTests
{
    private static readonly PackageLaunch _launch = new("launch-1", "e1", "p1", "f1", "u1") { PlayerOrigin = "https://learn.example.com" };

    [Fact]
    public void Page_StartsTheApiInBridgeMode_AndLoadsTheLaunchFileInAChildFrame()
    {
        var page = PackageFramePage.Build(_launch, null, PackagesConstants.Standards.Scorm12, "/learning/packages/run/tok/scormdriver/indexAPI.html?x=1&y=2", "Lesson");

        Assert.StartsWith("<!DOCTYPE html>", page, StringComparison.Ordinal);
        Assert.Contains("window.CrestAppsScorm12Launch={", page, StringComparison.Ordinal);
        Assert.Contains("\"playerOrigin\":\"https://learn.example.com\"", page, StringComparison.Ordinal);
        Assert.Contains("\"bridge\":true", page, StringComparison.Ordinal);
        Assert.Contains("root.API = createApi", page, StringComparison.Ordinal);
        Assert.Contains("<iframe name=\"crestapps-package-content\"", page, StringComparison.Ordinal);
        Assert.Contains("src=\"/learning/packages/run/tok/scormdriver/indexAPI.html?x=1&amp;y=2\"", page, StringComparison.Ordinal);
        Assert.True(page.IndexOf("CrestAppsScorm12Launch", StringComparison.Ordinal) < page.IndexOf("<iframe", StringComparison.Ordinal));
    }

    [Fact]
    public void Page_OfAScorm2004Package_HasThe2004Api()
    {
        var page = PackageFramePage.Build(_launch, null, PackagesConstants.Standards.Scorm2004FourthEdition, "/run/a.html", "Lesson");

        Assert.Contains("window.CrestAppsScorm2004Launch={", page, StringComparison.Ordinal);
        Assert.Contains("root.API_1484_11 = createApi", page, StringComparison.Ordinal);
        Assert.DoesNotContain("CrestAppsScorm12Launch", page, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_EncodesTheTitleAndTheSource()
    {
        var page = PackageFramePage.Build(_launch, null, PackagesConstants.Standards.Scorm12, "/run/\"><script>alert(1)</script>.html", "</title><script>alert(2)</script>");

        Assert.DoesNotContain("<script>alert(1)", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>alert(2)", page, StringComparison.Ordinal);
    }

    [Fact]
    public void StrictMarkup_HasNoBridgeFlag()
    {
        Assert.DoesNotContain("\"bridge\"", PackageShimScript.Build(_launch, null, "https://learn.example.com", PackagesConstants.Standards.Scorm12), StringComparison.Ordinal);
    }
}
