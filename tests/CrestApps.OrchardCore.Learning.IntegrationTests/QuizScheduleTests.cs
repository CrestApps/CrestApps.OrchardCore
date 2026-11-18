using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
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
/// A quiz's dates as the learner meets them: the player gives the date of a deadline days away.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed partial class QuizScheduleTests
{
    private readonly LearningSiteFixture _fixture;

    public QuizScheduleTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ThePlayer_ShowsTheDateOfADeadlineDaysAway()
    {
        // Arrange: no time limit, so the deadline is the close date a week away.
        var closes = DateTime.UtcNow.AddDays(7);
        var quizId = await CreateQuizAsync("Weekly safety check", part => part.ClosesUtc = closes);
        var attemptId = await AsLearnerAsync(async (services, user, userId) =>
        {
            var started = await services.GetRequiredService<IAssessmentAttemptService>().StartAsync(new StartAttemptRequest { UserId = userId, User = user, QuizId = quizId, HonorConfirmed = true });

            Assert.True(started.Succeeded, started.ErrorCode);

            return started.Attempt.ItemId;
        });
        using var learner = await _fixture.CreateClientAsync("learner1");

        // Act
        var response = await learner.GetAsync($"learning/attempts/{attemptId}", TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var due = DueLine().Match(html);
        Assert.True(due.Success, "The player shows no Due line.");
        Assert.Contains(closes.Year.ToString(CultureInfo.InvariantCulture), due.Groups[1].Value);
    }

    private Task<string> CreateQuizAsync(string title, Action<QuizPart> configure)
        => _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();

            var bank = await contentManager.NewAsync(AssessmentsConstants.ContentTypes.QuestionBank);
            bank.DisplayText = title + " bank";
            bank.Alter<TitlePart>(part => part.Title = title + " bank");
            await contentManager.CreateAsync(bank, VersionOptions.Published);

            var question = await contentManager.NewAsync(AssessmentsConstants.ContentTypes.ShortTextQuestion);
            question.DisplayText = title + " question";
            question.Alter<TitlePart>(part => part.Title = title + " question");
            question.Alter<QuestionPart>(part =>
            {
                part.Prompt = "<p>Which stairs do you take?</p>";
                part.Points = 1;
            });
            question.Alter<ShortTextQuestionPart>(part => part.AcceptedAnswers = ["North stairs"]);
            question.Alter<ContainedPart>(part =>
            {
                part.ListContentItemId = bank.ContentItemId;
                part.ListContentType = bank.ContentType;
            });
            await contentManager.CreateAsync(question, VersionOptions.Published);

            var course = await contentManager.GetAsync(_fixture.CourseId, VersionOptions.Published);
            var quiz = await contentManager.NewAsync(AssessmentsConstants.ContentTypes.Quiz);
            quiz.DisplayText = title;
            quiz.Alter<TitlePart>(part => part.Title = title);
            quiz.Alter<QuizPart>(part =>
            {
                part.QuestionIds = [question.ContentItemId];
                part.MaxAttempts = null;
                configure(part);
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

    private Task<T> AsLearnerAsync<T>(Func<IServiceProvider, System.Security.Claims.ClaimsPrincipal, string, Task<T>> work)
        => _fixture.InTenantAsync(async services =>
        {
            var userManager = services.GetRequiredService<UserManager<IUser>>();
            var user = await userManager.FindByNameAsync("learner1");
            var principal = await services.GetRequiredService<IUserClaimsPrincipalFactory<IUser>>().CreateAsync(user);

            return await work(services, principal, await userManager.GetUserIdAsync(user));
        });

    [GeneratedRegex(@">\s*Due\s*(.*?)</span>", RegexOptions.Singleline)]
    private static partial Regex DueLine();
}
