using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Recipes;

/// <summary>
/// Writes Surveys records as recipe JSON: surveys with their versions and collectors, banks with their questions,
/// responses, accommodations and invitations. People are written by user name; secrets only when asked.
/// </summary>
internal sealed class SurveysRecipeExporter
{
    private readonly SurveyVersionStore _versionStore;
    private readonly SurveyCollectorStore _collectorStore;
    private readonly BankQuestionStore _bankQuestionStore;
    private readonly SurveyStore _surveyStore;
    private readonly ISurveyUserLookup _userLookup;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysRecipeExporter"/> class.
    /// </summary>
    /// <param name="versionStore">The version store.</param>
    /// <param name="collectorStore">The collector store.</param>
    /// <param name="bankQuestionStore">The bank question store.</param>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="userLookup">The user lookup, which turns user ids into user names and back.</param>
    public SurveysRecipeExporter(
        SurveyVersionStore versionStore,
        SurveyCollectorStore collectorStore,
        BankQuestionStore bankQuestionStore,
        SurveyStore surveyStore,
        ISurveyUserLookup userLookup)
    {
        _versionStore = versionStore;
        _collectorStore = collectorStore;
        _bankQuestionStore = bankQuestionStore;
        _surveyStore = surveyStore;
        _userLookup = userLookup;
    }

    /// <summary>
    /// Writes a survey with its versions and collectors. The anonymity secret is never written; collector tokens and
    /// password hashes only when <paramref name="includeSecrets"/> is set.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="includeSecrets">Whether collector tokens and password hashes are written.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The record.</returns>
    public async Task<JsonObject> ExportSurveyAsync(Survey survey, bool includeSecrets, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(survey);

        var node = SurveysRecipeJson.Write(survey, nameof(Survey.AnonymitySecret), nameof(Survey.PublishedVersionId));
        var versions = (await _versionStore.ListBySurveyAsync(survey.ItemId, cancellationToken)).OrderBy(version => version.Number).ToList();
        var names = await _userLookup.GetUserNamesAsync(versions.Select(version => version.PublishedBy));
        var versionArray = new JsonArray();

        foreach (var version in versions)
        {
            var item = SurveysRecipeJson.Write(version, nameof(SurveyVersion.SurveyId), nameof(SurveyVersion.PublishedBy));

            if (!string.IsNullOrEmpty(version.PublishedBy) && names.TryGetValue(version.PublishedBy, out var userName))
            {
                item["PublishedByUserName"] = userName;
            }

            versionArray.Add(item);
        }

        var collectorArray = new JsonArray();

        foreach (var collector in await _collectorStore.ListBySurveyAsync(survey.ItemId, cancellationToken))
        {
            collectorArray.Add(includeSecrets
                ? SurveysRecipeJson.Write(collector, nameof(SurveyCollector.SurveyId))
                : SurveysRecipeJson.Write(collector, nameof(SurveyCollector.SurveyId), nameof(SurveyCollector.Token), nameof(SurveyCollector.PasswordHash)));
        }

        node["Versions"] = versionArray;
        node["Collectors"] = collectorArray;

        return node;
    }

    /// <summary>
    /// Writes a question bank with its questions, retired ones included.
    /// </summary>
    /// <param name="bank">The bank.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The record.</returns>
    public async Task<JsonObject> ExportBankAsync(QuestionBank bank, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bank);

        var node = SurveysRecipeJson.Write(bank);
        var questions = new JsonArray();

        foreach (var question in await _bankQuestionStore.ListByBankAsync(bank.ItemId, includeRetired: true, cancellationToken))
        {
            questions.Add(SurveysRecipeJson.Write(question, nameof(BankQuestion.BankId)));
        }

        node["Questions"] = questions;

        return node;
    }

    /// <summary>
    /// Writes responses. Identified respondents and graders are written by user name; anonymous responses carry no
    /// identity.
    /// </summary>
    /// <param name="responses">The responses.</param>
    /// <returns>The records.</returns>
    public async Task<JsonArray> ExportResponsesAsync(IReadOnlyCollection<SurveyResponse> responses)
    {
        ArgumentNullException.ThrowIfNull(responses);

        var surveyNames = await SurveyNamesAsync(responses.Select(response => response.SurveyId));
        var names = await _userLookup.GetUserNamesAsync(responses.SelectMany(response => new[] { response.UserId, response.GradedBy }));
        var array = new JsonArray();

        foreach (var response in responses)
        {
            var node = SurveysRecipeJson.Write(
                response,
                nameof(SurveyResponse.UserId),
                nameof(SurveyResponse.GradedBy),
                nameof(SurveyResponse.SurveyId),
                nameof(SurveyResponse.SurveyVersionId),
                nameof(SurveyResponse.ResumeTokenHash),
                nameof(SurveyResponse.RespondentKey),
                nameof(SurveyResponse.UpkeepFailures));

            node["SurveyName"] = surveyNames.GetValueOrDefault(response.SurveyId);

            if (!string.IsNullOrEmpty(response.UserId) && names.TryGetValue(response.UserId, out var userName))
            {
                node["UserName"] = userName;

                // A user's party is rebuilt from the user on import, since the user's id differs on another site.
                if (response.Respondent is { Kind: PartyKinds.User })
                {
                    node.Remove(nameof(SurveyResponse.Respondent));
                }
            }

            if (!string.IsNullOrEmpty(response.GradedBy) && names.TryGetValue(response.GradedBy, out var graderName))
            {
                node["GradedByUserName"] = graderName;
            }

            array.Add(node);
        }

        return array;
    }

    /// <summary>
    /// Writes accommodations, people by user name.
    /// </summary>
    /// <param name="overrides">The accommodations.</param>
    /// <returns>The records.</returns>
    public async Task<JsonArray> ExportOverridesAsync(IReadOnlyCollection<SurveyOverride> overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);

        var surveyNames = await SurveyNamesAsync(overrides.Select(entry => entry.SurveyId));
        var names = await _userLookup.GetUserNamesAsync(overrides.SelectMany(entry => new[] { entry.UserId, entry.Actor }));
        var array = new JsonArray();

        foreach (var entry in overrides)
        {
            var node = SurveysRecipeJson.Write(entry, nameof(SurveyOverride.UserId), nameof(SurveyOverride.Actor), nameof(SurveyOverride.SurveyId));

            node["SurveyName"] = surveyNames.GetValueOrDefault(entry.SurveyId);

            if (!string.IsNullOrEmpty(entry.UserId) && names.TryGetValue(entry.UserId, out var userName))
            {
                node["UserName"] = userName;
            }

            if (!string.IsNullOrEmpty(entry.Actor) && names.TryGetValue(entry.Actor, out var actorName))
            {
                node["ActorUserName"] = actorName;
            }

            array.Add(node);
        }

        return array;
    }

    /// <summary>
    /// Writes invitations; tokens are kept only as their hashes.
    /// </summary>
    /// <param name="invitations">The invitations.</param>
    /// <returns>The records.</returns>
    public async Task<JsonArray> ExportInvitationsAsync(IReadOnlyCollection<SurveyInvitation> invitations)
    {
        ArgumentNullException.ThrowIfNull(invitations);

        var surveyNames = await SurveyNamesAsync(invitations.Select(entry => entry.SurveyId));
        var names = await _userLookup.GetUserNamesAsync(invitations.Select(entry => entry.UserId));
        var collectorIds = invitations.Select(entry => entry.CollectorId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();
        IReadOnlyCollection<SurveyCollector> collectors = collectorIds.Length == 0 ? [] : await _collectorStore.GetAsync(collectorIds);
        var collectorNames = collectors.ToDictionary(collector => collector.ItemId, collector => collector.Name, StringComparer.Ordinal);
        var array = new JsonArray();

        foreach (var invitation in invitations)
        {
            var node = SurveysRecipeJson.Write(invitation, nameof(SurveyInvitation.UserId), nameof(SurveyInvitation.SurveyId));

            node["SurveyName"] = surveyNames.GetValueOrDefault(invitation.SurveyId);

            // Collector ids differ between sites (the "Personal links" list gets a new id on each), so the list travels
            // by name too.
            node["CollectorName"] = collectorNames.GetValueOrDefault(invitation.CollectorId ?? string.Empty);

            if (!string.IsNullOrEmpty(invitation.UserId) && names.TryGetValue(invitation.UserId, out var userName))
            {
                node["UserName"] = userName;

                if (invitation.Recipient is { Kind: PartyKinds.User })
                {
                    node.Remove(nameof(SurveyInvitation.Recipient));
                }
            }

            array.Add(node);
        }

        return array;
    }

    private async Task<Dictionary<string, string>> SurveyNamesAsync(IEnumerable<string> surveyIds)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        // A chunk of surveys at a time, so an export across many surveys never passes SQL Server's 2,100 parameters.
        foreach (var chunk in SurveyIdChunks.Of(surveyIds))
        {
            foreach (var survey in await _surveyStore.GetAsync(chunk))
            {
                names[survey.ItemId] = survey.Name;
            }
        }

        return names;
    }
}
