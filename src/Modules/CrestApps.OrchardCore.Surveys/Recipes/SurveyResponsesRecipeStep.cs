using CrestApps.Core;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Surveys.Recipes;

/// <summary>
/// Imports responses. A response names its survey by name and its version by number; an identified respondent by user
/// name, which must exist on the site. A response already on the site, by id, is replaced.
/// </summary>
internal sealed class SurveyResponsesRecipeStep : NamedRecipeStepHandler
{
    private readonly SurveyResponseStore _responseStore;
    private readonly SurveyStore _surveyStore;
    private readonly SurveyVersionStore _versionStore;
    private readonly ISurveyUserLookup _users;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponsesRecipeStep"/> class.
    /// </summary>
    /// <param name="responseStore">The response store.</param>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="versionStore">The version store.</param>
    /// <param name="users">The user lookup.</param>
    public SurveyResponsesRecipeStep(
        SurveyResponseStore responseStore,
        SurveyStore surveyStore,
        SurveyVersionStore versionStore,
        ISurveyUserLookup users)
        : base(SurveysRecipeSteps.Responses)
    {
        _responseStore = responseStore;
        _surveyStore = surveyStore;
        _versionStore = versionStore;
        _users = users;
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
            var response = existing ?? SurveysRecipeJson.Read<SurveyResponse>(token, "SurveyName", "UserName", "GradedByUserName");

            if (existing is not null)
            {
                SurveysRecipeJson.Populate(existing, token, "SurveyName", "UserName", "GradedByUserName", nameof(SurveyResponse.ResumeTokenHash));
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
                var userId = await _users.FindUserIdAsync(userName);

                if (userId is null)
                {
                    context.Errors.Add($"The response '{id}' was given by the user '{userName}', who is not on the site.");

                    continue;
                }

                response.UserId = userId;
                response.Respondent ??= new PartyRef(PartyKinds.User, userId);
            }

            response.GradedBy = await _users.FindUserIdAsync(SurveysRecipeJson.String(token, "GradedByUserName"));
            response.SurveyId = survey.ItemId;
            response.SurveyVersionId = version.ItemId;

            // An anonymous response never carries who gave it, whatever the file says.
            if (survey.Access?.Anonymity == AnonymityMode.Anonymous)
            {
                response.UserId = null;
                response.Respondent = null;
                response.IpAddress = null;
                response.InvitationId = null;
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
}
