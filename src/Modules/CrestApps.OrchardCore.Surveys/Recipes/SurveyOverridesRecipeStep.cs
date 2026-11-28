using CrestApps.Core;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Surveys.Recipes;

/// <summary>
/// Imports accommodations. People are named by user name; a user who is not on the site is an error.
/// </summary>
internal sealed class SurveyOverridesRecipeStep : NamedRecipeStepHandler
{
    private readonly SurveyOverrideStore _overrideStore;
    private readonly SurveyStore _surveyStore;
    private readonly ISurveyUserLookup _userLookup;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyOverridesRecipeStep"/> class.
    /// </summary>
    /// <param name="overrideStore">The accommodation store.</param>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="userLookup">The user lookup, which turns user ids into user names and back.</param>
    public SurveyOverridesRecipeStep(
        SurveyOverrideStore overrideStore,
        SurveyStore surveyStore,
        ISurveyUserLookup userLookup)
        : base(SurveysRecipeSteps.Overrides)
    {
        _overrideStore = overrideStore;
        _surveyStore = surveyStore;
        _userLookup = userLookup;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        foreach (var token in SurveysRecipeJson.Array(context.Step, SurveysRecipeSteps.OverridesSection))
        {
            var id = SurveysRecipeJson.String(token, nameof(SurveyOverride.ItemId));
            var survey = await _surveyStore.FindByNameAsync(SurveysRecipeJson.String(token, "SurveyName"));

            if (survey is null)
            {
                context.Errors.Add($"The accommodation '{id}' names a survey that is not on the site.");

                continue;
            }

            var existing = string.IsNullOrEmpty(id) ? null : await _overrideStore.FindByIdAsync(id);
            var entry = existing ?? SurveysRecipeJson.Read<SurveyOverride>(token, "SurveyName", "UserName", "ActorUserName");

            if (existing is not null)
            {
                SurveysRecipeJson.Populate(existing, token, "SurveyName", "UserName", "ActorUserName");
            }

            var userName = SurveysRecipeJson.String(token, "UserName");

            if (userName is not null)
            {
                entry.UserId = await _userLookup.FindUserIdAsync(userName);

                if (entry.UserId is null)
                {
                    context.Errors.Add($"The accommodation '{id}' is for the user '{userName}', who is not on the site.");

                    continue;
                }
            }

            entry.SurveyId = survey.ItemId;
            entry.Actor = await _userLookup.FindUserIdAsync(SurveysRecipeJson.String(token, "ActorUserName"));

            if (existing is null)
            {
                entry.ItemId = !string.IsNullOrEmpty(id) && UniqueId.IsValid(id) ? id : IdGenerator.GenerateId();
                await _overrideStore.CreateAsync(entry);
            }
            else
            {
                await _overrideStore.UpdateAsync(entry);
            }
        }
    }
}
