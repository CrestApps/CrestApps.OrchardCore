using System.IO.Compression;
using CrestApps.OrchardCore.Learning.Packages;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Core.Scorm;
using CrestApps.OrchardCore.Learning.Packages.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Learning.Scorm;

public sealed class LearningPackageImporterTests : IDisposable
{
    private readonly TestTempDirectoryProvider _temp = new();
    private readonly InMemoryPackageFiles _files = new();
    private readonly LearningPackageOptions _options = new();

    [Fact]
    public async Task ValidPackage_IsKeptAsACleanArchiveOfTheExtractedFiles()
    {
        using var zip = PackageZip.SingleScoPackage();

        var result = await Importer().ImportAsync("ladder-safety.zip", zip, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(3, result.FileCount);
        Assert.Equal("index.html", result.Manifest.Launch.Path);

        var stored = Assert.Single(_files.Stored.Values);

        using var archive = new ZipArchive(new MemoryStream(stored.Content), ZipArchiveMode.Read);

        Assert.Equal(["imsmanifest.xml", "index.html", "scripts/lesson.js"], archive.Entries.Select(entry => entry.FullName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ApplyTo_WritesTheManifestOntoThePart()
    {
        using var zip = PackageZip.Create(("imsmanifest.xml", ScormManifestSamples.MasteryScore.TrimStart()), ("exam.html", "<html></html>"));

        var result = await Importer().ImportAsync("exam.zip", zip, TestContext.Current.CancellationToken);
        var part = new LearningPackagePart();
        var now = new DateTime(2026, 11, 15, 9, 0, 0, DateTimeKind.Utc);

        result.ApplyTo(part, "exam.zip", "author1", now);

        Assert.Equal(result.File.FileId, part.PackageFileId);
        Assert.Equal("exam.zip", part.FileName);
        Assert.Equal(PackagesConstants.Standards.Scorm12, part.Standard);
        Assert.Equal("MASTERY", part.ManifestIdentifier);
        Assert.Equal("Exam", part.Title);
        Assert.Equal("EXAM", part.LaunchItemIdentifier);
        Assert.Equal("Final exam", part.LaunchItemTitle);
        Assert.Equal("exam.html", part.LaunchPath);
        Assert.Equal(80m, part.MasteryScore);
        Assert.Equal("00:30:00", part.MaxTimeAllowed);
        Assert.Equal("exit,message", part.TimeLimitAction);
        Assert.Equal("level=2", part.LaunchData);
        Assert.Equal(1, part.ScoCount);
        Assert.Equal(2, part.FileCount);
        Assert.Equal(now, part.ImportedUtc);
        Assert.Equal("author1", part.ImportedBy);
    }

    [Fact]
    public async Task ApplyTo_WritesTheScorm2004VersionAndValuesOntoThePart()
    {
        using var zip = PackageZip.Create(
            ("imsmanifest.xml", ScormManifestSamples.Scorm2004Fourth.TrimStart()),
            ("pre/index.html", "<html></html>"),
            ("drive/index.html", "<html></html>"),
            ("glossary.html", "<html></html>"));

        var result = await Importer().ImportAsync("forklift.zip", zip, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.Error);

        var part = new LearningPackagePart { MasteryScore = 50 };

        result.ApplyTo(part, "forklift.zip", "author1", new DateTime(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc));

        Assert.Equal(PackagesConstants.Standards.Scorm2004FourthEdition, part.Standard);
        Assert.Equal("Forklift Operation", part.Title);
        Assert.Equal("ITEM-PRE", part.LaunchItemIdentifier);
        Assert.Equal("pre/index.html", part.LaunchPath);
        Assert.Equal(0.75m, part.CompletionThreshold);
        Assert.Equal(0.6m, part.ScaledPassingScore);
        Assert.Equal(["PRIMARY-PRE", "obj-mast-check"], part.ObjectiveIds);
        Assert.True(part.HasSequencing);
        Assert.Equal("PT30M", part.MaxTimeAllowed);
        Assert.Null(part.MasteryScore);
        Assert.Equal(2, part.ScoCount);
    }

    [Fact]
    public async Task ApplyTo_AScorm12PackageAfterA2004One_ClearsThe2004Values()
    {
        using var zip = PackageZip.SingleScoPackage();

        var result = await Importer().ImportAsync("ladder.zip", zip, TestContext.Current.CancellationToken);
        var part = new LearningPackagePart { CompletionThreshold = 0.5m, ScaledPassingScore = 0.7m, ObjectiveIds = ["old"], HasSequencing = true };

        result.ApplyTo(part, "ladder.zip", "author1", new DateTime(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc));

        Assert.Equal(PackagesConstants.Standards.Scorm12, part.Standard);
        Assert.Null(part.CompletionThreshold);
        Assert.Null(part.ScaledPassingScore);
        Assert.Empty(part.ObjectiveIds);
        Assert.False(part.HasSequencing);
    }

    [Fact]
    public async Task LaunchFileMissing_IsRefused()
    {
        using var zip = PackageZip.Create(("imsmanifest.xml", ScormManifestSamples.SingleSco.TrimStart()), ("other.html", "x"));

        var result = await Importer().ImportAsync("p.zip", zip, TestContext.Current.CancellationToken);

        Assert.Equal(ScormManifestErrors.LaunchFileMissing, result.Error);
        Assert.Empty(_files.Stored);
    }

    [Fact]
    public async Task LaunchFileInAnotherCase_UsesTheFileAsItIsInThePackage()
    {
        using var zip = PackageZip.Create(("imsmanifest.xml", ScormManifestSamples.SingleSco.TrimStart()), ("Index.HTML", "x"));

        var result = await Importer().ImportAsync("p.zip", zip, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("Index.HTML", result.Manifest.Launch.Path);
    }

    [Fact]
    public async Task NoManifest_IsRefused()
    {
        using var zip = PackageZip.Create(("index.html", "x"));

        Assert.Equal(ScormManifestErrors.Missing, (await Importer().ImportAsync("p.zip", zip, TestContext.Current.CancellationToken)).Error);
    }

    [Fact]
    public async Task Cmi5Course_IsRefusedAsNotSupported()
    {
        using var zip = PackageZip.Create(("cmi5.xml", "<courseStructure xmlns=\"https://w3id.org/xapi/profiles/cmi5/v1/CourseStructure.xsd\"/>"), ("index.html", "x"));

        Assert.Equal(ScormManifestErrors.Cmi5, (await Importer().ImportAsync("p.zip", zip, TestContext.Current.CancellationToken)).Error);
    }

    [Fact]
    public async Task NotAZipName_IsRefused()
    {
        using var zip = PackageZip.SingleScoPackage();

        Assert.Equal(PackageImportErrors.NotZip, (await Importer().ImportAsync("lesson.exe", zip, TestContext.Current.CancellationToken)).Error);
    }

    [Fact]
    public async Task UploadOverTheLimit_IsRefused()
    {
        using var zip = PackageZip.SingleScoPackage();
        _options.MaxUploadBytes = 100;

        Assert.Equal(PackageImportErrors.UploadTooLarge, (await Importer().ImportAsync("p.zip", zip, TestContext.Current.CancellationToken)).Error);
    }

    [Fact]
    public async Task HostileEntry_IsRefusedWithTheEntry()
    {
        using var zip = PackageZip.SingleScoPackage(null, ("../../evil.js", "x"));

        var result = await Importer().ImportAsync("p.zip", zip, TestContext.Current.CancellationToken);

        Assert.Equal(PackageArchiveErrors.UnsafePath, result.Error);
        Assert.Equal("../../evil.js", result.Entry);
        Assert.Empty(_files.Stored);
    }

    [Fact]
    public async Task ExpandedLimit_ComesFromTheOptions()
    {
        using var zip = PackageZip.SingleScoPackage(null, ("big.bin", new string('a', 4096)));
        _options.MaxExpandedBytes = 2048;

        Assert.Equal(PackageArchiveErrors.TooLarge, (await Importer().ImportAsync("p.zip", zip, TestContext.Current.CancellationToken)).Error);
    }

    [Fact]
    public async Task StoreRefusal_IsReported()
    {
        using var zip = PackageZip.SingleScoPackage();
        _files.RefuseAll = true;

        Assert.Equal(PackageImportErrors.Refused, (await Importer().ImportAsync("p.zip", zip, TestContext.Current.CancellationToken)).Error);
    }

    [Fact]
    public async Task TemporaryFolder_IsDeletedAfterSuccessAndRefusal()
    {
        using (var good = PackageZip.SingleScoPackage())
        {
            await Importer().ImportAsync("p.zip", good, TestContext.Current.CancellationToken);
        }

        using (var bad = PackageZip.SingleScoPackage(null, ("../x", "x")))
        {
            await Importer().ImportAsync("p.zip", bad, TestContext.Current.CancellationToken);
        }

        Assert.Empty(Directory.EnumerateFileSystemEntries(_temp.GetRootDirectory()));
    }

    public void Dispose()
        => _temp.Dispose();

    private LearningPackageImporter Importer()
        => new(_temp, _files, Options.Create(_options), NullLogger<LearningPackageImporter>.Instance);
}
