using System.Net;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Lists.Models;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// Assessments on a running site: a quiz owns the survey its attempts answer, and the grade scale form saves what it
/// posts (it once posted to a path that a page action without an HTTP method also matched, so nothing was saved).
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class AssessmentAdminFormsTests
{
    private readonly LearningSiteFixture _fixture;

    public AssessmentAdminFormsTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ANewQuiz_CreatesTheQuizSurveyItOwns_AndRemovingTheQuizDeletesIt()
    {
        // Arrange and act: a quiz lesson saved in a course gets a survey of kind Quiz owned by the content item.
        var (quizId, surveyId, survey) = await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var course = await contentManager.GetAsync(_fixture.OtherCourseId, VersionOptions.Published);
            var quiz = await contentManager.NewAsync(AssessmentsConstants.ContentTypes.Quiz);

            quiz.DisplayText = "Owned survey quiz";
            quiz.Alter<TitlePart>(part => part.Title = "Owned survey quiz");
            quiz.Alter<ContainedPart>(part =>
            {
                part.ListContentItemId = course.ContentItemId;
                part.ListContentType = course.ContentType;
            });
            quiz.Alter<CourseItemPart>(part => part.CompletionMode = CompletionModes.Submitted);
            await contentManager.CreateAsync(quiz, VersionOptions.Published);

            var surveyId = quiz.Get<QuizPart>(nameof(QuizPart))?.SurveyId;
            var survey = (await services.GetRequiredService<ISurveyManager>().ListByOwnerAsync(new RecordRef(RecordKinds.ContentItem, quiz.ContentItemId))).SingleOrDefault();

            return (quiz.ContentItemId, surveyId, survey);
        });

        await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var quiz = await contentManager.GetAsync(quizId, VersionOptions.Latest);
            await contentManager.RemoveAsync(quiz);
        });

        // The survey is removed after the removing request commits (a deferred task), so it is read in a scope of its own.
        var afterRemoving = await _fixture.InTenantAsync(services =>
            services.GetRequiredService<ISurveyManager>().ListByOwnerAsync(new RecordRef(RecordKinds.ContentItem, quizId)));

        // Assert
        Assert.False(string.IsNullOrEmpty(surveyId));
        Assert.NotNull(survey);
        Assert.Equal(surveyId, survey.ItemId);
        Assert.Equal(SurveyKinds.Quiz, survey.Kind);
        Assert.Equal(new RecordRef(RecordKinds.ContentItem, quizId), survey.Owner);
        Assert.Empty(afterRemoving);
    }

    [Fact]
    public async Task SaveANewGradeScale_CreatesTheScale()
    {
        // Arrange
        var name = "Letters " + Guid.NewGuid().ToString("N");
        using var admin = await _fixture.CreateClientAsync("admin");
        var form = await admin.GetAsync("Admin/learning/grade-scales/new", TestContext.Current.CancellationToken);
        var token = LearningSiteFixture.Token(await form.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        // Act
        var response = await admin.PostAsync("Admin/learning/grade-scales/save", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = string.Empty,
            ["Name"] = name,
            ["Bands[0].MinPercent"] = "50",
            ["Bands[0].Label"] = "Pass",
            ["Bands[0].Passing"] = "true",
            ["Bands[1].MinPercent"] = "0",
            ["Bands[1].Label"] = "Fail",
            ["__RequestVerificationToken"] = token,
        }), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var scale = await _fixture.InTenantAsync(async services => await services.GetRequiredService<GradeScaleStore>().FindByNameAsync(name));
        Assert.NotNull(scale);
        Assert.Equal(["Pass", "Fail"], scale.Bands.Select(band => band.Label));

        var editor = await admin.GetAsync($"Admin/learning/grade-scales/{scale.ItemId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, editor.StatusCode);
        Assert.Contains(name, await editor.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
