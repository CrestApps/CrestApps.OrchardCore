using CrestApps.Core;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceProvider _serviceProvider;
    private ISurveyUserLookup _users;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponsesRecipeStep"/> class.
    /// </summary>
    /// <param name="responseStore">The response store.</param>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="versionStore">The version store.</param>
    /// <param name="serviceProvider">The service provider. The user lookup is resolved from it only when a record names a person, so a missing optional service never breaks recipe execution.</param>
    public SurveyResponsesRecipeStep(
        SurveyResponseStore responseStore,
        SurveyStore surveyStore,
        SurveyVersionStore versionStore,
        IServiceProvider serviceProvider)
        : base(SurveysRecipeSteps.Responses)
    {
        _responseStore = responseStore;
        _surveyStore = surveyStore;
        _versionStore = versionStore;
        _serviceProvider = serviceProvider;
    }

    private ISurveyUserLookup UserLookup => _users ??= _serviceProvider.GetRequiredService<ISurveyUserLookup>();

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
            var response = existing ?? SurveysRecipeJson.Read<SurveyResponse>(token, "SurveyName", "UserName", "GradedByUserName", nameof(SurveyResponse.UpkeepFailures));

            if (existing is not null)
            {
                SurveysRecipeJson.Populate(existing, token, "SurveyName", "UserName", "GradedByUserName", nameof(SurveyResponse.ResumeTokenHash), nameof(SurveyResponse.UpkeepFailures));
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
                var userId = await UserLookup.FindUserIdAsync(userName);

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
                var names = await UserLookup.GetUserNamesAsync([response.UserId]);

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

            response.GradedBy = await UserLookup.FindUserIdAsync(SurveysRecipeJson.String(token, "GradedByUserName"));
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
