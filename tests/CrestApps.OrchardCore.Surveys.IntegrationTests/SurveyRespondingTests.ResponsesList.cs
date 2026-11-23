using System.Net;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Surveys.IntegrationTests;

/// <summary>
/// The admin responses list: an anonymous survey never lists a status too few responses share (R4-L7), and responses
/// upkeep stopped trying are shown and can be tried again (R4-M2).
/// </summary>
public sealed partial class SurveyRespondingTests
{
    [Fact]
    public async Task ResponsesList_OfAnAnonymousSurvey_LeavesOutAStatusTooFewResponsesShare()
    {
        // Arrange: an anonymous survey with five completed responses (the site's floor) and one abandoned response.
        var (surveyId, _, _) = await PublishAsync("Anonymous list check");
        var tag = Guid.NewGuid().ToString("N")[..8];

        await _fixture.InTenantAsync(async services =>
        {
            var surveys = services.GetRequiredService<SurveyStore>();
            var survey = await surveys.FindByIdAsync(surveyId);

            survey.Access ??= new SurveyAccessSettings();
            survey.Access.Anonymity = AnonymityMode.Anonymous;
            await surveys.UpdateAsync(survey);

            for (var index = 0; index < SurveySettings.DefaultMinimumGroupSizeFloor; index++)
            {
                await CreateResponseAsync(services, survey, $"C{tag}{index}", SurveyResponseStatus.Completed);
            }

            await CreateResponseAsync(services, survey, $"A{tag}", SurveyResponseStatus.Abandoned);
        });

        using var admin = await _fixture.CreateClientAsync("admin");
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var page = await admin.GetStringAsync($"Admin/surveys/{surveyId}/responses", cancellationToken);

        // Assert
        Assert.Contains($"C{tag}0", page, StringComparison.Ordinal);
        Assert.DoesNotContain($"A{tag}", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResponsesList_ShowsResponsesUpkeepStoppedTrying_AndTryAgainClearsTheirCount()
    {
        // Arrange: a response upkeep failed on too many times in a row.
        var (surveyId, _, _) = await PublishAsync("Upkeep stopped check");
        var tag = Guid.NewGuid().ToString("N")[..8];
        var stuckId = await _fixture.InTenantAsync(async services =>
        {
            var survey = await services.GetRequiredService<SurveyStore>().FindByIdAsync(surveyId);

            await CreateResponseAsync(services, survey, $"K{tag}", SurveyResponseStatus.Completed);

            return await CreateResponseAsync(services, survey, $"S{tag}", SurveyResponseStatus.InProgress, SurveyMaintenanceService.MaxUpkeepFailures);
        });

        using var admin = await _fixture.CreateClientAsync("admin");
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var list = await admin.GetStringAsync($"Admin/surveys/{surveyId}/responses", cancellationToken);
        var filtered = await admin.GetStringAsync($"Admin/surveys/{surveyId}/responses?UpkeepStopped=true", cancellationToken);
        var retried = await admin.PostAsync($"Admin/surveys/{surveyId}/responses/retry-upkeep", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = SurveysSiteFixture.Token(list),
        }), cancellationToken);

        // Assert
        Assert.Contains("stopped trying it", list, StringComparison.Ordinal);
        Assert.Contains("Upkeep stopped", list, StringComparison.Ordinal);
        Assert.Contains($"S{tag}", filtered, StringComparison.Ordinal);
        Assert.DoesNotContain($"K{tag}", filtered, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, retried.StatusCode);

        var stored = await _fixture.InTenantAsync(services => services.GetRequiredService<SurveyResponseStore>().FindByIdAsync(stuckId).AsTask());

        Assert.Equal(0, stored.UpkeepFailures);
    }

    private static async Task<string> CreateResponseAsync(IServiceProvider services, Survey survey, string number, SurveyResponseStatus status, int upkeepFailures = 0)
    {
        var now = DateTime.UtcNow;
        var response = new SurveyResponse
        {
            SurveyId = survey.ItemId,
            SurveyVersionId = survey.PublishedVersionId,
            VersionNumber = survey.PublishedVersionNumber,
            Number = number,
            Status = status,
            StartedUtc = now,
            LastActivityUtc = now,
            CompletedUtc = status == SurveyResponseStatus.Completed ? now : null,
            UpkeepFailures = upkeepFailures,
        };

        await services.GetRequiredService<SurveyResponseStore>().CreateAsync(response);

        return response.ItemId;
    }
}
