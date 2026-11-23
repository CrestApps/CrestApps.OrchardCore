using System.Net;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Surveys.IntegrationTests;

/// <summary>
/// Grading: nobody grades, rescores or regrades their own response (P4-M1), and an anonymous survey's responses wait
/// in the grading queue until its completed responses reach the minimum group size (P2-M3).
/// </summary>
public sealed partial class SurveyRespondingTests
{
    [Fact]
    public async Task Grading_TheGradersOwnResponse_IsLeftOutOfTheQueueAndRefusedOnEveryPath()
    {
        // Arrange: two responses waiting for a grader, one given by the admin who grades.
        var (surveyId, _, _) = await PublishAsync("Own work check");
        var (ownId, otherId) = await _fixture.InTenantAsync(async services =>
        {
            var users = services.GetRequiredService<UserManager<IUser>>();
            var adminId = await users.GetUserIdAsync(await users.FindByNameAsync("admin"));
            var survey = await services.GetRequiredService<SurveyStore>().FindByIdAsync(surveyId);

            return (await CreatePendingAsync(services, survey, adminId), await CreatePendingAsync(services, survey, null));
        });

        using var admin = await _fixture.CreateClientAsync("admin");
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var queue = await admin.GetStringAsync($"Admin/surveys/grading?surveyId={surveyId}", cancellationToken);
        var own = await admin.GetAsync($"Admin/surveys/grading/{ownId}", cancellationToken);
        var other = await admin.GetAsync($"Admin/surveys/grading/{otherId}", cancellationToken);
        var otherPage = await other.Content.ReadAsStringAsync(cancellationToken);
        var changed = await admin.PostAsync($"Admin/surveys/grading/{ownId}/score", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = SurveysSiteFixture.Token(otherPage),
            ["NewScore"] = "100",
            ["Reason"] = "Mine",
        }), cancellationToken);

        // Assert
        Assert.Contains(otherId, queue, StringComparison.Ordinal);
        Assert.DoesNotContain(ownId, queue, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, own.StatusCode);
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);

        var stored = await _fixture.InTenantAsync(services => services.GetRequiredService<SurveyResponseStore>().FindByIdAsync(ownId).AsTask());

        Assert.Null(stored.Score);
    }

    [Fact]
    public async Task Grading_AnAnonymousSurveyBelowItsMinimum_ListsAndOpensNothing()
    {
        // Arrange: an anonymous survey with one completed response waiting for a grader.
        var (surveyId, _, _) = await PublishAsync("Anonymous grading check");
        var responseId = await _fixture.InTenantAsync(async services =>
        {
            var surveys = services.GetRequiredService<SurveyStore>();
            var survey = await surveys.FindByIdAsync(surveyId);

            survey.Access ??= new SurveyAccessSettings();
            survey.Access.Anonymity = AnonymityMode.Anonymous;
            await surveys.UpdateAsync(survey);

            return await CreatePendingAsync(services, survey, null);
        });

        using var admin = await _fixture.CreateClientAsync("admin");
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var queue = await admin.GetStringAsync($"Admin/surveys/grading?surveyId={surveyId}", cancellationToken);
        var grade = await admin.GetAsync($"Admin/surveys/grading/{responseId}", cancellationToken);

        // Assert
        Assert.DoesNotContain(responseId, queue, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, grade.StatusCode);
    }

    private static async Task<string> CreatePendingAsync(IServiceProvider services, Survey survey, string userId)
    {
        var now = DateTime.UtcNow;
        var response = new SurveyResponse
        {
            SurveyId = survey.ItemId,
            SurveyVersionId = survey.PublishedVersionId,
            VersionNumber = survey.PublishedVersionNumber,
            Number = "G" + Guid.NewGuid().ToString("N")[..8],
            UserId = userId,
            Status = SurveyResponseStatus.Completed,
            Grading = GradingStatus.Pending,
            StartedUtc = now,
            LastActivityUtc = now,
            CompletedUtc = now,
        };

        await services.GetRequiredService<SurveyResponseStore>().CreateAsync(response);

        return response.ItemId;
    }
}
