using System.Text;
using CrestApps.OrchardCore.Learning.Packages.Core.Scorm;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

public sealed class Scorm12ManifestParserTests
{
    [Fact]
    public void SingleSco_IsTheLaunch()
    {
        var result = Parse(ScormManifestSamples.SingleSco);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("com.example.safety", result.Manifest.Identifier);
        Assert.Equal("Ladder Safety", result.Manifest.Title);
        Assert.Equal("1.2", result.Manifest.SchemaVersion);
        Assert.Equal("ORG-1", result.Manifest.OrganizationIdentifier);

        var sco = Assert.Single(result.Manifest.Scos);

        Assert.Same(sco, result.Manifest.Launch);
        Assert.Equal("ITEM-1", sco.ItemIdentifier);
        Assert.Equal("Ladder Safety Lesson", sco.Title);
        Assert.Equal("RES-1", sco.ResourceIdentifier);
        Assert.Equal("index.html", sco.Path);
        Assert.Null(sco.Query);
        Assert.Null(sco.MasteryScore);
    }

    [Fact]
    public void MultiSco_ListsScosInOrderAndLaunchesTheFirst_SkippingAssets()
    {
        var result = Parse(ScormManifestSamples.MultiSco);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(["LESSON-1", "LESSON-2"], result.Manifest.Scos.Select(sco => sco.ItemIdentifier));
        Assert.Equal(1, result.Manifest.AssetCount);
        Assert.Equal("lesson1/start.htm", result.Manifest.Launch.Path);
        Assert.Equal(70m, result.Manifest.Launch.MasteryScore);
        Assert.Null(result.Manifest.Scos[1].MasteryScore);
    }

    [Fact]
    public void AssetsOnly_IsRefusedForHavingNoSco()
    {
        var result = Parse(ScormManifestSamples.AssetsOnly);

        Assert.Equal(Scorm12ManifestErrors.NoSco, result.Error);
    }

    [Fact]
    public void XmlBase_IsAppliedFromManifestResourcesAndResource_AndParametersJoinTheQuery()
    {
        var result = Parse(ScormManifestSamples.XmlBase);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("course/content/sco1/launch.html", result.Manifest.Launch.Path);
        Assert.Equal("lang=en&mode=lms", result.Manifest.Launch.Query);
    }

    [Fact]
    public void MasteryScoreAndItemElements_AreRead()
    {
        var sco = Parse(ScormManifestSamples.MasteryScore).Manifest.Launch;

        Assert.Equal(80m, sco.MasteryScore);
        Assert.Equal("00:30:00", sco.MaxTimeAllowed);
        Assert.Equal("exit,message", sco.TimeLimitAction);
        Assert.Equal("level=2", sco.DataFromLms);
    }

    [Theory]
    [InlineData("101")]
    [InlineData("-1")]
    [InlineData("eighty")]
    public void MasteryScore_OutsideZeroToHundred_IsRefused(string value)
    {
        var result = Parse(ScormManifestSamples.MasteryScore.Replace("<adlcp:masteryscore>80<", $"<adlcp:masteryscore>{value}<", StringComparison.Ordinal));

        Assert.Equal(Scorm12ManifestErrors.InvalidMasteryScore, result.Error);
    }

    [Fact]
    public void MissingDefaultOrganization_ReadsTheFirstOrganization()
    {
        var result = Parse(ScormManifestSamples.NoDefaultOrganization);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("FIRST", result.Manifest.OrganizationIdentifier);
        Assert.Equal("a.html", result.Manifest.Launch.Path);
    }

    [Fact]
    public void DefaultNamingAnUnknownOrganization_ReadsTheFirstOrganization()
    {
        var result = Parse(ScormManifestSamples.NoDefaultOrganization.Replace("<organizations>", "<organizations default=\"GONE\">", StringComparison.Ordinal));

        Assert.Equal("FIRST", result.Manifest.OrganizationIdentifier);
    }

    [Fact]
    public void DefaultOrganization_IsReadEvenWhenItIsNotFirst()
    {
        var result = Parse(ScormManifestSamples.NoDefaultOrganization.Replace("<organizations>", "<organizations default=\"SECOND\">", StringComparison.Ordinal));

        Assert.Equal("SECOND", result.Manifest.OrganizationIdentifier);
        Assert.Equal("Second organization", result.Manifest.Title);
        Assert.Equal("b.html", result.Manifest.Launch.Path);
    }

    [Fact]
    public void Scorm2004_IsRefused()
    {
        Assert.Equal(Scorm12ManifestErrors.Scorm2004, Parse(ScormManifestSamples.Scorm2004).Error);
    }

    [Fact]
    public void Dtd_IsRefusedAndNeverResolved()
    {
        Assert.Equal(Scorm12ManifestErrors.InvalidXml, Parse(ScormManifestSamples.Doctype).Error);
    }

    [Fact]
    public void BrokenXml_IsRefused()
    {
        Assert.Equal(Scorm12ManifestErrors.InvalidXml, Parse("<manifest><organizations>").Error);
    }

    [Theory]
    [InlineData("http://example.com/launch.html")]
    [InlineData("https://example.com/launch.html")]
    [InlineData("//example.com/launch.html")]
    public void ExternalHref_IsRefused(string href)
    {
        Assert.Equal(Scorm12ManifestErrors.ExternalLaunch, Parse(ScormManifestSamples.WithHref(href)).Error);
    }

    [Theory]
    [InlineData("../outside.html")]
    [InlineData("/etc/passwd")]
    [InlineData("C:/windows/win.ini")]
    public void UnsafeHref_IsRefused(string href)
    {
        Assert.Equal(Scorm12ManifestErrors.UnsafeHref, Parse(ScormManifestSamples.WithHref(href)).Error);
    }

    [Fact]
    public void EncodedHref_IsDecoded()
    {
        var result = Parse(ScormManifestSamples.WithHref("my%20lesson/index.html"));

        Assert.Equal("my lesson/index.html", result.Manifest.Launch.Path);
    }

    private static Scorm12ManifestResult Parse(string xml)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml.TrimStart()));

        return Scorm12ManifestParser.Parse(stream);
    }
}
