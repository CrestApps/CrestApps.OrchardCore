using System.Reflection;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.BackgroundTasks;
using CrestApps.OrchardCore.ContentTransfer.Indexes;
using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Packages;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Tests.Learning.Moodle;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Entities;
using OrchardCore.Lists.Indexes;
using OrchardCore.Lists.Models;
using OrchardCore.Media;
using YesSql;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// The Moodle import on a running site: a synthetic course backup imported through ContentTransfer's background import
/// becomes a course in its category with its dates, enrolment and completion, its sections as outline groups and an
/// item for each activity the site supports; each activity it cannot import is a failed row of the import; importing
/// the backup again updates the course instead of making another. The site is shared, so each test uses a course id
/// and a title of its own.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class MoodleImportTests
{
    private readonly LearningSiteFixture _fixture;

    public MoodleImportTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ABackup_BecomesACourseWithItsSectionsAndItems_AndReportsWhatItCannotImport()
    {
        // Arrange
        var courseId = Random.Shared.Next(100_000, 900_000);
        var title = "Workplace Safety (Moodle) " + courseId;
        var backup = MoodleBackupSamples.WorkplaceSafety(courseId, title).BuildTarGz();

        // Act
        var entry = await ImportAsync(backup);
        var course = await FindCourseAsync(title);

        // Assert: the report. The forum is not imported and the self enrolment key is flagged, as failed rows.
        var stats = entry.GetOrCreate<ImportFileProcessStatsPart>();
        Assert.Equal(ContentTransferEntryStatus.CompletedWithErrors, entry.Status);
        Assert.True(stats.ImportedCount == 6, $"Imported {stats.ImportedCount}: " + string.Join(" | ", stats.ErrorMessages.Values));
        Assert.Equal(2, stats.ErrorMessages.Count);
        Assert.Contains(stats.ErrorMessages.Values, message => message.Contains("forum", StringComparison.OrdinalIgnoreCase) && message.Contains("Discussion", StringComparison.Ordinal));
        Assert.Contains(stats.ErrorMessages.Values, message => message.Contains("enrollment key", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(stats.ErrorMessages.Values, message => message.Contains("never-imported", StringComparison.Ordinal));

        // The course, its dates, enrolment, completion and category.
        Assert.NotNull(course);
        var part = course.Get<CoursePart>(nameof(CoursePart));
        Assert.Equal(courseId.ToString(System.Globalization.CultureInfo.InvariantCulture), part.SourceIdentifier.Split(':')[^1]);
        Assert.StartsWith("moodle:", part.SourceIdentifier, StringComparison.Ordinal);
        Assert.Equal("WS-M", part.Code);
        Assert.Equal("A course made up for the tests.", part.Summary);
        Assert.Equal(new DateTime(2024, 1, 15, 8, 0, 0, DateTimeKind.Utc), part.StartsUtc);
        Assert.Equal(new DateTime(2025, 1, 15, 8, 0, 0, DateTimeKind.Utc), part.EndsUtc);
        Assert.Equal(EnrollmentMode.Key, part.EnrollmentMode);
        Assert.Equal(30, part.AccessDays);
        var grade = Assert.Single(part.CompletionCriteria, rule => rule.Name == LearningRuleNames.MinimumGrade);
        Assert.Equal("70", grade.Get("Grade"));

        var category = await _fixture.InTenantAsync(async services =>
        {
            var categories = services.GetRequiredService<CourseCategoryService>();
            var tree = await categories.GetTreeAsync();

            return (await categories.GetCategoryIdsAsync(course)).Select(id => tree.Find(id)?.Name).ToList();
        });
        Assert.Equal(["30 Day Learning Plan"], category);

        // The outline: the label joined the section summary; the items are in the activities' order, the activities of
        // the completion criteria required and the others optional.
        var section = Assert.Single(course.Get<CourseOutlinePart>(nameof(CourseOutlinePart)).Sections);
        Assert.Equal("Basics", section.Title);
        Assert.Contains("Start here.", section.Summary, StringComparison.Ordinal);
        Assert.Contains("Welcome to the course.", section.Summary, StringComparison.Ordinal);

        var items = await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();

            return (await contentManager.GetAsync(section.Items.Select(item => item.ContentItemId), VersionOptions.Latest)).ToDictionary(item => item.ContentItemId);
        });
        var ordered = section.Items.Select(item => items[item.ContentItemId]).ToList();
        Assert.Equal(
            [PackagesConstants.ContentTypes.Package, LearningConstants.ContentTypes.FileLesson, LearningConstants.ContentTypes.LinkLesson, AssessmentsConstants.ContentTypes.Quiz, LearningConstants.ContentTypes.PageLesson],
            ordered.Select(item => item.ContentType));
        Assert.Equal([true, false, false, true, false], section.Items.Select(item => item.Required));
        Assert.All(ordered, item => Assert.Equal(course.ContentItemId, item.Get<ContainedPart>(nameof(ContainedPart))?.ListContentItemId));
        Assert.All(ordered, item => Assert.StartsWith(part.SourceIdentifier + ":cm:", item.Get<CourseItemPart>(nameof(CourseItemPart)).SourceIdentifier, StringComparison.Ordinal));

        // The SCORM package, imported through the package importer, completes on a score of 90.
        var package = ordered[0];
        Assert.Equal("Fire safety package", package.DisplayText);
        Assert.Equal(PackagesConstants.Standards.Scorm12, package.Get<LearningPackagePart>(nameof(LearningPackagePart)).Standard);
        Assert.False(string.IsNullOrEmpty(package.Get<LearningPackagePart>(nameof(LearningPackagePart)).PackageFileId));
        Assert.Equal(CompletionModes.Passed, package.Get<CourseItemPart>(nameof(CourseItemPart)).CompletionMode);
        Assert.Equal(90, package.Get<CourseItemPart>(nameof(CourseItemPart)).PassMark);

        // The file, in the media library under the lesson, shown by its own name.
        var file = ordered[1];
        var files = (JsonObject)((JsonObject)file.Content)[LearningConstants.ContentTypes.FileLesson]?["Files"];
        var path = files?["Paths"]?[0]?.GetValue<string>();
        Assert.Equal($"mediafields/{LearningConstants.ContentTypes.FileLesson}/{file.ContentItemId}/guide.pdf", path);
        Assert.NotNull(await _fixture.InTenantAsync(services => services.GetRequiredService<IMediaFileStore>().GetFileInfoAsync(path)));

        // The link and the page.
        Assert.Equal("https://example.com/policy", ((JsonObject)ordered[2].Content)[LearningConstants.ContentTypes.LinkLesson]?["Url"]?["Url"]?.GetValue<string>());
        Assert.Contains("Rules of the site.", ordered[4].Content.HtmlBodyPart.Html.ToString(), StringComparison.Ordinal);

        // The quiz, with its two questions in a bank of the course and its random question as a draw.
        var quiz = ordered[3].Get<QuizPart>(nameof(QuizPart));
        Assert.Equal(2, quiz.QuestionIds.Count);
        Assert.Equal(30, quiz.TimeLimitMinutes);
        Assert.Equal(2, quiz.MaxAttempts);
        Assert.Equal(70, quiz.PassMark);
        var draw = Assert.Single(quiz.DrawRules);
        Assert.Equal(1, draw.Count);

        var questions = await _fixture.InTenantAsync(async services =>
            (await services.GetRequiredService<IContentManager>().GetAsync(quiz.QuestionIds, VersionOptions.Latest)).ToList());
        Assert.Equal(
            [AssessmentsConstants.ContentTypes.SingleChoiceQuestion, AssessmentsConstants.ContentTypes.TrueFalseQuestion],
            questions.Select(question => question.ContentType).Order(StringComparer.Ordinal));
        Assert.All(questions, question => Assert.Equal(draw.BankId, question.Get<ContainedPart>(nameof(ContainedPart))?.ListContentItemId));
        Assert.All(questions, question => Assert.StartsWith("moodle:", question.Get<QuestionPart>(nameof(QuestionPart)).SourceIdentifier, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ImportingABackupAgain_UpdatesTheCourseAndItsItems_InsteadOfMakingNewOnes()
    {
        // Arrange
        var courseId = Random.Shared.Next(100_000, 900_000);
        var title = "Re-imported course " + courseId;
        var backup = MoodleBackupSamples.WorkplaceSafety(courseId, title).BuildZip();

        // Act
        await ImportAsync(backup);
        var first = await FindCourseAsync(title);
        var firstItems = first.Get<CourseOutlinePart>(nameof(CourseOutlinePart)).Sections.SelectMany(section => section.Items).Select(item => item.ContentItemId).ToList();
        var firstQuestions = await QuestionIdsAsync(firstItems);

        var second = await ImportAsync(backup);
        var courses = await _fixture.InTenantAsync(async services => (await services.GetRequiredService<ISession>()
            .Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentType == LearningConstants.ContentTypes.Course && index.DisplayText == title)
            .ListAsync()).ToList());
        var again = Assert.Single(courses);
        var againItems = again.Get<CourseOutlinePart>(nameof(CourseOutlinePart)).Sections.SelectMany(section => section.Items).Select(item => item.ContentItemId).ToList();

        // Assert
        Assert.Equal(6, second.GetOrCreate<ImportFileProcessStatsPart>().ImportedCount);
        Assert.Equal(first.ContentItemId, again.ContentItemId);
        Assert.Equal(firstItems, againItems);
        Assert.Equal(firstQuestions, await QuestionIdsAsync(againItems));

        var children = await _fixture.InTenantAsync(async services => await services.GetRequiredService<ISession>()
            .Query<ContentItem, ContainedPartIndex>(index => index.ListContentItemId == first.ContentItemId)
            .ListAsync());
        Assert.Equal(5, children.Where(child => child.Latest).Select(child => child.ContentItemId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task AnUnsafeBackup_IsOneFailedRow_AndCreatesNothing()
    {
        // Arrange
        var courseId = Random.Shared.Next(100_000, 900_000);
        var title = "Unsafe course " + courseId;
        var backup = MoodleBackupSamples.WorkplaceSafety(courseId, title).RawEntry("../escape.txt", "escape"u8.ToArray()).BuildZip();

        // Act
        var entry = await ImportAsync(backup);

        // Assert
        var stats = entry.GetOrCreate<ImportFileProcessStatsPart>();
        Assert.Equal(0, stats.ImportedCount);
        var message = Assert.Single(stats.ErrorMessages).Value;
        Assert.Contains("safe extraction", message, StringComparison.Ordinal);
        Assert.Null(await FindCourseAsync(title));
    }

    private Task<List<string>> QuestionIdsAsync(IEnumerable<string> itemIds)
        => _fixture.InTenantAsync(async services =>
            (await services.GetRequiredService<IContentManager>().GetAsync(itemIds, VersionOptions.Latest))
                .Where(item => item.ContentType == AssessmentsConstants.ContentTypes.Quiz)
                .SelectMany(item => item.Get<QuizPart>(nameof(QuizPart)).QuestionIds)
                .ToList());

    private Task<ContentItem> FindCourseAsync(string title)
        => _fixture.InTenantAsync(services => services.GetRequiredService<ISession>()
            .Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentType == LearningConstants.ContentTypes.Course && index.DisplayText == title)
            .FirstOrDefaultAsync());

    private async Task<ContentTransferEntry> ImportAsync(byte[] backup)
    {
        var entryId = await _fixture.InTenantAsync(async services =>
        {
            var users = services.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<global::OrchardCore.Users.IUser>>();
            var admin = await users.FindByNameAsync("admin");
            var store = services.GetRequiredService<IContentTransferFileStore>();
            var session = services.GetRequiredService<ISession>();
            var storedName = await store.CreateFileFromStreamAsync(Guid.NewGuid().ToString("n") + ".mbz", new MemoryStream(backup), false);
            var entry = new ContentTransferEntry
            {
                EntryId = Guid.NewGuid().ToString("n")[..26],
                ContentType = LearningConstants.ContentTypes.Course,
                UploadedFileName = "backup-moodle2-course.mbz",
                Owner = await users.GetUserIdAsync(admin),
                StoredFileName = storedName,
                Status = ContentTransferEntryStatus.Pending,
                Direction = ContentTransferDirection.Import,
                CreatedUtc = DateTime.UtcNow,
            };

            entry.Put(new ImportContentOptionsPart
            {
                PublishImportedContent = true,
                RowContentTypes =
                [
                    LearningConstants.ContentTypes.Course,
                    LearningConstants.ContentTypes.PageLesson,
                    LearningConstants.ContentTypes.FileLesson,
                    LearningConstants.ContentTypes.LinkLesson,
                    PackagesConstants.ContentTypes.Package,
                    AssessmentsConstants.ContentTypes.Quiz,
                ],
            });

            await session.SaveAsync(entry, false, collection: null);
            await session.SaveChangesAsync();

            return entry.EntryId;
        });

        await _fixture.InTenantAsync(services =>
        {
            var method = typeof(ImportFilesBackgroundTask).GetMethod("ProcessEntriesAsync", BindingFlags.NonPublic | BindingFlags.Static, [typeof(IServiceProvider), typeof(string), typeof(CancellationToken)]);

            return (Task)method.Invoke(null, [services, entryId, CancellationToken.None]);
        });

        return await _fixture.InTenantAsync(services => services.GetRequiredService<ISession>()
            .Query<ContentTransferEntry, ContentTransferEntryIndex>(index => index.EntryId == entryId)
            .FirstOrDefaultAsync());
    }
}
