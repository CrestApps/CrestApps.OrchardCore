using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Surveys.IntegrationTests;

/// <summary>
/// R8-M5: the admin responses list's Collector filter leaves out the responses an anonymous survey shows as "Other", so
/// it can't tell their real collector.
/// </summary>
public sealed partial class SurveyRespondingTests
{
    [Fact]
    public async Task ResponsesList_OfAnAnonymousSurvey_ACollectorFilterLeavesOutItsMaskedResponses()
    {
        // Arrange: five responses on collector A, Bob's on collector A with a context kind no one else has, and four on
        // links sent to one person each: the small groups hold five together, so Bob's response is masked, not hidden.
        // Ten responses, one page of the list.
        var (surveyId, _, _) = await PublishAsync("Anonymous list collector filter");
        var collectorA = "a" + Guid.NewGuid().ToString("N")[..8];
        var shown = new List<string>();
        var masked = new List<string>();
        string bob = null;

        await _fixture.InTenantAsync(async services =>
        {
            var surveys = services.GetRequiredService<SurveyStore>();
            var survey = await surveys.FindByIdAsync(surveyId);

            survey.Access ??= new SurveyAccessSettings();
            survey.Access.Anonymity = AnonymityMode.Anonymous;
            await surveys.UpdateAsync(survey);

            for (var index = 0; index < 5; index++)
            {
                shown.Add(await CreateCollectedResponseAsync(services, survey, collectorA, null));
            }

            bob = await CreateCollectedResponseAsync(services, survey, collectorA, new RecordRef("Ticket", "t1"));

            for (var index = 0; index < 4; index++)
            {
                masked.Add(await CreateCollectedResponseAsync(services, survey, "link" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + collectorA, null));
            }
        });

        using var admin = await _fixture.CreateClientAsync("admin");
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var all = await admin.GetStringAsync($"Admin/surveys/{surveyId}/responses", cancellationToken);
        var filtered = await admin.GetStringAsync($"Admin/surveys/{surveyId}/responses?Collector={collectorA}", cancellationToken);

        // Assert: unfiltered, every response is listed (the masked ones as "Other"); filtered to collector A, Bob's
        // masked response is left out, so the filter doesn't tell it came through collector A.
        Assert.All(shown.Append(bob).Concat(masked), id => Assert.Contains(id, all, StringComparison.Ordinal));
        Assert.All(shown, id => Assert.Contains(id, filtered, StringComparison.Ordinal));
        Assert.DoesNotContain(bob, filtered, StringComparison.Ordinal);
    }

    private static async Task<string> CreateCollectedResponseAsync(IServiceProvider services, Survey survey, string collectorId, RecordRef? context)
    {
        var now = DateTime.UtcNow;
        var response = new SurveyResponse
        {
            SurveyId = survey.ItemId,
            SurveyVersionId = survey.PublishedVersionId,
            VersionNumber = survey.PublishedVersionNumber,
            CollectorId = collectorId,
            Context = context,
            Status = SurveyResponseStatus.Completed,
            StartedUtc = now,
            LastActivityUtc = now,
            CompletedUtc = now,
        };

        await services.GetRequiredService<SurveyResponseStore>().CreateAsync(response);

        return response.ItemId;
    }
}
