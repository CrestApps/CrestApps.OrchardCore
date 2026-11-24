using System.Net;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Lists.Models;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// The admin forms of Assessments save what they post: a quiz override and a new grade scale. Both posted to a path
/// that a page action without an HTTP method also matched, so the page answered and nothing was saved.
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
    public async Task GiveAnOverride_SavesTheOverride()
    {
        // Arrange
        var quizId = await _fixture.InTenantAsync(async services =>
        {
            var contentManager = services.GetRequiredService<IContentManager>();
            var course = await contentManager.GetAsync(_fixture.OtherCourseId, VersionOptions.Published);
            var quiz = await contentManager.NewAsync(AssessmentsConstants.ContentTypes.Quiz);

            quiz.DisplayText = "Override form quiz";
            quiz.Alter<TitlePart>(part => part.Title = "Override form quiz");
            quiz.Alter<ContainedPart>(part =>
            {
                part.ListContentItemId = course.ContentItemId;
                part.ListContentType = course.ContentType;
            });
            quiz.Alter<CourseItemPart>(part => part.CompletionMode = CompletionModes.Submitted);
            await contentManager.CreateAsync(quiz, VersionOptions.Published);

            return quiz.ContentItemId;
        });
        using var admin = await _fixture.CreateClientAsync("admin");
        var form = await admin.GetAsync($"Admin/learning/quizzes/{quizId}/overrides", TestContext.Current.CancellationToken);
        var token = LearningSiteFixture.Token(await form.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        // Act
        var response = await admin.PostAsync($"Admin/learning/quizzes/{quizId}/overrides", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["UserName"] = "learner1",
            ["ExtraMinutes"] = "0",
            ["ExtraAttempts"] = "1",
            ["Reason"] = "Missed the first sitting",
            ["__RequestVerificationToken"] = token,
        }), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var overrides = await _fixture.InTenantAsync(async services => await services.GetRequiredService<AssessmentOverrideStore>().ListByQuizAsync(quizId));
        var entry = Assert.Single(overrides);
        Assert.Equal(1, entry.ExtraAttempts);
        Assert.Equal("Missed the first sitting", entry.Reason);
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
