using System.Globalization;
using CrestApps.Core;
using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Surveys.Recipes;

/// <summary>
/// Imports responses. A response names its survey by name and its version by number; an identified respondent by user
/// name, which must exist on the site. A response already on the site, by id, is replaced. Its collector is matched by
/// name, then by id, then falls back to the survey's default collector, since collector ids differ between sites; its
/// invitation is kept only when an invitation of the same survey with that id is on the site (an imported invitation
/// keeps its id), and cleared otherwise.
/// </summary>
internal sealed class SurveyResponsesRecipeStep : NamedRecipeStepHandler
{
    private readonly SurveyResponseStore _responseStore;
    private readonly SurveyStore _surveyStore;
    private readonly SurveyVersionStore _versionStore;
    private readonly SurveyCollectorStore _collectorStore;
    private readonly SurveyInvitationStore _invitationStore;
    private readonly INumberSequence _numberSequence;
    private readonly ISurveyUserLookup _userLookup;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponsesRecipeStep"/> class.
    /// </summary>
    /// <param name="responseStore">The response store.</param>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="versionStore">The version store.</param>
    /// <param name="collectorStore">The collector store.</param>
    /// <param name="invitationStore">The invitation store.</param>
    /// <param name="numberSequences">The number sequence, to number a response the file gives no number (an anonymous one).</param>
    /// <param name="userLookup">The user lookup, which turns user ids into user names and back.</param>
    public SurveyResponsesRecipeStep(
        SurveyResponseStore responseStore,
        SurveyStore surveyStore,
        SurveyVersionStore versionStore,
        SurveyCollectorStore collectorStore,
        SurveyInvitationStore invitationStore,
        IEnumerable<INumberSequence> numberSequences,
        ISurveyUserLookup userLookup)
        : base(SurveysRecipeSteps.Responses)
    {
        _responseStore = responseStore;
        _surveyStore = surveyStore;
        _versionStore = versionStore;
        _collectorStore = collectorStore;
        _invitationStore = invitationStore;
        _numberSequence = numberSequences.FirstOrDefault();
        _userLookup = userLookup;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var surveys = new Dictionary<string, Survey>(StringComparer.OrdinalIgnoreCase);

        foreach (var token in SurveysRecipeJson.Array(context.Step, SurveysRecipeSteps.ResponsesSection))
        {
            var id = SurveysRecipeJson.String(token, nameof(SurveyResponse.ItemId));
            var surveyName = SurveysRecipeJson.String(token, "SurveyName");

            if (string.IsNullOrEmpty(surveyName))
            {
                context.Errors.Add($"The response '{id}' names no survey and was skipped.");

                continue;
            }

            if (!surveys.TryGetValue(surveyName, out var survey))
            {
                survey = await _surveyStore.FindByNameAsync(surveyName);
                surveys[surveyName] = survey;
            }

            if (survey is null)
            {
                context.Errors.Add($"The response '{id}' belongs to the survey '{surveyName}', which is not on the site.");

                continue;
            }

            var existing = string.IsNullOrEmpty(id) ? null : await _responseStore.FindByIdAsync(id);
            var response = existing ?? SurveysRecipeJson.Read<SurveyResponse>(token, "SurveyName", "UserName", "GradedByUserName", SurveysRecipeJson.CollectorName, nameof(SurveyResponse.RespondentKey), nameof(SurveyResponse.Accommodation), nameof(SurveyResponse.UpkeepFailures));

            if (existing is not null)
            {
                SurveysRecipeJson.Populate(existing, token, "SurveyName", "UserName", "GradedByUserName", SurveysRecipeJson.CollectorName, nameof(SurveyResponse.ResumeTokenHash), nameof(SurveyResponse.RespondentKey), nameof(SurveyResponse.Accommodation), nameof(SurveyResponse.UpkeepFailures));
            }

            var version = await _versionStore.FindByNumberAsync(survey.ItemId, response.VersionNumber);

            if (version is null)
            {
                context.Errors.Add($"The response '{id}' is on version {response.VersionNumber} of the survey '{surveyName}', which the site does not have.");

                continue;
            }

            var userName = SurveysRecipeJson.String(token, "UserName");

            if (userName is not null)
            {
                var userId = await _userLookup.FindUserIdAsync(userName);

                if (userId is null)
                {
                    context.Errors.Add($"The response '{id}' was given by the user '{userName}', who is not on the site.");

                    continue;
                }

                response.UserId = userId;
                response.Respondent ??= new PartyRef(PartyKinds.User, userId);
            }
            else if (!string.IsNullOrEmpty(response.UserId))
            {
                // A raw user id from the file is kept only when it names a user of this site; ids from another site
                // would point the response at someone else, or at nobody.
                var names = await _userLookup.GetUserNamesAsync([response.UserId]);

                if (names is null || !names.ContainsKey(response.UserId))
                {
                    context.Errors.Add($"The response '{id}' names a user id that is not on the site and no UserName; it was imported without a user.");

                    if (response.Respondent is { } respondent && respondent.Kind == PartyKinds.User && respondent.Id == response.UserId)
                    {
                        response.Respondent = null;
                    }

                    response.UserId = null;
                }
            }

            response.GradedBy = await _userLookup.FindUserIdAsync(SurveysRecipeJson.String(token, "GradedByUserName"));
            response.SurveyId = survey.ItemId;
            response.SurveyVersionId = version.ItemId;
            response.CollectorId = await ResolveCollectorAsync(survey, response.CollectorId, SurveysRecipeJson.String(token, SurveysRecipeJson.CollectorName));

            if (!string.IsNullOrEmpty(response.InvitationId)
                && (await _invitationStore.FindByIdAsync(response.InvitationId) is not { } invitation || invitation.SurveyId != survey.ItemId))
            {
                response.InvitationId = null;
                response.InvitationSeal = null;
            }

            // An anonymous response never carries who gave it, whatever the file says.
            if (survey.Access?.Anonymity == AnonymityMode.Anonymous)
            {
                response.UserId = null;
                response.Respondent = null;
                response.IpAddress = null;
                response.InvitationId = null;
            }

            // An anonymous response has no number: the site's sequence runs in the order responses start, so a number next
            // to the numbers of identified responses could name its respondent. One in the file is never kept.
            if (survey.Access?.Anonymity == AnonymityMode.Anonymous)
            {
                response.Number = null;
            }
            else if (existing is null && string.IsNullOrEmpty(response.Number))
            {
                response.Number = await NextNumberAsync();
            }

            if (existing is null)
            {
                response.ItemId = !string.IsNullOrEmpty(id) && UniqueId.IsValid(id) ? id : IdGenerator.GenerateId();
                response.ResumeTokenHash = null;
                await _responseStore.CreateAsync(response);
            }
            else
            {
                await _responseStore.UpdateAsync(response);
            }
        }
    }

    // The same numbers the response service gives (R and seven digits), or an opaque one when no sequence is registered.
    private async Task<string> NextNumberAsync()
        => _numberSequence is null
            ? "R" + IdGenerator.GenerateId()[..10].ToUpperInvariant()
            : "R" + (await _numberSequence.NextAsync(SurveyResponseService.NumberSeries)).ToString("D7", CultureInfo.InvariantCulture);

    // A response's collector on this site: the survey's collector of that name, then the one with that id, then the
    // survey's default collector. A response with no collector (a preview) keeps none.
    private async Task<string> ResolveCollectorAsync(Survey survey, string collectorId, string collectorName)
    {
        if (string.IsNullOrEmpty(collectorId) && string.IsNullOrEmpty(collectorName))
        {
            return null;
        }

        var collectors = await _collectorStore.ListBySurveyAsync(survey.ItemId);

        var match = (string.IsNullOrEmpty(collectorName) ? null : collectors.FirstOrDefault(collector => string.Equals(collector.Name, collectorName, StringComparison.OrdinalIgnoreCase)))
            ?? (string.IsNullOrEmpty(collectorId) ? null : collectors.FirstOrDefault(collector => collector.ItemId == collectorId))
            ?? await _collectorStore.FindDefaultAsync(survey.ItemId)
            ?? (collectors.Count > 0 ? collectors[0] : null);

        return match?.ItemId;
    }
}
