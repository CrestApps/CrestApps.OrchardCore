using CrestApps.Core;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using OrchardCore;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Surveys.Recipes;

/// <summary>
/// Imports invitations. Tokens travel only as their hashes, so the personal links sent before keep working. An
/// invitation joins the survey's invitation list of the same name on this site (collector ids differ between sites),
/// else the list of its id when that is one of the survey's invitation lists, else the survey's first invitation list,
/// else a new personal links list.
/// </summary>
internal sealed class SurveyInvitationsRecipeStep : NamedRecipeStepHandler
{
    private readonly SurveyInvitationStore _invitationStore;
    private readonly SurveyStore _surveyStore;
    private readonly SurveyCollectorStore _collectorStore;
    private readonly ISurveyUserLookup _userLookup;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationsRecipeStep"/> class.
    /// </summary>
    /// <param name="invitationStore">The invitation store.</param>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="collectorStore">The collector store, which finds the invitation list on this site.</param>
    /// <param name="userLookup">The user lookup, which turns user ids into user names and back.</param>
    /// <param name="clock">The clock.</param>
    public SurveyInvitationsRecipeStep(
        SurveyInvitationStore invitationStore,
        SurveyStore surveyStore,
        SurveyCollectorStore collectorStore,
        ISurveyUserLookup userLookup,
        IClock clock)
        : base(SurveysRecipeSteps.Invitations)
    {
        _invitationStore = invitationStore;
        _surveyStore = surveyStore;
        _collectorStore = collectorStore;
        _userLookup = userLookup;
        _clock = clock;
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

            var collector = await FindCollectorAsync(survey, SurveysRecipeJson.String(token, "CollectorName"), SurveysRecipeJson.String(token, nameof(SurveyInvitation.CollectorId)));

            var existing = string.IsNullOrEmpty(id) ? null : await _invitationStore.FindByIdAsync(id);
            var invitation = existing ?? SurveysRecipeJson.Read<SurveyInvitation>(token, "SurveyName", "UserName", "CollectorName");

            if (existing is not null)
            {
                SurveysRecipeJson.Populate(existing, token, "SurveyName", "UserName", "CollectorName");
            }

            invitation.CollectorId = collector.ItemId;

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

    private async Task<SurveyCollector> FindCollectorAsync(Survey survey, string collectorName, string collectorId)
    {
        var lists = (await _collectorStore.ListBySurveyAsync(survey.ItemId))
            .Where(collector => collector.Kind == SurveyCollectorKinds.Invitation)
            .ToList();

        return (string.IsNullOrEmpty(collectorName) ? null : lists.FirstOrDefault(collector => string.Equals(collector.Name, collectorName, StringComparison.OrdinalIgnoreCase)))
            ?? (string.IsNullOrEmpty(collectorId) ? null : lists.FirstOrDefault(collector => string.Equals(collector.ItemId, collectorId, StringComparison.Ordinal)))
            ?? lists.FirstOrDefault()
            ?? await CreatePersonalLinksAsync(survey);
    }

    // A survey with no invitation list on this site gets the personal links list, as inviting someone there would.
    private async Task<SurveyCollector> CreatePersonalLinksAsync(Survey survey)
    {
        var collector = SurveyInvitationService.NewPersonalLinksCollector(survey.ItemId, _clock.UtcNow);

        await _collectorStore.CreateAsync(collector);

        return collector;
    }
}
