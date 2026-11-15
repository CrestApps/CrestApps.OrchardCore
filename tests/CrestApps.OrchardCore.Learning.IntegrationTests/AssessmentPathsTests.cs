using System.Net;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Lists.Models;
using OrchardCore.Title.Models;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// Assessments on a running site: learner answers reach the grading page encoded, and a question's key never leaves
/// through the paths that do not ask for each item.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class AssessmentPathsTests
{
    private const string AcceptedAnswer = "Stairwell-B-only";
    private const string GraderGuidance = "Look for the assembly point";

    private static readonly SemaphoreSlim _setupLock = new(1, 1);
    private static (LearningSiteFixture Fixture, Setup Value)? _setup;

    private readonly LearningSiteFixture _fixture;

    public AssessmentPathsTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task LearnerAnswers_ReachTheGradingPageEncoded()
    {
        // Arrange
        var setup = await SetupAsync();
        var attemptId = await AsLearnerAsync(async (services, user, userId) =>
        {
            var attempts = services.GetRequiredService<IAssessmentAttemptService>();
            var started = await attempts.StartAsync(new StartAttemptRequest { UserId = userId, User = user, QuizId = setup.QuizId, HonorConfirmed = true });

            Assert.True(started.Succeeded, started.ErrorCode);

            await attempts.SaveResponseAsync(started.Attempt.ItemId, userId, 0, new AttemptResponse { Texts = ["<script>alert('essay')</script> Leave by the stairs"] });
            await attempts.SaveResponseAsync(started.Attempt.ItemId, userId, 1, new AttemptResponse { Texts = ["<img src=x onerror=alert('short')>"] });
            await attempts.SubmitAsync(started.Attempt.ItemId, userId);

            return started.Attempt.ItemId;
        });

        using var admin = await _fixture.CreateClientAsync("admin");

        // Act
        var page = await admin.GetAsync($"Admin/learning/grading/attempts/{attemptId}", TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Leave by the stairs", html);
        Assert.DoesNotContain("<script>alert('essay')</script>", html);
        Assert.DoesNotContain("<img src=x onerror", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public async Task AQuestionsKey_DoesNotLeaveByTheDisplayRoute_TheContentApi_ABanksFeed_OrSearchResults()
    {
        // Arrange
        var setup = await SetupAsync();
        await _fixture.InTenantAsync(services => LearningSiteFixture.RunRecipeAsync(services, new JsonObject
        {
            ["name"] = "LearningQuestionSearch",
            ["steps"] = new JsonArray(
                new JsonObject
                {
                    ["name"] = "CreateOrUpdateIndexProfile",
                    ["indexes"] = new JsonArray(new JsonObject
                    {
                        ["IndexName"] = "questions",
                        ["ProviderName"] = "Lucene",
                        ["Type"] = "Content",
                        ["Name"] = "questions",
                        ["Properties"] = new JsonObject
                        {
                            ["LuceneIndexMetadata"] = new JsonObject { ["StoreSourceData"] = false, ["AnalyzerName"] = "standardanalyzer" },
                            ["ContentIndexMetadata"] = new JsonObject { ["IndexLatest"] = false, ["IndexedContentTypes"] = new JsonArray(AssessmentsConstants.ContentTypes.ShortTextQuestion, AssessmentsConstants.ContentTypes.EssayQuestion), ["Culture"] = "any" },
                            ["LuceneIndexDefaultQueryMetadata"] = new JsonObject
                            {
                                ["DefaultVersion"] = "LUCENE_30",
                                ["QueryAnalyzerName"] = "standardanalyzer",
                                ["AllowLuceneQueries"] = false,
                                ["DefaultSearchFields"] = new JsonArray("Content.ContentItem.FullText"),
                            },
                        },
                        ["IndexLatest"] = false,
                        ["Culture"] = "any",
                        ["IndexedContentTypes"] = new JsonArray(AssessmentsConstants.ContentTypes.ShortTextQuestion, AssessmentsConstants.ContentTypes.EssayQuestion),
                    }),
                },
                new JsonObject { ["name"] = "RebuildIndex", ["includeAll"] = false, ["indexNames"] = new JsonArray("questions") },
                new JsonObject
                {
                    ["name"] = "Roles",
                    ["Roles"] = new JsonArray(new JsonObject
                    {
                        ["Name"] = "Anonymous",
                        ["Description"] = "Anonymous role",
                        ["PermissionBehavior"] = "Add",
                        ["Permissions"] = new JsonArray("QueryIndex_questions"),
                    }),
                }),
        }));

        using var anonymous = await _fixture.CreateClientAsync();
        using var learner = await _fixture.CreateClientAsync("learner1");

        // Act
        var bodies = new List<(string Path, string Body)>();

        foreach (var client in new[] { anonymous, learner })
        {
            foreach (var path in new[]
            {
                $"Contents/ContentItems/{setup.ShortTextId}",
                $"api/content/{setup.ShortTextId}",
                $"OrchardCore.Feeds/Feed/Index?format=rss&contentItemId={setup.BankId}",
            })
            {
                var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
                bodies.Add((path, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
            }
        }

        // The index is built in the background: wait until the search answers with the question, or give up after a minute.
        var search = string.Empty;

        for (var attempt = 0; attempt < 60 && !search.Contains("Fire exit short answer", StringComparison.Ordinal); attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(1000, TestContext.Current.CancellationToken);
            }

            search = await (await anonymous.GetAsync("search/questions?Terms=exit", TestContext.Current.CancellationToken)).Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        }

        bodies.Add(("search", search));

        // Assert: the search did find the question, so its result page is checked, not an empty one.
        Assert.Contains("Fire exit short answer", search);
        Assert.All(bodies, entry =>
        {
            Assert.DoesNotContain(AcceptedAnswer, entry.Body);
            Assert.DoesNotContain(GraderGuidance, entry.Body);
        });
    }

    private async Task<Setup> SetupAsync()
    {
        await _setupLock.WaitAsync(TestContext.Current.CancellationToken);

        try
        {
            if (_setup is { } existing && ReferenceEquals(existing.Fixture, _fixture))
            {
                return existing.Value;
            }

            var value = await _fixture.InTenantAsync(async services =>
            {
                var contentManager = services.GetRequiredService<IContentManager>();

                var bank = await contentManager.NewAsync(AssessmentsConstants.ContentTypes.QuestionBank);
                bank.DisplayText = "Fire bank";
                bank.Alter<TitlePart>(part => part.Title = "Fire bank");
                await contentManager.CreateAsync(bank, VersionOptions.Published);

                var essay = await contentManager.NewAsync(AssessmentsConstants.ContentTypes.EssayQuestion);
                essay.DisplayText = "Exit route essay";
                essay.Alter<TitlePart>(part => part.Title = "Exit route essay");
                essay.Alter<QuestionPart>(part =>
                {
                    part.Prompt = "<p>Describe your exit route.</p>";
                    part.Points = 2;
                });
                essay.Alter<EssayQuestionPart>(part => part.GraderGuidance = GraderGuidance);
                essay.Alter<ContainedPart>(part =>
                {
                    part.ListContentItemId = bank.ContentItemId;
                    part.ListContentType = bank.ContentType;
                });
                await contentManager.CreateAsync(essay, VersionOptions.Published);

                var shortText = await contentManager.NewAsync(AssessmentsConstants.ContentTypes.ShortTextQuestion);
                shortText.DisplayText = "Fire exit short answer";
                shortText.Alter<TitlePart>(part => part.Title = "Fire exit short answer");
                shortText.Alter<QuestionPart>(part =>
                {
                    part.Prompt = "<p>Which exit do you use?</p>";
                    part.Points = 1;
                });
                shortText.Alter<ShortTextQuestionPart>(part => part.AcceptedAnswers = [AcceptedAnswer]);
                shortText.Alter<ContainedPart>(part =>
                {
                    part.ListContentItemId = bank.ContentItemId;
                    part.ListContentType = bank.ContentType;
                });
                await contentManager.CreateAsync(shortText, VersionOptions.Published);

                var course = await contentManager.GetAsync(_fixture.CourseId, VersionOptions.Published);
                var quiz = await contentManager.NewAsync(AssessmentsConstants.ContentTypes.Quiz);
                quiz.DisplayText = "Exit quiz";
                quiz.Alter<TitlePart>(part => part.Title = "Exit quiz");
                quiz.Alter<QuizPart>(part => part.QuestionIds = [essay.ContentItemId, shortText.ContentItemId]);
                quiz.Alter<ContainedPart>(part =>
                {
                    part.ListContentItemId = course.ContentItemId;
                    part.ListContentType = course.ContentType;
                });
                quiz.Alter<CourseItemPart>(part => part.CompletionMode = CompletionModes.Submitted);
                await contentManager.CreateAsync(quiz, VersionOptions.Published);

                var draft = await contentManager.GetAsync(_fixture.CourseId, VersionOptions.DraftRequired);
                draft.Alter<CourseOutlinePart>(part => part.Sections[0].Items.Add(new CourseOutlineItem { ContentItemId = quiz.ContentItemId, Required = false }));
                await contentManager.UpdateAsync(draft);
                await contentManager.PublishAsync(draft);

                return new Setup(bank.ContentItemId, essay.ContentItemId, shortText.ContentItemId, quiz.ContentItemId);
            });

            _setup = (_fixture, value);

            return value;
        }
        finally
        {
            _setupLock.Release();
        }
    }

    private Task<T> AsLearnerAsync<T>(Func<IServiceProvider, System.Security.Claims.ClaimsPrincipal, string, Task<T>> work)
        => _fixture.InTenantAsync(async services =>
        {
            var userManager = services.GetRequiredService<UserManager<IUser>>();
            var user = await userManager.FindByNameAsync("learner1");
            var principal = await services.GetRequiredService<IUserClaimsPrincipalFactory<IUser>>().CreateAsync(user);

            return await work(services, principal, await userManager.GetUserIdAsync(user));
        });

    private sealed record Setup(string BankId, string EssayId, string ShortTextId, string QuizId);
}
