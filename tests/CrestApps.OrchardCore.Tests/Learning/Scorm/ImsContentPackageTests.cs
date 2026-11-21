using System.Text;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Packages;
using CrestApps.OrchardCore.Learning.Packages.Core.Scorm;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

/// <summary>
/// Remaining work section 3, item 13: an IMS content package (a manifest with no SCORM resources) imports through the
/// SCORM package import and plays in the same frame without the run-time API; it completes when the learner opens it.
/// </summary>
public sealed class ImsContentPackageTests
{
    private const string ImsManifest = """
        <?xml version="1.0" encoding="UTF-8"?>
        <manifest identifier="moodle.imscp" version="1.1" xmlns="http://www.imsglobal.org/xsd/imscp_v1p1">
          <metadata><schema>IMS Content</schema><schemaversion>1.1.4</schemaversion></metadata>
          <organizations default="TOC1">
            <organization identifier="TOC1">
              <title>Forklift handbook</title>
              <item identifier="I0" identifierref="R0"><title>Start here</title></item>
              <item identifier="I1" identifierref="R1"><title>Chapter 1</title></item>
            </organization>
          </organizations>
          <resources>
            <resource identifier="R0" type="webcontent" href="pages/intro.html"><file href="pages/intro.html"/></resource>
            <resource identifier="R1" type="webcontent" href="pages/one.html"><file href="pages/one.html"/></resource>
          </resources>
        </manifest>
        """;

    [Fact]
    public void AManifestWithNoScormResources_IsAnImsContentPackage_ThatLaunchesItsFirstItem()
    {
        // Act
        var result = Parse(ImsManifest);

        // Assert
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(PackagesConstants.Standards.ImsContentPackage, result.Manifest.Standard);
        Assert.Equal("Forklift handbook", result.Manifest.Title);
        Assert.Equal("pages/intro.html", result.Manifest.Launch.Path);
        Assert.Equal(2, result.Manifest.Scos.Count);
        Assert.False(result.Manifest.IsScorm2004);
    }

    [Fact]
    public void AnImsContentPackage_ThatPointsOutside_IsRefused()
    {
        // Act
        var result = Parse(ImsManifest.Replace("href=\"pages/intro.html\">", "href=\"https://example.com/x.html\">", StringComparison.Ordinal));

        // Assert
        Assert.Equal(ScormManifestErrors.ExternalLaunch, result.Error);
    }

    [Fact]
    public void AScormPackageWithOnlyAssets_IsStillRefused_AndNotTakenForAnImsContentPackage()
    {
        // Act
        var result = Parse(ScormManifestSamples.AssetsOnly);

        // Assert
        Assert.Equal(ScormManifestErrors.NoSco, result.Error);
    }

    [Fact]
    public void ThePackageKind_CompletesWhenViewed_ForAnImsContentPackage()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLearningPackagesCore();

        // Act
        var kind = services.BuildServiceProvider().GetRequiredService<IOptions<CourseItemKindOptions>>().Value.Find(PackagesConstants.PackageKind);

        // Assert
        Assert.True(kind.Supports(CompletionModes.Viewed));
        Assert.Equal(CompletionModes.PackageReported, kind.DefaultCompletionMode);
    }

    [Fact]
    public void Importing_AnImsContentPackage_KeepsItsStandard_AndAsksForViewedCompletion()
    {
        // Arrange
        var manifest = Parse(ImsManifest).Manifest;
        var result = new PackageImportResult { Manifest = manifest, File = new global::CrestApps.OrchardCore.FileStorage.ProtectedFile("f1", "handbook.zip", 10) };
        var part = new LearningPackagePart();

        // Act
        result.ApplyTo(part, "handbook.zip", "admin", DateTime.UtcNow);

        // Assert
        Assert.Equal(PackagesConstants.Standards.ImsContentPackage, part.Standard);
        Assert.Equal("pages/intro.html", part.LaunchPath);
        Assert.True(PackagesConstants.Standards.HasNoRuntime(part.Standard));
        Assert.Equal(CompletionModes.Viewed, PackageImportResult.CompletionModeFor(part.Standard));
        Assert.Null(PackageImportResult.CompletionModeFor(PackagesConstants.Standards.Scorm12));
    }

    private static ScormManifestResult Parse(string xml)
        => ScormManifestParser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(xml.TrimStart())));
}
