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
        Assert.True(stats.ImportedCount == 7, $"Imported {stats.ImportedCount}: " + string.Join(" | ", stats.ErrorMessages.Values));
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

        // The outline: the label is a Text item; the items are in the activities' order, the activities of the
        // completion criteria required and the others (and the text) optional.
        var section = Assert.Single(course.Get<CourseOutlinePart>(nameof(CourseOutlinePart)).Sections);
        Assert.Equal("Basics", section.Title);
        Assert.Contains("Start here.", section.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Welcome to the course.", section.Summary, StringComparison.Ordinal);

        var items = await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();

            return (await contentManager.GetAsync(section.Items.Select(item => item.ContentItemId), VersionOptions.Latest)).ToDictionary(item => item.ContentItemId);
        });
        var all = section.Items.Select(item => items[item.ContentItemId]).ToList();
        Assert.Equal(LearningConstants.ContentTypes.TextBlock, all[0].ContentType);
        Assert.Contains("Welcome to the course.", all[0].Content.HtmlBodyPart.Html.ToString(), StringComparison.Ordinal);
        var ordered = all.Skip(1).ToList();
        Assert.Equal(
            [PackagesConstants.ContentTypes.Package, LearningConstants.ContentTypes.FileLesson, LearningConstants.ContentTypes.LinkLesson, AssessmentsConstants.ContentTypes.Quiz, LearningConstants.ContentTypes.PageLesson],
            ordered.Select(item => item.ContentType));
        Assert.Equal([false, true, false, false, true, false], section.Items.Select(item => item.Required));
        Assert.All(all, item => Assert.Equal(course.ContentItemId, item.Get<ContainedPart>(nameof(ContainedPart))?.ListContentItemId));
        Assert.All(all, item => Assert.StartsWith(part.SourceIdentifier + ":cm:", item.Get<CourseItemPart>(nameof(CourseItemPart)).SourceIdentifier, StringComparison.Ordinal));

        // The SCORM package, imported through the package importer, completes on a score of 90, whatever it reports.
        var package = ordered[0];
        Assert.Equal("Fire safety package", package.DisplayText);
        Assert.Equal(PackagesConstants.Standards.Scorm12, package.Get<LearningPackagePart>(nameof(LearningPackagePart)).Standard);
        Assert.False(string.IsNullOrEmpty(package.Get<LearningPackagePart>(nameof(LearningPackagePart)).PackageFileId));
        Assert.Equal(CompletionModes.MinimumScore, package.Get<CourseItemPart>(nameof(CourseItemPart)).CompletionMode);
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
        Assert.Equal(7, second.GetOrCreate<ImportFileProcessStatsPart>().ImportedCount);
        Assert.Equal(first.ContentItemId, again.ContentItemId);
        Assert.Equal(firstItems, againItems);
        Assert.Equal(firstQuestions, await QuestionIdsAsync(againItems));

        var children = await _fixture.InTenantAsync(async services => await services.GetRequiredService<ISession>()
            .Query<ContentItem, ContainedPartIndex>(index => index.ListContentItemId == first.ContentItemId)
            .ListAsync());
        Assert.Equal(6, children.Where(child => child.Latest).Select(child => child.ContentItemId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task EveryNewlyMappedActivity_IsImported_AndOnlyTheGlossaryAndTheCaptchaAreReported()
    {
        // Arrange
        var courseId = Random.Shared.Next(100_000, 900_000);
        var title = "Every activity " + courseId;
        var backup = MoodleBackupSamples.EveryActivity(courseId, title).BuildZip();

        // Act
        var entry = await ImportAsync(backup);
        var course = await FindCourseAsync(title);

        // Assert: the report. The glossary has no equivalent; the captcha of the feedback and the hidden page are flagged.
        var stats = entry.GetOrCreate<ImportFileProcessStatsPart>();
        var messages = string.Join(" | ", stats.ErrorMessages.Values);
        Assert.True(stats.ImportedCount == 10, $"Imported {stats.ImportedCount}: " + messages);
        Assert.Contains(stats.ErrorMessages.Values, message => message.Contains("glossary", StringComparison.OrdinalIgnoreCase) && message.Contains("Terms", StringComparison.Ordinal));
        Assert.Contains(stats.ErrorMessages.Values, message => message.Contains("captcha", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(stats.ErrorMessages.Values, message => message.Contains("Hidden notes", StringComparison.Ordinal));
        Assert.DoesNotContain(stats.ErrorMessages.Values, message => message.Contains("assignment", StringComparison.OrdinalIgnoreCase) || message.Contains("new window", StringComparison.OrdinalIgnoreCase) || message.Contains("attempts", StringComparison.OrdinalIgnoreCase));

        // The course: its picture and the picture of its summary in the media library, completion by the instructors.
        var part = course.Get<CoursePart>(nameof(CoursePart));
        var cover = ((JsonObject)course.Content)[LearningConstants.ContentTypes.Course]?["Cover"]?["Paths"]?[0]?.GetValue<string>();
        Assert.False(string.IsNullOrEmpty(cover));
        Assert.NotNull(await _fixture.InTenantAsync(services => services.GetRequiredService<IMediaFileStore>().GetFileInfoAsync(cover)));
        var summary = course.Content.HtmlBodyPart.Html.ToString();
        Assert.DoesNotContain("@@PLUGINFILE@@", summary, StringComparison.Ordinal);
        Assert.Contains("<img", summary, StringComparison.Ordinal);
        var catalog = await (await _fixture.CreateClientAsync()).GetStringAsync("learning/catalog?q=" + courseId, TestContext.Current.CancellationToken);
        Assert.Contains("data-course-cover", catalog, StringComparison.Ordinal);
        var approval = Assert.Single(part.CompletionCriteria, rule => rule.Name == LearningRuleNames.StaffApproval);
        Assert.Equal(LearningConstants.CourseStaffRoles.Instructor, approval.Get("Role"));
        Assert.False(part.NeverCompletes);

        var section = Assert.Single(course.Get<CourseOutlinePart>(nameof(CourseOutlinePart)).Sections);
        var items = await _fixture.InTenantAsync(async services =>
            (await services.GetRequiredService<IContentManager>().GetAsync(section.Items.Select(item => item.ContentItemId), VersionOptions.Latest)).ToDictionary(item => item.ContentItemId));
        var ordered = section.Items.Select(item => items[item.ContentItemId]).ToList();
        Assert.Equal(
        [
            LearningConstants.ContentTypes.TextBlock,
            CrestApps.OrchardCore.Learning.Coursework.CourseworkConstants.ContentTypes.Coursework,
            CrestApps.OrchardCore.Learning.Surveys.SurveysConstants.ContentTypes.FeedbackSurvey,
            CrestApps.OrchardCore.Learning.Surveys.SurveysConstants.ContentTypes.Choice,
            PackagesConstants.ContentTypes.Package,
            AssessmentsConstants.ContentTypes.Quiz,
            PackagesConstants.ContentTypes.Package,
            LearningConstants.ContentTypes.PageLesson,
            LearningConstants.ContentTypes.PageLesson,
        ], ordered.Select(item => item.ContentType));

        // The label, with its picture in the media library.
        var text = ordered[0].Content.HtmlBodyPart.Html.ToString();
        Assert.Contains("Read this first", text, StringComparison.Ordinal);
        Assert.Contains("<img", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@@PLUGINFILE@@", text, StringComparison.Ordinal);
        Assert.False(section.Items[0].Required);

        // The assignment as coursework.
        var coursework = ordered[1].Get<CrestApps.OrchardCore.Learning.Coursework.Models.CourseworkPart>("CourseworkPart");
        Assert.Equal(new DateTime(2025, 3, 1, 17, 0, 0, DateTimeKind.Utc), coursework.DueUtc);
        Assert.Equal(20, coursework.MaxPoints);
        Assert.Equal(CrestApps.OrchardCore.Learning.Coursework.Models.CourseworkSubmissionType.TextAndFiles, coursework.SubmissionType);
        Assert.Equal(5, coursework.MaxFiles);
        Assert.Equal(CompletionModes.Submitted, ordered[1].Get<CourseItemPart>(nameof(CourseItemPart)).CompletionMode);

        // The feedback and the choice as surveys.
        var feedback = ordered[2].Get<CrestApps.OrchardCore.Learning.Surveys.Models.SurveyPart>("SurveyPart");
        Assert.Equal(["How did you hear of it?", "What would you change?"], feedback.Questions.Select(question => question.Text));
        Assert.Equal(["Friend", "Web"], feedback.Questions[0].Options);
        Assert.True(feedback.Questions[0].Required);
        var choice = ordered[3].Get<CrestApps.OrchardCore.Learning.Surveys.Models.SurveyPart>("SurveyPart");
        Assert.Equal(["Monday", "Friday"], Assert.Single(choice.Questions).Options);
        Assert.Equal(CrestApps.OrchardCore.Learning.Surveys.Models.SurveyResultsVisibility.AfterAnswer, choice.ResultsVisibility);

        // The IMS package, which completes when the learner opens it.
        var ims = ordered[4];
        Assert.Equal(PackagesConstants.Standards.ImsContentPackage, ims.Get<LearningPackagePart>(nameof(LearningPackagePart)).Standard);
        Assert.Equal(CompletionModes.Viewed, ims.Get<CourseItemPart>(nameof(CourseItemPart)).CompletionMode);

        // The quiz: two questions a page, immediate feedback, review options, and the new question types.
        var quiz = ordered[5].Get<QuizPart>(nameof(QuizPart));
        Assert.Equal(2, quiz.QuestionsPerPage);
        Assert.Equal(QuizFeedbackMode.Immediate, quiz.FeedbackMode);
        Assert.NotNull(quiz.ReviewOptions);
        Assert.Equal(ReviewShows.Score, quiz.ReviewOptions.During);
        Assert.Equal(ReviewShows.Score | ReviewShows.RightAnswers, quiz.ReviewOptions.AfterClose);
        var questions = await _fixture.InTenantAsync(async services =>
            (await services.GetRequiredService<IContentManager>().GetAsync(quiz.QuestionIds, VersionOptions.Latest)).ToDictionary(question => question.ContentItemId));
        Assert.Equal(
            [AssessmentsConstants.ContentTypes.SurveyQuestion, AssessmentsConstants.ContentTypes.DragIntoTextQuestion, AssessmentsConstants.ContentTypes.GapSelectQuestion, AssessmentsConstants.ContentTypes.DescriptionQuestion],
            quiz.QuestionIds.Select(id => questions[id].ContentType));

        // The SCORM package: a new window, two attempts, the average of them, and a score of at least 80.
        var scorm = ordered[6].Get<LearningPackagePart>(nameof(LearningPackagePart));
        Assert.True(scorm.OpenInNewWindow);
        Assert.Equal(2, scorm.MaxAttempts);
        Assert.Equal(GradingMethod.Average, scorm.GradingMethod);
        Assert.Equal(CompletionModes.MinimumScore, ordered[6].Get<CourseItemPart>(nameof(CourseItemPart)).CompletionMode);
        Assert.Equal(80, ordered[6].Get<CourseItemPart>(nameof(CourseItemPart)).PassMark);

        // The page open only to the person with that e-mail address, and the hidden page, which is optional.
        var adminId = await _fixture.InTenantAsync(async services =>
        {
            var users = services.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<global::OrchardCore.Users.IUser>>();

            return await users.GetUserIdAsync(await users.FindByNameAsync("admin"));
        });
        var people = Assert.Single(ordered[7].Get<CourseItemPart>(nameof(CourseItemPart)).ReleaseRules, rule => rule.Name == LearningRuleNames.People);
        Assert.Equal(adminId, people.Get("UserIds"));
        Assert.False(section.Items[8].Required);
    }

    [Fact]
    public async Task CohortSync_BecomesTheCourseCohortSync_AndACohortTheSiteDoesNotHaveIsReported()
    {
        // Arrange
        var courseId = Random.Shared.Next(100_000, 900_000);
        var title = "Cohort sync " + courseId;
        var cohortName = "Moodle crew " + courseId;
        var cohortId = await _fixture.InTenantAsync(async services =>
            (await services.GetRequiredService<CrestApps.OrchardCore.Users.ICohortService>().CreateAsync(cohortName, null)).Cohort.CohortId);
        var backup = new MoodleBackupBuilder(courseId, title, "CS-" + courseId)
            .Section(1, 0, null)
            .Section(2, 1, "Only section")
            .Enrolment("manual")
            .Cohort(31, cohortName)
            .Cohort(32, "Missing crew " + courseId)
            .Page(2, 3, "Read me", "<p>Text.</p>")
            .BuildZip();

        // Act
        var entry = await ImportAsync(backup);
        var course = await FindCourseAsync(title);

        // Assert
        var stats = entry.GetOrCreate<ImportFileProcessStatsPart>();
        var message = Assert.Single(stats.ErrorMessages).Value;
        Assert.Contains("Missing crew " + courseId, message, StringComparison.Ordinal);
        Assert.Equal([cohortId], course.Get<LearningCohortsPart>(nameof(LearningCohortsPart)).CohortIds);
    }

    [Fact]
    public async Task Round11L2_AnUnknownCompletionRole_IsFlagged_SeveralRolesAreFlagged_AndTheEditorKeepsTheRole()
    {
        // Arrange: completion when a manager or a teacher marks it; manager is no course staff role.
        var courseId = Random.Shared.Next(100_000, 900_000);
        var title = "Role completion " + courseId;
        var backup = new MoodleBackupBuilder(courseId, title, "RC-" + courseId)
            .Section(1, 0, null)
            .Section(2, 1, "Only section")
            .Enrolment("manual")
            .RoleCriterion(1, "manager")
            .RoleCriterion(3, "editingteacher")
            .Page(2, 3, "Read me", "<p>Text.</p>")
            .BuildZip();

        // Act
        var entry = await ImportAsync(backup);
        var course = await FindCourseAsync(title);

        using var admin = await _fixture.CreateClientAsync("admin");
        var editor = await admin.GetStringAsync($"Admin/Contents/ContentItems/{course.ContentItemId}/Edit", TestContext.Current.CancellationToken);

        // Assert
        var messages = entry.GetOrCreate<ImportFileProcessStatsPart>().ErrorMessages.Values;
        Assert.Contains(messages, message => message.Contains("manager", StringComparison.Ordinal) && message.Contains("no course staff role", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("several roles", StringComparison.Ordinal));
        var approval = Assert.Single(course.Get<CoursePart>(nameof(CoursePart)).CompletionCriteria, rule => rule.Name == LearningRuleNames.StaffApproval);
        Assert.Equal("manager", approval.Get("Role"));
        Assert.Matches("<option[^>]*selected[^>]*value=\"manager\"|<option[^>]*value=\"manager\"[^>]*selected", editor);
    }

    [Fact]
    public async Task Round11S1_AnImporterWhoMayNotManageEnrollments_LinksNoCohort_AndIsToldTheSameAsForAMissingCohort()
    {
        // Arrange: a cohort the site has and one it does not; the instructor (Manage own courses only) imports.
        var courseId = Random.Shared.Next(100_000, 900_000);
        var title = "Cohort by instructor " + courseId;
        var cohortName = "Staff crew " + courseId;
        var missingName = "Absent crew " + courseId;
        await _fixture.InTenantAsync(async services =>
            (await services.GetRequiredService<CrestApps.OrchardCore.Users.ICohortService>().CreateAsync(cohortName, null)).Cohort.CohortId);
        var backup = new MoodleBackupBuilder(courseId, title, "CI-" + courseId)
            .Section(1, 0, null)
            .Section(2, 1, "Only section")
            .Enrolment("manual")
            .Cohort(31, cohortName)
            .Cohort(32, missingName)
            .Page(2, 3, "Read me", "<p>Text.</p>")
            .BuildZip();

        // Act
        var entry = await ImportAsync(backup, "instructor1");
        var course = await FindCourseAsync(title);

        // Assert
        var messages = entry.GetOrCreate<ImportFileProcessStatsPart>().ErrorMessages.Values.ToArray();
        var known = Assert.Single(messages, message => message.Contains(cohortName, StringComparison.Ordinal));
        var missing = Assert.Single(messages, message => message.Contains(missingName, StringComparison.Ordinal));
        Assert.Equal(missing.Replace(missingName, "X", StringComparison.Ordinal), known.Replace(cohortName, "X", StringComparison.Ordinal));
        Assert.NotNull(course);
        Assert.Empty(course.TryGet<LearningCohortsPart>(out var cohorts) ? cohorts.CohortIds : []);
    }

    [Fact]
    public async Task ACourseWithoutCompletionCriteria_NeverCompletes()
    {
        // Arrange
        var courseId = Random.Shared.Next(100_000, 900_000);
        var title = "No criteria " + courseId;
        var backup = new MoodleBackupBuilder(courseId, title, "NC-" + courseId)
            .Section(1, 0, null)
            .Section(2, 1, "Only section")
            .Enrolment("manual")
            .Page(2, 3, "Read me", "<p>Text.</p>")
            .BuildZip();

        // Act
        await ImportAsync(backup);
        var course = await FindCourseAsync(title);

        // Assert
        Assert.True(course.Get<CoursePart>(nameof(CoursePart)).NeverCompletes);
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

    private async Task<ContentTransferEntry> ImportAsync(byte[] backup, string ownerName = "admin")
    {
        var entryId = await _fixture.InTenantAsync(async services =>
        {
            var users = services.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<global::OrchardCore.Users.IUser>>();
            var admin = await users.FindByNameAsync(ownerName);
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
                    LearningConstants.ContentTypes.TextBlock,
                    CrestApps.OrchardCore.Learning.Coursework.CourseworkConstants.ContentTypes.Coursework,
                    CrestApps.OrchardCore.Learning.Surveys.SurveysConstants.ContentTypes.FeedbackSurvey,
                    CrestApps.OrchardCore.Learning.Surveys.SurveysConstants.ContentTypes.Choice,
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
