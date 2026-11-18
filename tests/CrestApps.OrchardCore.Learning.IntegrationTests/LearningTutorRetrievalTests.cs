using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.DataSources;
using CrestApps.Core.Infrastructure.Indexing.Models;
using CrestApps.OrchardCore.AI.Core.DataSources;
using CrestApps.OrchardCore.Learning.AI.Models;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Html.Models;
using OrchardCore.Lists.Models;
using OrchardCore.Title.Models;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// The tutor's retrieval on a real site: what the knowledge base search returns passes through every registered result
/// filter, exactly as the AI data source search does before the chunks reach the model's context. Exam content
/// (questions with their answer keys, quizzes and question banks) never gets through, not even when the learner asks
/// for it (the chunks below are what a search for "the answers to the quiz" would match) and not even for a learner
/// enrolled in the course.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class LearningTutorRetrievalTests
{
    private const string AnswerKey = "The answer key: the correct answer is B, the extinguisher class for electrical fires.";

    private readonly LearningSiteFixture _site;

    public LearningTutorRetrievalTests(LearningSiteFixture site)
    {
        _site = site;
    }

    [Fact]
    public async Task ExamContent_NeverReachesTheTutorsContext_EvenForAnEnrolledLearnerWhoAsksForIt()
    {
        var setup = await _site.InTenantAsync(SetUpAsync);

        var kept = await _site.InTenantAsync(services => FilterAsync(services, setup.LearnerName, Tutor(), setup.Chunks));

        Assert.Equal([setup.EnabledLessonId], kept.Select(chunk => chunk.ReferenceId).Distinct());
        Assert.DoesNotContain(kept, chunk => chunk.Content.Contains(AnswerKey, StringComparison.Ordinal));
        Assert.DoesNotContain(kept, chunk => setup.ExamIds.Contains(chunk.ReferenceId));
    }

    [Fact]
    public async Task ExamContent_NeverReachesAnyProfile_EvenForSomeoneWhoMayReadAnswerKeys()
    {
        var setup = await _site.InTenantAsync(SetUpAsync);

        var kept = await _site.InTenantAsync(services => FilterAsync(services, "admin", new AIProfile { ItemId = "plain" }, setup.Chunks));

        Assert.DoesNotContain(kept, chunk => setup.ExamIds.Contains(chunk.ReferenceId));
        Assert.DoesNotContain(kept, chunk => chunk.Content.Contains(AnswerKey, StringComparison.Ordinal));
        Assert.Contains(kept, chunk => chunk.ReferenceId == setup.EnabledLessonId);
    }

    [Fact]
    public async Task Tutor_AnswersOnlyFromLessonsOfEnrolledCoursesWithTheAssistantOn()
    {
        var setup = await _site.InTenantAsync(SetUpAsync);

        var kept = await _site.InTenantAsync(services => FilterAsync(services, setup.LearnerName, Tutor(), setup.Chunks));
        var stranger = await _site.InTenantAsync(services => FilterAsync(services, "learner2", Tutor(), setup.Chunks));

        Assert.DoesNotContain(kept, chunk => chunk.ReferenceId == setup.DisabledCourseLessonId);
        Assert.DoesNotContain(kept, chunk => chunk.ReferenceId == setup.NotEnrolledLessonId);
        Assert.DoesNotContain(kept, chunk => chunk.ReferenceId == "not-a-content-item");
        Assert.Empty(stranger);
    }

    [Fact]
    public async Task Round9M11_ACourseItemOfACourseWithTheAssistantOff_ReachesNoProfile()
    {
        var setup = await _site.InTenantAsync(SetUpAsync);

        // Someone who may read every lesson, through a profile that is not marked as a tutor.
        var kept = await _site.InTenantAsync(services => FilterAsync(services, "admin", new AIProfile { ItemId = "plain" }, setup.Chunks));

        Assert.DoesNotContain(kept, chunk => chunk.ReferenceId == setup.DisabledCourseLessonId);
        Assert.Contains(kept, chunk => chunk.ReferenceId == setup.EnabledLessonId);
    }

    [Fact]
    public async Task Round9M11_ATutorMadeByTheRecipeStep_IsMarkedAsATutor()
    {
        var name = "recipe-tutor-" + Guid.NewGuid().ToString("n")[..8];

        await _site.InTenantAsync(services => LearningSiteFixture.RunRecipeAsync(services, new System.Text.Json.Nodes.JsonObject
        {
            ["name"] = "Round9TutorFromTemplate",
            ["steps"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject
            {
                ["name"] = "CreateAIProfileFromTemplate",
                ["Profiles"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject
                {
                    ["TemplateId"] = CrestApps.OrchardCore.Learning.AI.LearningAIConstants.Templates.Tutor,
                    ["Name"] = name,
                    ["DisplayText"] = "Recipe tutor",
                }),
            }),
        }));

        var profile = await _site.InTenantAsync(async services => await services.GetRequiredService<CrestApps.Core.AI.Profiles.IAIProfileManager>().FindByNameAsync(name));

        Assert.NotNull(profile);
        Assert.True(profile.TryGet<LearningTutorProfileMetadata>(out var metadata) && metadata.IsTutor);
    }

    [Fact]
    public async Task Round9H6_SeveralPhrasesSearchedAtOnce_AreFilteredOneAtATime_OnTheSitesStore()
    {
        // Arrange: the knowledge base search wrapped as AI Data Sources wraps it, with every filter of the site and a
        // probe that records how many filter runs overlap. CrestApps.Core searches each phrase of a question at once
        // (Task.WhenAll on the same content manager); the Learning filter reads the request's YesSql session.
        var setup = await _site.InTenantAsync(SetUpAsync);

        // Act
        var (results, overlap) = await _site.InTenantAsync(async services =>
        {
            var userManager = services.GetRequiredService<UserManager<IUser>>();
            var principal = await services.GetRequiredService<IUserClaimsPrincipalFactory<IUser>>().CreateAsync(await userManager.FindByNameAsync(setup.LearnerName));
            var accessor = services.GetRequiredService<IHttpContextAccessor>();
            accessor.HttpContext.User = principal;

            var probe = new OverlapProbe();
            var search = new FilteringDataSourceContentManager(
                new FixedSearch(setup.Chunks),
                [probe, .. services.GetServices<IAIDataSourceResultFilter>()],
                accessor,
                new AIDataSourceResultFilterOptions());

            var phrases = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => search.SearchAsync(null, [1f], "learning-kb", 10, null, TestContext.Current.CancellationToken)));

            return (phrases.Select(found => found.Select(chunk => chunk.ReferenceId).ToArray()).ToArray(), probe.MaxOverlap);
        });

        // Assert: one filter run at a time, and every phrase got the same, filtered, chunks.
        Assert.Equal(1, overlap);
        Assert.All(results, ids => Assert.Equal(results[0], ids));
        Assert.Contains(setup.EnabledLessonId, results[0]);
        Assert.DoesNotContain(results[0], id => setup.ExamIds.Contains(id));
    }

    private sealed class OverlapProbe : IAIDataSourceResultFilter
    {
        private int _current;

        public int MaxOverlap { get; private set; }

        public async Task<IReadOnlyList<DataSourceSearchResult>> FilterAsync(AIDataSourceResultFilterContext context, IReadOnlyList<DataSourceSearchResult> results)
        {
            var now = Interlocked.Increment(ref _current);

            lock (this)
            {
                MaxOverlap = Math.Max(MaxOverlap, now);
            }

            await Task.Delay(50, context.CancellationToken);
            Interlocked.Decrement(ref _current);

            return results;
        }
    }

    private sealed class FixedSearch : IDataSourceContentManager
    {
        private readonly IReadOnlyList<DataSourceSearchResult> _results;

        public FixedSearch(IReadOnlyList<DataSourceSearchResult> results)
        {
            _results = results;
        }

        public Task<IEnumerable<DataSourceSearchResult>> SearchAsync(IIndexProfileInfo indexProfile, float[] embedding, string dataSourceId, int topN, string filter = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<DataSourceSearchResult>>(_results.ToList());

        public Task<DataSourceSearchOutcome> TrySearchAsync(IIndexProfileInfo indexProfile, float[] embedding, string dataSourceId, int topN, string filter = null, CancellationToken cancellationToken = default)
            => Task.FromResult(DataSourceSearchOutcome.Success(_results.ToList()));

        public Task<long> DeleteByDataSourceIdAsync(IIndexProfileInfo indexProfile, string dataSourceId, CancellationToken cancellationToken = default)
            => Task.FromResult(0L);

        public Task<bool> DeleteByReferenceIdsAsync(IIndexProfileInfo profile, string dataSourceId, IReadOnlyCollection<string> referenceIds, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private static AIProfile Tutor()
    {
        var profile = new AIProfile { ItemId = "tutor" };
        profile.Put(new LearningTutorProfileMetadata { IsTutor = true });

        return profile;
    }

    private static async Task<IReadOnlyList<DataSourceSearchResult>> FilterAsync(IServiceProvider services, string userName, AIProfile profile, IReadOnlyList<DataSourceSearchResult> chunks)
    {
        var userManager = services.GetRequiredService<UserManager<IUser>>();
        var user = await userManager.FindByNameAsync(userName);
        var principal = await services.GetRequiredService<IUserClaimsPrincipalFactory<IUser>>().CreateAsync(user);
        services.GetRequiredService<IHttpContextAccessor>().HttpContext.User = principal;

        var filters = services.GetServices<IAIDataSourceResultFilter>().ToArray();
        Assert.NotEmpty(filters);

        var context = new AIDataSourceResultFilterContext
        {
            DataSourceId = "learning-kb",
            User = principal,
            Resource = profile,
            ProfileId = profile.ItemId,
            CancellationToken = TestContext.Current.CancellationToken,
        };

        IReadOnlyList<DataSourceSearchResult> kept = chunks;

        foreach (var filter in filters)
        {
            kept = await filter.FilterAsync(context, kept);
        }

        return kept;
    }

    private static async Task<TutorSetup> SetUpAsync(IServiceProvider services)
    {
        var suffix = Guid.NewGuid().ToString("n")[..8];
        var contentManager = services.GetRequiredService<IContentManager>();
        var learnerName = "tutor-learner-" + suffix;
        var learnerId = await LearningSiteFixture.CreateUserAsync(services, learnerName);

        var enabled = await CourseAsync(contentManager, "Fire safety " + suffix, assistant: true);
        var disabled = await CourseAsync(contentManager, "Ladder safety " + suffix, assistant: false);
        var other = await CourseAsync(contentManager, "First aid " + suffix, assistant: true);

        var lesson = await ItemAsync(contentManager, enabled, LearningConstants.ContentTypes.PageLesson, "Extinguisher classes", "Class C extinguishers are for electrical fires.");
        var quiz = await ItemAsync(contentManager, enabled, AssessmentsConstants.ContentTypes.Quiz, "Fire safety quiz", AnswerKey);
        var disabledLesson = await ItemAsync(contentManager, disabled, LearningConstants.ContentTypes.PageLesson, "Ladder angles", "Use the one-in-four rule.");
        var otherLesson = await ItemAsync(contentManager, other, LearningConstants.ContentTypes.PageLesson, "Recovery position", "Roll the person onto their side.");

        await OutlineAsync(contentManager, enabled, lesson, quiz);
        await OutlineAsync(contentManager, disabled, disabledLesson);
        await OutlineAsync(contentManager, other, otherLesson);

        var bank = await contentManager.NewAsync(AssessmentsConstants.ContentTypes.QuestionBank);
        bank.DisplayText = "Fire safety bank " + suffix;
        bank.Alter<TitlePart>(part => part.Title = bank.DisplayText);
        await contentManager.CreateAsync(bank, VersionOptions.Published);

        var question = await contentManager.NewAsync(AssessmentsConstants.ContentTypes.EssayQuestion);
        question.DisplayText = "Which extinguisher for electrical fires?";
        question.Alter<TitlePart>(part => part.Title = question.DisplayText);
        question.Alter<QuestionPart>(part =>
        {
            part.Prompt = "Which extinguisher class is for electrical fires?";
            part.GeneralFeedback = AnswerKey;
        });
        question.Alter<EssayQuestionPart>(part => part.GraderGuidance = AnswerKey);
        await contentManager.CreateAsync(question, VersionOptions.Published);

        var enrollments = services.GetRequiredService<IEnrollmentService>();

        foreach (var courseId in new[] { enabled.ContentItemId, disabled.ContentItemId })
        {
            var result = await enrollments.EnrollAsync(new EnrollmentRequest
            {
                UserId = learnerId,
                CourseId = courseId,
                Source = LearningConstants.EnrollmentSources.Manual,
                SourceId = "tutor-test",
            });

            Assert.True(result.Succeeded, result.ErrorCode);
        }

        var chunks = new List<DataSourceSearchResult>
        {
            Chunk(question.ContentItemId, "Question: which extinguisher class is for electrical fires? " + AnswerKey),
            Chunk(quiz.ContentItemId, "Fire safety quiz. " + AnswerKey),
            Chunk(bank.ContentItemId, "Fire safety bank. " + AnswerKey),
            Chunk(lesson.ContentItemId, "Class C extinguishers are for electrical fires."),
            Chunk(disabledLesson.ContentItemId, "Use the one-in-four rule."),
            Chunk(otherLesson.ContentItemId, "Roll the person onto their side."),
            Chunk("not-a-content-item", "A document about fire drills."),
        };

        return new TutorSetup(
            learnerName,
            lesson.ContentItemId,
            disabledLesson.ContentItemId,
            otherLesson.ContentItemId,
            [question.ContentItemId, quiz.ContentItemId, bank.ContentItemId],
            chunks);
    }

    private static DataSourceSearchResult Chunk(string referenceId, string content)
        => new()
        {
            ReferenceId = referenceId,
            ReferenceType = "Content",
            DataSourceId = "learning-kb",
            Content = content,
            Score = 0.9f,
        };

    private static async Task<ContentItem> CourseAsync(IContentManager contentManager, string title, bool assistant)
    {
        var course = await contentManager.NewAsync(LearningConstants.ContentTypes.Course);
        course.DisplayText = title;
        course.Alter<TitlePart>(part => part.Title = title);
        course.Alter<CoursePart>(part => part.EnrollmentMode = EnrollmentMode.Open);
        course.Alter<LearningAssistantPart>(part => part.Enabled = assistant);
        await contentManager.CreateAsync(course, VersionOptions.Draft);

        return course;
    }

    private static async Task<ContentItem> ItemAsync(IContentManager contentManager, ContentItem course, string contentType, string title, string body)
    {
        var item = await contentManager.NewAsync(contentType);
        item.DisplayText = title;
        item.Alter<TitlePart>(part => part.Title = title);
        item.Alter<HtmlBodyPart>(part => part.Html = $"<p>{body}</p>");
        item.Alter<ContainedPart>(part =>
        {
            part.ListContentItemId = course.ContentItemId;
            part.ListContentType = course.ContentType;
        });
        await contentManager.CreateAsync(item, VersionOptions.Published);

        return item;
    }

    private static async Task OutlineAsync(IContentManager contentManager, ContentItem course, params ContentItem[] items)
    {
        course.Alter<CourseOutlinePart>(part => part.Sections =
        [
            new CourseSection
            {
                Id = "s1",
                Title = "Basics",
                Items = items.Select(item => new CourseOutlineItem { ContentItemId = item.ContentItemId }).ToList(),
            },
        ]);
        await contentManager.UpdateAsync(course);
        await contentManager.PublishAsync(course);
    }

    private sealed record TutorSetup(
        string LearnerName,
        string EnabledLessonId,
        string DisabledCourseLessonId,
        string NotEnrolledLessonId,
        string[] ExamIds,
        IReadOnlyList<DataSourceSearchResult> Chunks);
}
