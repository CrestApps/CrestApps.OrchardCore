using System.Text;
using CrestApps.OrchardCore.Learning.Packages;
using CrestApps.OrchardCore.Learning.Packages.Core.Scorm;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

public sealed class ScormManifestParserTests
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

        Assert.Equal(ScormManifestErrors.NoSco, result.Error);
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

        Assert.Equal(ScormManifestErrors.InvalidMasteryScore, result.Error);
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
    public void Scorm12_IsDetectedAsScorm12()
    {
        var result = Parse(ScormManifestSamples.SingleSco);

        Assert.Equal(PackagesConstants.Standards.Scorm12, result.Manifest.Standard);
        Assert.False(result.Manifest.IsScorm2004);
        Assert.False(result.Manifest.HasSequencing);
    }

    [Fact]
    public void Scorm2004ThirdEdition_IsDetected_AndItsItemValuesAreRead()
    {
        var result = Parse(ScormManifestSamples.Scorm2004Third);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(PackagesConstants.Standards.Scorm2004ThirdEdition, result.Manifest.Standard);
        Assert.True(result.Manifest.IsScorm2004);
        Assert.Equal("2004 3rd Edition", result.Manifest.SchemaVersion);
        Assert.Equal("Hazard Communication", result.Manifest.Title);
        Assert.False(result.Manifest.HasSequencing);

        var sco = Assert.Single(result.Manifest.Scos);

        Assert.Equal("ITEM-HAZCOM", sco.ItemIdentifier);
        Assert.Equal("content/hazcom/index.html", sco.Path);
        Assert.Equal("mode=lms", sco.Query);
        Assert.Equal(0.8m, sco.CompletionThreshold);
        Assert.Null(sco.ScaledPassingScore);
        Assert.Empty(sco.ObjectiveIds);
        Assert.Equal("chapter=1", sco.DataFromLms);
        Assert.Equal("continue,message", sco.TimeLimitAction);
        Assert.Null(sco.MaxTimeAllowed);
        Assert.Null(sco.MasteryScore);
        Assert.False(sco.HasSequencing);
    }

    [Fact]
    public void Scorm2004FourthEdition_WithSequencing_ReadsObjectivesThresholdAndLimits_AndLaunchesTheFirstSco()
    {
        var result = Parse(ScormManifestSamples.Scorm2004Fourth);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(PackagesConstants.Standards.Scorm2004FourthEdition, result.Manifest.Standard);
        Assert.True(result.Manifest.HasSequencing);
        Assert.Equal(["ITEM-PRE", "ITEM-DRIVE"], result.Manifest.Scos.Select(sco => sco.ItemIdentifier));
        Assert.Equal(1, result.Manifest.AssetCount);

        var launch = result.Manifest.Launch;

        Assert.Equal("pre/index.html", launch.Path);
        Assert.True(launch.HasSequencing);
        Assert.Equal(0.75m, launch.CompletionThreshold);
        Assert.Equal(0.6m, launch.ScaledPassingScore);
        Assert.Equal(["PRIMARY-PRE", "obj-mast-check"], launch.ObjectiveIds);
        Assert.Equal("PT30M", launch.MaxTimeAllowed);
        Assert.False(result.Manifest.Scos[1].HasSequencing);
        Assert.Null(result.Manifest.Scos[1].CompletionThreshold);
    }

    [Fact]
    public void Scorm2004Namespaces_WithoutASchemaVersion_AreTheFourthEdition()
    {
        var result = Parse(ScormManifestSamples.Scorm2004NamespacesOnly);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(PackagesConstants.Standards.Scorm2004FourthEdition, result.Manifest.Standard);
        Assert.Equal("index.html", result.Manifest.Launch.Path);
    }

    [Theory]
    [InlineData("2004 3rd Edition", PackagesConstants.Standards.Scorm2004ThirdEdition)]
    [InlineData("2004 4th Edition", PackagesConstants.Standards.Scorm2004FourthEdition)]
    [InlineData("CAM 1.3", PackagesConstants.Standards.Scorm2004ThirdEdition)]
    [InlineData("2004 2nd Edition", PackagesConstants.Standards.Scorm2004ThirdEdition)]
    public void SchemaVersion_NamesTheEdition(string schemaVersion, string expected)
    {
        var result = Parse(ScormManifestSamples.Scorm2004Third.Replace("2004 3rd Edition", schemaVersion, StringComparison.Ordinal));

        Assert.Equal(expected, result.Manifest.Standard);
    }

    [Fact]
    public void FourthEditionThreshold_NotCompletedByMeasure_IsNoThreshold()
    {
        var result = Parse(ScormManifestSamples.Scorm2004Fourth.Replace("completedByMeasure=\"true\"", "completedByMeasure=\"false\"", StringComparison.Ordinal));

        Assert.Null(result.Manifest.Launch.CompletionThreshold);
    }

    [Fact]
    public void FourthEditionThreshold_WithoutAMinimum_IsOne()
    {
        var result = Parse(ScormManifestSamples.Scorm2004Fourth.Replace(" minProgressMeasure=\"0.75\"", string.Empty, StringComparison.Ordinal));

        Assert.Equal(1m, result.Manifest.Launch.CompletionThreshold);
    }

    [Fact]
    public void PrimaryObjective_NotSatisfiedByMeasure_HasNoPassingScore()
    {
        var result = Parse(ScormManifestSamples.Scorm2004Fourth.Replace(" satisfiedByMeasure=\"true\"", string.Empty, StringComparison.Ordinal));

        Assert.Null(result.Manifest.Launch.ScaledPassingScore);
        Assert.Equal(["PRIMARY-PRE", "obj-mast-check"], result.Manifest.Launch.ObjectiveIds);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("-0.1")]
    [InlineData("most")]
    public void CompletionThreshold_OutsideZeroToOne_IsRefused(string value)
    {
        var result = Parse(ScormManifestSamples.Scorm2004Third.Replace(">0.8<", $">{value}<", StringComparison.Ordinal));

        Assert.Equal(ScormManifestErrors.InvalidCompletionThreshold, result.Error);
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("-2")]
    [InlineData("high")]
    public void MinNormalizedMeasure_OutsideMinusOneToOne_IsRefused(string value)
    {
        var result = Parse(ScormManifestSamples.Scorm2004Fourth.Replace(">0.6<", $">{value}<", StringComparison.Ordinal));

        Assert.Equal(ScormManifestErrors.InvalidPassingScore, result.Error);
    }

    [Fact]
    public void Dtd_IsRefusedAndNeverResolved()
    {
        Assert.Equal(ScormManifestErrors.InvalidXml, Parse(ScormManifestSamples.Doctype).Error);
    }

    [Fact]
    public void BrokenXml_IsRefused()
    {
        Assert.Equal(ScormManifestErrors.InvalidXml, Parse("<manifest><organizations>").Error);
    }

    [Theory]
    [InlineData("http://example.com/launch.html")]
    [InlineData("https://example.com/launch.html")]
    [InlineData("//example.com/launch.html")]
    public void ExternalHref_IsRefused(string href)
    {
        Assert.Equal(ScormManifestErrors.ExternalLaunch, Parse(ScormManifestSamples.WithHref(href)).Error);
    }

    [Theory]
    [InlineData("../outside.html")]
    [InlineData("/etc/passwd")]
    [InlineData("C:/windows/win.ini")]
    public void UnsafeHref_IsRefused(string href)
    {
        Assert.Equal(ScormManifestErrors.UnsafeHref, Parse(ScormManifestSamples.WithHref(href)).Error);
    }

    [Fact]
    public void EncodedHref_IsDecoded()
    {
        var result = Parse(ScormManifestSamples.WithHref("my%20lesson/index.html"));

        Assert.Equal("my lesson/index.html", result.Manifest.Launch.Path);
    }

    private static ScormManifestResult Parse(string xml)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml.TrimStart()));

        return ScormManifestParser.Parse(stream);
    }
}
