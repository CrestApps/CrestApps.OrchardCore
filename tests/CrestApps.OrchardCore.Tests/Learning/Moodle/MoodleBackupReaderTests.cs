using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using CrestApps.OrchardCore.Learning.Core.Moodle;

namespace CrestApps.OrchardCore.Tests.Learning.Moodle;

/// <summary>
/// The Moodle course backup reader on synthetic backups in Moodle's layout, as a zip archive and as the gzip-compressed
/// tar archive newer Moodle versions write: what it reads, and what the safe extraction rules refuse.
/// </summary>
public sealed class MoodleBackupReaderTests
{
    public static TheoryData<string> Formats => ["zip", "tar.gz"];

    [Theory]
    [MemberData(nameof(Formats))]
    public async Task ACourseBackup_IsRead_WithItsCourseSectionsActivitiesFilesAndQuestions(string format)
    {
        // Arrange
        var builder = MoodleBackupSamples.WorkplaceSafety();
        var archive = format == "zip" ? builder.BuildZip() : builder.BuildTarGz();
        using var folder = new TempFolder();

        // Act
        var result = await MoodleBackupReader.ExtractAndReadAsync(new MemoryStream(archive), folder.Path, new MoodleBackupLimits(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Error + " " + result.ErrorDetail);
        var backup = result.Backup;
        Assert.Equal(12, backup.SiteKey.Length);
        Assert.Equal($"moodle:{backup.SiteKey}:course:12", backup.CourseSource);
        Assert.Equal("https://moodle.example.test", backup.WwwRoot);

        var course = backup.Course;
        Assert.Equal("12", course.Id);
        Assert.Equal("Workplace Safety (Moodle)", course.FullName);
        Assert.Equal("WS-M", course.ShortName);
        Assert.Equal("<p>A course made up for the tests.</p>", course.Summary);
        Assert.Equal(new DateTime(2024, 1, 15, 8, 0, 0, DateTimeKind.Utc), course.StartUtc);
        Assert.Equal(new DateTime(2025, 1, 15, 8, 0, 0, DateTimeKind.Utc), course.EndUtc);
        Assert.Equal("30 Day Learning Plan", course.CategoryName);
        Assert.True(course.EnableCompletion);
        Assert.Equal(100, course.GradeMax);

        Assert.Collection(
            course.Enrolments,
            enrolment => Assert.Equal(("manual", true, false), (enrolment.Method, enrolment.Enabled, enrolment.HasPassword)),
            enrolment =>
            {
                Assert.Equal(("self", true, true), (enrolment.Method, enrolment.Enabled, enrolment.HasPassword));
                Assert.Equal(30 * 86400, enrolment.EnrolPeriodSeconds);
            });

        Assert.Equal(
            [(MoodleCompletionCriterion.Activity, "202"), (MoodleCompletionCriterion.Activity, "205"), (MoodleCompletionCriterion.Grade, null)],
            course.CompletionCriteria.Select(criterion => (criterion.Type, criterion.ModuleId)));
        Assert.Equal(70, course.CompletionCriteria[2].GradePass);

        Assert.Equal(["General", "Basics"], backup.Sections.Select(section => section.Name ?? "General"));
        Assert.Equal(["201", "202", "203", "204", "205", "206", "207"], backup.Sections[1].Sequence);
        Assert.Equal("<p>Start here.</p>", backup.Sections[1].Summary);

        Assert.Equal(["label", "scorm", "resource", "url", "quiz", "page", "forum"], backup.Activities.Select(activity => activity.ModuleName));

        var scorm = backup.FindActivity("202");
        Assert.Equal("Fire safety package", scorm.Name);
        Assert.Equal(2, scorm.Completion);
        Assert.Equal("90", scorm.Get("completionscorerequired"));
        var package = Assert.Single(scorm.Files);
        Assert.Equal(("mod_scorm", "package", "fire.zip"), (package.Component, package.FileArea, package.FileName));
        Assert.True(File.Exists(backup.GetContentPath(package)));

        var resource = backup.FindActivity("203");
        Assert.Equal("guide.pdf", Assert.Single(resource.Files).FileName);

        Assert.Equal("https://example.com/policy", backup.FindActivity("204").Get("externalurl"));
        Assert.Equal("<p>Rules of the site.</p>", backup.FindActivity("206").Get("content"));

        var quiz = backup.FindActivity("205");
        Assert.Equal(10, quiz.GradeMax);
        Assert.Equal(7, quiz.GradePass);
        Assert.True(quiz.CompletionPassGrade);
        Assert.Equal(["10301", "10302", null], quiz.Slots.Select(slot => slot.BankEntryId));
        Assert.Equal("40", quiz.Slots[2].RandomCategoryId);
        Assert.Equal("1800", quiz.Get("timelimit"));

        var category = Assert.Single(backup.QuestionCategories, candidate => candidate.Id == "40");
        Assert.Equal("Default for WS-M", category.Name);
        var choice = backup.FindQuestion("10301");
        Assert.Equal("multichoice", choice.Type);
        Assert.Equal("<p>Which extinguisher is for electrical fires?</p>", choice.Text);
        Assert.Equal(["CO2", "Water"], choice.Answers.Select(answer => answer.Text));
        Assert.Equal([1m, 0m], choice.Answers.Select(answer => answer.Fraction));
        Assert.Equal("1", choice.Option("single"));
    }

    [Fact]
    public async Task OnlyTheLatestVersionOfAQuestion_IsRead()
    {
        // Arrange
        var builder = new MoodleBackupBuilder(3, "Versions")
            .Section(1, 0, null)
            .Questions(9, "Default", new Question(77, "truefalse", "TF", "<p>Version two</p>")
            {
                Version = 2,
                Answers = [("True", 1, string.Empty), ("False", 0, string.Empty)],
                OlderVersions = [new Question(76, "truefalse", "TF", "<p>Version one</p>") { BankEntryId = 10077, Answers = [("True", 0, string.Empty), ("False", 1, string.Empty)] }],
            });
        using var folder = new TempFolder();

        // Act
        var result = await MoodleBackupReader.ExtractAndReadAsync(new MemoryStream(builder.BuildTarGz()), folder.Path, new MoodleBackupLimits(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded, result.Error);
        var question = Assert.Single(Assert.Single(result.Backup.QuestionCategories).Questions);
        Assert.Equal("<p>Version two</p>", question.Text);
        Assert.Equal("10077", question.BankEntryId);
    }

    [Fact]
    public async Task AnEnrolmentKey_IsNeverRead_OnlyWhetherThereIsOne()
    {
        // Arrange
        var builder = new MoodleBackupBuilder(4, "Keys").Section(1, 0, null).Enrolment("self", password: "S3cret-key-value");
        using var folder = new TempFolder();

        // Act
        var result = await MoodleBackupReader.ExtractAndReadAsync(new MemoryStream(builder.BuildZip()), folder.Path, new MoodleBackupLimits(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(Assert.Single(result.Backup.Course.Enrolments).HasPassword);
        Assert.DoesNotContain("S3cret-key-value", System.Text.Json.JsonSerializer.Serialize(result.Backup), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public async Task AnEntryThatClimbsOutOfTheBackup_IsRefused(string format)
    {
        // Arrange
        var builder = new MoodleBackupBuilder(5, "Traversal").Section(1, 0, null).RawEntry("../evil.txt", Encoding.UTF8.GetBytes("evil"));
        using var folder = new TempFolder();

        // Act
        var result = await MoodleBackupReader.ExtractAndReadAsync(new MemoryStream(format == "zip" ? builder.BuildZip() : builder.BuildTarGz()), folder.Path, new MoodleBackupLimits(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MoodleBackupErrors.UnsafeArchive, result.Error);
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(folder.Path), "evil.txt")));
    }

    [Fact]
    public async Task ALinkInATarBackup_IsRefused()
    {
        // Arrange
        using var output = new MemoryStream();

        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        using (var tar = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: true))
        {
            tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "moodle_backup.xml") { DataStream = new MemoryStream(Encoding.UTF8.GetBytes("<moodle_backup/>")) });
            tar.WriteEntry(new PaxTarEntry(TarEntryType.SymbolicLink, "files/link") { LinkName = "/etc/passwd" });
        }

        using var folder = new TempFolder();

        // Act
        var result = await MoodleBackupReader.ExtractAndReadAsync(new MemoryStream(output.ToArray()), folder.Path, new MoodleBackupLimits(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MoodleBackupErrors.UnsafeArchive, result.Error);
    }

    [Fact]
    public async Task ABackupWithMoreEntriesThanAllowed_IsRefused()
    {
        // Arrange
        using var folder = new TempFolder();

        // Act
        var result = await MoodleBackupReader.ExtractAndReadAsync(new MemoryStream(MoodleBackupSamples.WorkplaceSafety().BuildTarGz()), folder.Path, new MoodleBackupLimits { MaxEntries = 5 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MoodleBackupErrors.UnsafeArchive, result.Error);
    }

    [Fact]
    public async Task ADocumentTypeDefinition_IsRefused()
    {
        // Arrange
        var builder = new MoodleBackupBuilder(6, "DTD")
        {
            BackupXmlOverride = "<?xml version=\"1.0\"?><!DOCTYPE moodle_backup [<!ENTITY x \"y\">]><moodle_backup><information>&x;</information></moodle_backup>",
        };
        builder.Section(1, 0, null);
        using var folder = new TempFolder();

        // Act
        var result = await MoodleBackupReader.ExtractAndReadAsync(new MemoryStream(builder.BuildZip()), folder.Path, new MoodleBackupLimits(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MoodleBackupErrors.InvalidXml, result.Error);
        Assert.Equal("moodle_backup.xml", result.ErrorDetail);
    }

    [Fact]
    public async Task AFileThatIsNotABackup_IsRefused_WithTheReason()
    {
        // Arrange
        using var notAnArchive = new TempFolder();
        using var noBackupFile = new TempFolder();
        using var activityBackup = new TempFolder();
        var zipWithoutBackup = new MemoryStream();

        using (var zip = new ZipArchive(zipWithoutBackup, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var stream = zip.CreateEntry("readme.txt").Open();
            stream.Write(Encoding.UTF8.GetBytes("Not a backup."));
        }

        var activity = new MoodleBackupBuilder(7, "Activity only") { BackupType = "activity" };
        activity.Section(1, 0, null);

        // Act
        var text = await MoodleBackupReader.ExtractAndReadAsync(new MemoryStream(Encoding.UTF8.GetBytes("plain text")), notAnArchive.Path, new MoodleBackupLimits(), TestContext.Current.CancellationToken);
        var missing = await MoodleBackupReader.ExtractAndReadAsync(new MemoryStream(zipWithoutBackup.ToArray()), noBackupFile.Path, new MoodleBackupLimits(), TestContext.Current.CancellationToken);
        var notACourse = await MoodleBackupReader.ExtractAndReadAsync(new MemoryStream(activity.BuildZip()), activityBackup.Path, new MoodleBackupLimits(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MoodleBackupErrors.NotAnArchive, text.Error);
        Assert.Equal(MoodleBackupErrors.NoBackupFile, missing.Error);
        Assert.Equal(MoodleBackupErrors.NotACourse, notACourse.Error);
    }
}

/// <summary>
/// A folder for one test, deleted after it.
/// </summary>
internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = Directory.CreateTempSubdirectory("crestapps-moodle-test-").FullName;
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
