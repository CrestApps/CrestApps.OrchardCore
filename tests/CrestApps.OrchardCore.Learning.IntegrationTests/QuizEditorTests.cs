using System.Net;
using System.Text.Json;
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
/// The quiz editor: the fixed questions are searched on the server instead of listing every question, and only people
/// who edit quizzes or see question banks may search. The "Draw at random" rules read each bank's tags and difficulties
/// to show what the bank can supply, a rule that asks for more than that is refused, and a quiz saved before the editor
/// changed still loads and draws.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class QuizEditorTests
{
    private readonly LearningSiteFixture _fixture;

    public QuizEditorTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TheQuestionSearch_FindsPublishedQuestionsByTitle_AndIsRefusedToLearners()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (_, questionIds) = await CreateBankAsync(suffix, ("Ladder safety " + suffix, 2, ["ladders"]), ("Unrelated " + suffix, 3, []));

        using var admin = await _fixture.CreateClientAsync("admin");
        using var learner = await _fixture.CreateClientAsync("learner1");

        // Act
        var found = await admin.GetAsync($"Admin/learning/questions/search?query=Ladder%20safety%20{suffix}", TestContext.Current.CancellationToken);
        var refused = await learner.GetAsync($"Admin/learning/questions/search?query={suffix}", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);

        using var json = JsonDocument.Parse(await found.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var item = Assert.Single(json.RootElement.EnumerateArray());

        Assert.Equal(questionIds[0], item.GetProperty("value").GetString());
        Assert.Equal("Ladder safety " + suffix, item.GetProperty("text").GetString());
        Assert.NotEqual(HttpStatusCode.OK, refused.StatusCode);
    }

    [Fact]
    public async Task TheBankSummary_GivesTheTagsAndDifficultyOfEachQuestion_AndIsRefusedToLearners()
    {
        // Arrange
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (bankId, _) = await CreateBankAsync(suffix, ("Ladder " + suffix, 2, ["Ladders", "heights"]), ("Fire " + suffix, 4, ["fire"]));

        using var admin = await _fixture.CreateClientAsync("admin");
        using var learner = await _fixture.CreateClientAsync("learner1");

        // Act
        var summary = await admin.GetAsync($"Admin/learning/question-banks/{bankId}/draw-summary", TestContext.Current.CancellationToken);
        var refused = await learner.GetAsync($"Admin/learning/question-banks/{bankId}/draw-summary", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, summary.StatusCode);

        using var json = JsonDocument.Parse(await summary.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var questions = json.RootElement.GetProperty("questions").EnumerateArray()
            .Select(question => (Difficulty: question.GetProperty("difficulty").GetInt32(), Tags: string.Join(",", question.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()))))
            .OrderBy(question => question.Difficulty)
            .ToArray();

        Assert.Equal([(2, "Ladders,heights"), (4, "fire")], questions);
        Assert.NotEqual(HttpStatusCode.OK, refused.StatusCode);
    }

    [Fact]
    public async Task Round11S4_AnInstructorWhoMayNotViewABank_FindsNoneOfItsQuestions_NorReadsItsTags()
    {
        // Arrange: a bank the instructor (Manage own courses, no View question banks) may not view.
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (bankId, _) = await CreateBankAsync(suffix, ("Ladder safety " + suffix, 2, ["ladders"]));

        using var admin = await _fixture.CreateClientAsync("admin");
        using var instructor = await _fixture.CreateClientAsync("instructor1");

        // Act
        var adminFound = await admin.GetStringAsync($"Admin/learning/questions/search?query={suffix}", TestContext.Current.CancellationToken);
        var instructorFound = await instructor.GetAsync($"Admin/learning/questions/search?query={suffix}", TestContext.Current.CancellationToken);
        var instructorSummary = await instructor.GetAsync($"Admin/learning/question-banks/{bankId}/draw-summary", TestContext.Current.CancellationToken);

        // Assert
        using var adminJson = JsonDocument.Parse(adminFound);
        Assert.Single(adminJson.RootElement.EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, instructorFound.StatusCode);
        using var instructorJson = JsonDocument.Parse(await instructorFound.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Empty(instructorJson.RootElement.EnumerateArray());
        Assert.NotEqual(HttpStatusCode.OK, instructorSummary.StatusCode);
    }

    [Fact]
    public async Task ADrawRule_AskingForMoreThanTheBankHas_IsRefused_WhileAQuizSavedBeforeStillLoadsSavesAndDraws()
    {
        // Arrange: a quiz saved with a rule in the stored format: a bank, a tag in another case, no difficulty.
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (bankId, questionIds) = await CreateBankAsync(suffix, ("Ladder one " + suffix, 2, ["Ladders"]), ("Ladder two " + suffix, 3, ["ladders"]), ("Fire " + suffix, 2, ["fire"]));
        var quizId = await CreateQuizAsync(suffix, new QuizDrawRule { BankId = bankId, Tag = "LADDERS", Count = 1 });

        using var admin = await _fixture.CreateClientAsync("admin");

        // Act: the editor loads the stored rule.
        var editor = await admin.GetStringAsync($"Admin/Contents/ContentItems/{quizId}/Edit", TestContext.Current.CancellationToken);

        // A rule asking for 3 questions with the tag, of which the bank has 2, is refused.
        var refused = await SaveQuizAsync(admin, editor, quizId, suffix, bankId, "ladders", null, 3);
        var refusedHtml = await refused.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Two, with any difficulty, are fine.
        var saved = await SaveQuizAsync(admin, editor, quizId, suffix, bankId, "ladders", null, 2);

        var drawn = await _fixture.InTenantAsync(async services =>
        {
            var userManager = services.GetRequiredService<UserManager<IUser>>();
            var user = await userManager.FindByNameAsync("learner1");
            var principal = await services.GetRequiredService<IUserClaimsPrincipalFactory<IUser>>().CreateAsync(user);
            var started = await services.GetRequiredService<IAssessmentAttemptService>().StartAsync(new StartAttemptRequest
            {
                UserId = await userManager.GetUserIdAsync(user),
                User = principal,
                QuizId = quizId,
                HonorConfirmed = true,
            });

            Assert.True(started.Succeeded, started.ErrorCode);

            return started.Attempt.Items.Select(item => item.QuestionId).ToArray();
        });

        // Assert
        Assert.Contains("data-draw-rules", editor);
        Assert.Contains($"value=\"{bankId}\" selected", editor);
        Assert.Contains("value=\"LADDERS\" selected", editor);

        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains("only 2 that match", refusedHtml);

        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        Assert.Equal(questionIds.Take(2).Order(), drawn.Order());
    }

    private static Task<HttpResponseMessage> SaveQuizAsync(HttpClient admin, string editor, string quizId, string suffix, string bankId, string tag, int? difficulty, int count)
        => admin.PostAsync($"Admin/Contents/ContentItems/{quizId}/Edit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["TitlePart.Title"] = "Draw quiz " + suffix,
            ["QuizPart.DrawRules[0].BankId"] = bankId,
            ["QuizPart.DrawRules[0].Tag"] = tag,
            ["QuizPart.DrawRules[0].Difficulty"] = difficulty?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            ["QuizPart.DrawRules[0].Count"] = count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["QuizPart.GradingMethod"] = "Highest",
            ["QuizPart.Navigation"] = "Free",
            ["QuizPart.ReviewPolicy"] = "AfterGraded",
            ["CourseItemPart.CompletionMode"] = CompletionModes.Submitted,
            ["submit.Publish"] = "submit.Publish",
            ["__RequestVerificationToken"] = LearningSiteFixture.Token(editor),
        }), TestContext.Current.CancellationToken);

    // A published quiz of this test only in the fixture course, which learner1 is enrolled in, drawing by the rule.
    private Task<string> CreateQuizAsync(string suffix, QuizDrawRule rule)
        => _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var course = await contentManager.GetAsync(_fixture.CourseId, VersionOptions.Published);
            var quiz = await contentManager.NewAsync(AssessmentsConstants.ContentTypes.Quiz);

            quiz.DisplayText = "Draw quiz " + suffix;
            quiz.Alter<TitlePart>(part => part.Title = "Draw quiz " + suffix);
            quiz.Alter<QuizPart>(part =>
            {
                part.DrawRules = [rule];
                part.MaxAttempts = null;
            });
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

            return quiz.ContentItemId;
        });


    // A published bank of this test only, with published short-text questions of the given titles, difficulties and tags.
    internal Task<(string BankId, string[] QuestionIds)> CreateBankAsync(string suffix, params (string Title, int Difficulty, string[] Tags)[] questions)
        => _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var bank = await contentManager.NewAsync(AssessmentsConstants.ContentTypes.QuestionBank);

            bank.DisplayText = "Bank " + suffix;
            bank.Alter<TitlePart>(part => part.Title = "Bank " + suffix);
            await contentManager.CreateAsync(bank, VersionOptions.Published);

            var ids = new List<string>();

            foreach (var (title, difficulty, tags) in questions)
            {
                var question = await contentManager.NewAsync(AssessmentsConstants.ContentTypes.ShortTextQuestion);

                question.DisplayText = title;
                question.Alter<TitlePart>(part => part.Title = title);
                question.Alter<QuestionPart>(part =>
                {
                    part.Prompt = "<p>" + title + "?</p>";
                    part.Points = 1;
                    part.Difficulty = difficulty;
                    part.Tags = [.. tags];
                });
                question.Alter<ShortTextQuestionPart>(part => part.AcceptedAnswers = ["yes"]);
                question.Alter<ContainedPart>(part =>
                {
                    part.ListContentItemId = bank.ContentItemId;
                    part.ListContentType = bank.ContentType;
                });
                await contentManager.CreateAsync(question, VersionOptions.Published);
                ids.Add(question.ContentItemId);
            }

            return (bank.ContentItemId, ids.ToArray());
        });
}
