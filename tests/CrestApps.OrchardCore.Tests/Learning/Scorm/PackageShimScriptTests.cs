using CrestApps.OrchardCore.Learning.Packages;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Packages.Services;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

public sealed class PackageShimScriptTests
{
    [Fact]
    public void Markup_CarriesTheLaunchAndTheEmbeddedApi_AndCannotBeBrokenOutOf()
    {
        var launch = new PackageLaunch("launch-1", "e1", "p1", "f1", "u1");
        var state = new PackageRuntimeState { Cmi = new Scorm12Cmi { StudentName = "</script><script>alert(1)</script> Élise", SuspendData = "a&b" } };

        var markup = PackageShimScript.Build(launch, state, "https://learn.example.com");

        Assert.StartsWith("<script>window.CrestAppsScorm12Launch={", markup, StringComparison.Ordinal);
        Assert.Contains("\"launchId\":\"launch-1\"", markup, StringComparison.Ordinal);
        Assert.Contains("\"playerOrigin\":\"https://learn.example.com\"", markup, StringComparison.Ordinal);
        Assert.Contains("LMSInitialize", markup, StringComparison.Ordinal);
        Assert.Contains("root.API = createApi", markup, StringComparison.Ordinal);

        // Only the two script elements the server writes; the learner's data is escaped inside the first.
        Assert.Equal(2, CountOf(markup, "</script>"));
        Assert.DoesNotContain("alert(1)</", markup, StringComparison.Ordinal);
        Assert.All(markup, character => Assert.True(character < 128, $"Not ASCII: {(int)character}"));
    }

    [Fact]
    public void Scorm2004Markup_CarriesThe2004DataAndApi_AndCannotBeBrokenOutOf()
    {
        var launch = new PackageLaunch("launch-2", "e1", "p1", "f1", "u1");
        var state = new PackageRuntimeState
        {
            Standard = PackagesConstants.Standards.Scorm2004FourthEdition,
            Cmi = null,
            Cmi2004 = new Scorm2004Cmi { LearnerName = "</script><script>alert(1)</script> Élise", SuspendData = "a&b" },
        };

        var markup = PackageShimScript.Build(launch, state, "https://learn.example.com", PackagesConstants.Standards.Scorm2004FourthEdition);

        Assert.StartsWith("<script>window.CrestAppsScorm2004Launch={", markup, StringComparison.Ordinal);
        Assert.Contains("\"launchId\":\"launch-2\"", markup, StringComparison.Ordinal);
        Assert.Contains("\"Edition\":4", markup, StringComparison.Ordinal);
        Assert.Contains("root.API_1484_11 = createApi", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("CrestAppsScorm12Launch", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("root.API = createApi", markup, StringComparison.Ordinal);
        Assert.Equal(2, CountOf(markup, "</script>"));
        Assert.DoesNotContain("alert(1)</", markup, StringComparison.Ordinal);
        Assert.All(markup, character => Assert.True(character < 128, $"Not ASCII: {(int)character}"));
    }

    [Fact]
    public void Scorm2004Markup_WithoutAState_StartsWithNewData()
    {
        var markup = PackageShimScript.Build(new PackageLaunch("l", "e", "p", "f", "u"), null, "https://learn.example.com", PackagesConstants.Standards.Scorm2004ThirdEdition);

        Assert.Contains("\"Edition\":3", markup, StringComparison.Ordinal);
        Assert.Contains("\"CompletionStatus\":\"unknown\"", markup, StringComparison.Ordinal);
    }

    private static int CountOf(string text, string value)
    {
        var count = 0;

        for (var index = text.IndexOf(value, StringComparison.OrdinalIgnoreCase); index >= 0; index = text.IndexOf(value, index + 1, StringComparison.OrdinalIgnoreCase))
        {
            count++;
        }

        return count;
    }
}
