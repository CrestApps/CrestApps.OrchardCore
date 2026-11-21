using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using OrchardCore.ContentManagement;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// Helpers for the course items whose questions live in a Surveys survey the item owns: quizzes and course surveys.
/// </summary>
internal static partial class LearningSurveyTestSupport
{
    /// <summary>
    /// Makes a one-page definition with the given questions and a default ending.
    /// </summary>
    /// <param name="title">The title respondents see.</param>
    /// <param name="questions">The questions.</param>
    /// <returns>The definition.</returns>
    public static SurveyDefinition Definition(string title, params SurveyQuestion[] questions)
        => new()
        {
            Title = title,
            Pages =
            [
                new SurveyPage
                {
                    Id = "p1",
                    Name = "page_1",
                    Title = "Page 1",
                    Questions = [.. questions],
                },
            ],
            Endings = [new SurveyEnding { Id = "e1", Name = "thanks", Title = "Thanks", Message = "Thank you.", IsDefault = true }],
        };

    /// <summary>
    /// Makes a single-choice question with choices given as id and text.
    /// </summary>
    /// <param name="id">The question id.</param>
    /// <param name="title">The question text.</param>
    /// <param name="choices">The choices.</param>
    /// <returns>The question.</returns>
    public static SurveyQuestion SingleChoice(string id, string title, params (string Id, string Text)[] choices)
        => new()
        {
            Id = id,
            Name = id,
            ControlType = SurveyControlNames.SingleChoice,
            Title = title,
            Required = true,
            Choices = [.. choices.Select((choice, index) => new SurveyChoice { Id = choice.Id, Value = (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), Text = choice.Text })],
        };

    /// <summary>
    /// Gets the id of the survey a quiz or a course survey item owns.
    /// </summary>
    /// <param name="item">The course item.</param>
    /// <returns>The survey id, or <see langword="null"/>.</returns>
    public static string SurveyIdOf(ContentItem item)
        => item.TryGet<QuizPart>(out var quiz) && !string.IsNullOrEmpty(quiz.SurveyId)
            ? quiz.SurveyId
            : item.TryGet<CourseSurveyPart>(out var survey) ? survey.SurveyId : null;

    /// <summary>
    /// Replaces the draft of the survey an item owns with a definition, and publishes it.
    /// </summary>
    /// <param name="services">The tenant services.</param>
    /// <param name="surveyId">The survey.</param>
    /// <param name="definition">The definition.</param>
    /// <param name="configure">Changes the survey's live settings before it is published, or <see langword="null"/>.</param>
    /// <returns>The published survey.</returns>
    public static async Task<Survey> PublishAsync(IServiceProvider services, string surveyId, SurveyDefinition definition, Action<Survey> configure = null)
    {
        var manager = services.GetRequiredService<ISurveyManager>();
        var survey = await manager.FindByIdAsync(surveyId);

        Assert.NotNull(survey);
        Assert.Equal(SurveyErrorCode.None, await manager.SaveDraftAsync(survey, definition));

        if (configure is not null)
        {
            survey = await manager.FindByIdAsync(surveyId);
            configure(survey);
            await manager.UpdateAsync(survey);
        }

        var published = await services.GetRequiredService<ISurveyPublisher>().PublishAsync(surveyId, "Test version", "admin");

        Assert.True(published.Succeeded, string.Join(", ", published.Messages.Where(message => message.IsError).Select(message => message.Message)));

        return await manager.FindByIdAsync(surveyId);
    }

    /// <summary>
    /// Takes the survey of a course item as a learner, in the service layer: starts a response for the learner's
    /// enrollment and finishes its one page with the given form values.
    /// </summary>
    /// <param name="services">The tenant services.</param>
    /// <param name="userName">The learner.</param>
    /// <param name="item">The course item.</param>
    /// <param name="values">The form values, by input name (<c>q.{questionId}</c>).</param>
    /// <returns>The response.</returns>
    public static async Task<SurveyResponse> AnswerAsync(IServiceProvider services, string userName, ContentItem item, IDictionary<string, string> values)
    {
        var userManager = services.GetRequiredService<UserManager<IUser>>();
        var user = await userManager.FindByNameAsync(userName);
        var principal = await services.GetRequiredService<IUserClaimsPrincipalFactory<IUser>>().CreateAsync(user);
        var accessor = services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext.User = principal;

        var started = await services.GetRequiredService<ILearningSurveyService>().StartAsync(principal, item, SurveyIdOf(item), null);

        Assert.True(started.Succeeded, started.Error.ToString());

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var form = new FormCollection(values.ToDictionary(pair => pair.Key, pair => new StringValues(pair.Value)));
        var result = await services.GetRequiredService<ISurveyResponseService>().SubmitPageAsync(new SubmitPageRequest
        {
            ResponseId = started.Response.ItemId,
            PageId = started.Response.CurrentPageId,
            Direction = SurveyPageDirection.Finish,
            Form = form,
            Identity = new SurveyRespondentIdentity { UserId = userId, ResumeToken = started.ResumeToken },
        });

        Assert.True(result.Succeeded, result.Error + " " + string.Join(", ", result.Errors.SelectMany(error => error.Value)));
        Assert.True(result.Ended, string.Join(", ", result.Errors.SelectMany(error => error.Value)));

        return result.Response;
    }

    /// <summary>
    /// Takes the survey of a course item as a learner over HTTP: posts the start form of the item, opens the response on
    /// the Surveys runner and finishes its one page with the given form values.
    /// </summary>
    /// <param name="client">The learner's signed-in client.</param>
    /// <param name="startPath">The start path of the item, such as <c>learning/quizzes/{id}/start</c>.</param>
    /// <param name="playerPage">A page of the course player that holds an antiforgery token.</param>
    /// <param name="values">The form values, by input name (<c>q.{questionId}</c>).</param>
    /// <returns>The path of the ending page.</returns>
    public static async Task<string> AnswerOverHttpAsync(HttpClient client, string startPath, string playerPage, IDictionary<string, string> values)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var started = await client.PostAsync(startPath, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = LearningSiteFixture.Token(playerPage),
        }), cancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, started.StatusCode);

        var responsePath = started.Headers.Location?.OriginalString;

        Assert.Contains("/s/r/", responsePath, StringComparison.Ordinal);

        var page = await client.GetStringAsync(responsePath, cancellationToken);
        var form = new MultipartFormDataContent
        {
            { new StringContent(LearningSiteFixture.Token(page)), "__RequestVerificationToken" },
            { new StringContent(PageId(page)), "pageId" },
            { new StringContent("finish"), "direction" },
        };

        foreach (var (name, value) in values)
        {
            form.Add(new StringContent(value), name);
        }

        var path = responsePath.Split('?')[0];
        var submitted = await client.PostAsync(path, form, cancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, submitted.StatusCode);

        var done = submitted.Headers.Location?.OriginalString;

        Assert.EndsWith("/done", done, StringComparison.Ordinal);

        return done;
    }

    /// <summary>
    /// Reads the page id of a runner page.
    /// </summary>
    /// <param name="html">The page.</param>
    /// <returns>The page id.</returns>
    public static string PageId(string html)
        => PageIdPattern().Match(html) is { Success: true } match ? match.Groups["id"].Value : throw new InvalidOperationException("The page has no page id.");

    [GeneratedRegex("name=\"pageId\" value=\"(?<id>[^\"]+)\"")]
    private static partial Regex PageIdPattern();
}
