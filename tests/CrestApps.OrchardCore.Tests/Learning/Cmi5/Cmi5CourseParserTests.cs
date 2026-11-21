using System.Text;
using CrestApps.OrchardCore.Learning.Packages;
using CrestApps.OrchardCore.Learning.Packages.Core.Cmi5;
using CrestApps.OrchardCore.Learning.Packages.Core.Scorm;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Tests.Learning.Scorm;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Learning.Cmi5;

public sealed class Cmi5CourseParserTests : IDisposable
{
    /// <summary>
    /// A course structure written from the cmi5 (Quartz) course structure example and schema: a course, a block with an
    /// AU, and an AU outside the block with an absolute URL.
    /// </summary>
    internal const string Sample = """
        <?xml version="1.0" encoding="utf-8"?>
        <courseStructure xmlns="https://w3id.org/xapi/profiles/cmi5/v1/CourseStructure.xsd">
          <course id="https://example.com/courses/forklift">
            <title><langstring lang="fr-FR">Sécurité</langstring><langstring lang="en-US">Forklift safety</langstring></title>
            <description><langstring lang="en-US">Drive a forklift safely.</langstring></description>
          </course>
          <block id="https://example.com/courses/forklift/block/1">
            <title><langstring lang="en-US">Practice</langstring></title>
            <description><langstring lang="en-US">Practice block</langstring></description>
            <au id="https://example.com/courses/forklift/au/quiz" moveOn="Passed" masteryScore="0.8" launchMethod="AnyWindow" activityType="http://adlnet.gov/expapi/activities/assessment">
              <title><langstring lang="en-US">Quiz</langstring></title>
              <description><langstring lang="en-US">A quiz</langstring></description>
              <url>quiz/index.html?lang=en</url>
              <launchParameters>level=2</launchParameters>
              <entitlementKey>key-1</entitlementKey>
            </au>
          </block>
          <au id="https://example.com/courses/forklift/au/video" moveOn="Completed" launchMethod="OwnWindow">
            <title><langstring lang="en-US">Video</langstring></title>
            <description><langstring lang="en-US">A video</langstring></description>
            <url>https://media.example.com/video.html</url>
          </au>
        </courseStructure>
        """;

    private readonly TestTempDirectoryProvider _temp = new();
    private readonly InMemoryPackageFiles _files = new();

    [Fact]
    public void Sample_IsRead_WithBlocksAndAus()
    {
        var result = Cmi5CourseParser.Parse(Stream(Sample));

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("https://example.com/courses/forklift", result.Course.Id);
        Assert.Equal("Forklift safety", result.Course.Title);
        Assert.Equal("Practice", Assert.Single(result.Course.Blocks).Title);
        Assert.Equal(2, result.Course.Aus.Count);

        var quiz = result.Course.Aus[0];

        Assert.Equal("Passed", quiz.MoveOn);
        Assert.Equal(0.8m, quiz.MasteryScore);
        Assert.Equal("quiz/index.html", quiz.Path);
        Assert.Equal("lang=en", quiz.Query);
        Assert.False(quiz.IsAbsolute);
        Assert.Equal("level=2", quiz.LaunchParameters);
        Assert.Equal("key-1", quiz.EntitlementKey);
        Assert.Equal(["https://example.com/courses/forklift/block/1"], quiz.BlockIds);

        var video = result.Course.Aus[1];

        Assert.True(video.IsAbsolute);
        Assert.Equal("OwnWindow", video.LaunchMethod);
        Assert.Empty(video.BlockIds);
    }

    [Theory]
    [InlineData("""<au id="https://e.com/au" moveOn="Sometimes"><title>T</title><url>a.html</url></au>""", Cmi5Errors.InvalidMoveOn)]
    [InlineData("""<au id="https://e.com/au" masteryScore="1.5"><title>T</title><url>a.html</url></au>""", Cmi5Errors.InvalidMasteryScore)]
    [InlineData("""<au id="https://e.com/au" launchMethod="Popup"><title>T</title><url>a.html</url></au>""", Cmi5Errors.InvalidLaunchMethod)]
    [InlineData("""<au id="https://e.com/au"><title>T</title><url>http://e.com/a.html</url></au>""", Cmi5Errors.NotHttps)]
    [InlineData("""<au id="https://e.com/au"><title>T</title><url>../outside.html</url></au>""", Cmi5Errors.InvalidUrl)]
    [InlineData("""<au id="https://e.com/au"><title>T</title></au>""", Cmi5Errors.InvalidUrl)]
    [InlineData("""<au id="not an iri"><title>T</title><url>a.html</url></au>""", Cmi5Errors.InvalidId)]
    [InlineData("""<au id="https://e.com/au"><title>T</title><url>a.html</url></au><au id="https://e.com/au"><title>T</title><url>b.html</url></au>""", Cmi5Errors.InvalidId)]
    [InlineData("", Cmi5Errors.NoAu)]
    public void InvalidCourseStructures_AreRefused(string aus, string error)
    {
        var xml = $"""<courseStructure xmlns="https://w3id.org/xapi/profiles/cmi5/v1/CourseStructure.xsd"><course id="https://e.com/c"><title><langstring lang="en">C</langstring></title></course>{aus}</courseStructure>""";

        Assert.Equal(error, Cmi5CourseParser.Parse(Stream(xml)).Error);
    }

    [Fact]
    public void Dtds_AndCoursesWithoutAnId_AreRefused()
    {
        Assert.Equal(Cmi5Errors.InvalidXml, Cmi5CourseParser.Parse(Stream("""<?xml version="1.0"?><!DOCTYPE x [<!ENTITY e "x">]><courseStructure/>""")).Error);
        Assert.Equal(Cmi5Errors.NoCourse, Cmi5CourseParser.Parse(Stream("""<courseStructure><course><title>C</title></course></courseStructure>""")).Error);
    }

    [Fact]
    public async Task Import_TakesACmi5Package_WhenCmi5IsOn_AndWritesTheCourseOntoThePart()
    {
        using var zip = PackageZip.Create(("cmi5.xml", Sample.TrimStart()), ("quiz/index.html", "<html></html>"));

        var result = await Importer(acceptCmi5: true).ImportAsync("forklift.zip", zip, TestContext.Current.CancellationToken);
        var part = new LearningPackagePart { MasteryScore = 70, CompletionThreshold = 0.5m };

        Assert.True(result.Succeeded, result.Error);

        result.ApplyTo(part, "forklift.zip", "author1", new DateTime(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc));

        Assert.Equal(PackagesConstants.Standards.Cmi5, part.Standard);
        Assert.Equal("Forklift safety", part.Title);
        Assert.Equal(2, part.Cmi5.Aus.Count);
        Assert.Equal("quiz/index.html", part.LaunchPath);
        Assert.Equal(2, part.ScoCount);
        Assert.Null(part.MasteryScore);
        Assert.Null(part.CompletionThreshold);
    }

    [Fact]
    public async Task Import_RefusesCmi5_WhenCmi5IsOff_OrWhenAnAuFileIsMissing()
    {
        using var off = PackageZip.Create(("cmi5.xml", Sample.TrimStart()), ("quiz/index.html", "<html></html>"));
        using var missing = PackageZip.Create(("cmi5.xml", Sample.TrimStart()), ("other.html", "<html></html>"));

        Assert.Equal(ScormManifestErrors.Cmi5, (await Importer(acceptCmi5: false).ImportAsync("a.zip", off, TestContext.Current.CancellationToken)).Error);
        Assert.Equal(Cmi5Errors.LaunchFileMissing, (await Importer(acceptCmi5: true).ImportAsync("b.zip", missing, TestContext.Current.CancellationToken)).Error);
        Assert.Empty(_files.Stored);
    }

    public void Dispose()
        => _temp.Dispose();

    private LearningPackageImporter Importer(bool acceptCmi5)
        => new(_temp, _files, Options.Create(new LearningPackageOptions { AcceptCmi5 = acceptCmi5 }), NullLogger<LearningPackageImporter>.Instance);

    private static MemoryStream Stream(string xml)
        => new(Encoding.UTF8.GetBytes(xml));
}
