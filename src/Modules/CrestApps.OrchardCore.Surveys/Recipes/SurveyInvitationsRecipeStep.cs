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
/// Imports invitations. Tokens travel only as their hashes, so the personal links sent before keep working.
/// </summary>
internal sealed class SurveyInvitationsRecipeStep : NamedRecipeStepHandler
{
    private readonly SurveyInvitationStore _invitationStore;
    private readonly SurveyStore _surveyStore;
    private readonly ISurveyUserLookup _userLookup;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationsRecipeStep"/> class.
    /// </summary>
    /// <param name="invitationStore">The invitation store.</param>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="userLookup">The user lookup, which turns user ids into user names and back.</param>
    public SurveyInvitationsRecipeStep(
        SurveyInvitationStore invitationStore,
        SurveyStore surveyStore,
        ISurveyUserLookup userLookup)
        : base(SurveysRecipeSteps.Invitations)
    {
        _invitationStore = invitationStore;
        _surveyStore = surveyStore;
        _userLookup = userLookup;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        foreach (var token in SurveysRecipeJson.Array(context.Step, SurveysRecipeSteps.InvitationsSection))
        {
            var id = SurveysRecipeJson.String(token, nameof(SurveyInvitation.ItemId));
            var survey = await _surveyStore.FindByNameAsync(SurveysRecipeJson.String(token, "SurveyName"));

            if (survey is null)
            {
                context.Errors.Add($"The invitation '{id}' names a survey that is not on the site.");

                continue;
            }

            var existing = string.IsNullOrEmpty(id) ? null : await _invitationStore.FindByIdAsync(id);
            var invitation = existing ?? SurveysRecipeJson.Read<SurveyInvitation>(token, "SurveyName", "UserName");

            if (existing is not null)
            {
                SurveysRecipeJson.Populate(existing, token, "SurveyName", "UserName");
            }

            var userName = SurveysRecipeJson.String(token, "UserName");

            if (userName is not null)
            {
                invitation.UserId = await _userLookup.FindUserIdAsync(userName);

                if (invitation.UserId is null)
                {
                    context.Errors.Add($"The invitation '{id}' is for the user '{userName}', who is not on the site.");

                    continue;
                }

                invitation.Recipient ??= new PartyRef(PartyKinds.User, invitation.UserId);
            }

            invitation.SurveyId = survey.ItemId;

            if (survey.Access?.Anonymity == AnonymityMode.Anonymous)
            {
                invitation.ResponseId = null;
            }

            if (existing is null)
            {
                invitation.ItemId = !string.IsNullOrEmpty(id) && UniqueId.IsValid(id) ? id : IdGenerator.GenerateId();
                await _invitationStore.CreateAsync(invitation);
            }
            else
            {
                await _invitationStore.UpdateAsync(invitation);
            }
        }
    }
}
