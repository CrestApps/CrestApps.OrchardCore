using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.ViewModels;
using CrestApps.OrchardCore.Users;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Assessments.Drivers;

internal sealed class AttemptRowDisplayDriver : DisplayDriver<AssessmentAttempt>
{
    private readonly IUserSearchService _userSearchService;

    public AttemptRowDisplayDriver(IUserSearchService userSearchService)
    {
        _userSearchService = userSearchService;
    }

    public override async Task<IDisplayResult> DisplayAsync(AssessmentAttempt attempt, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape row)
        {
            row.Classes.Add("item");
        }

        var learners = await _userSearchService.GetAsync([attempt.UserId]);
        var learner = learners.Count > 0 ? learners[0] : null;

        void Populate(AttemptRowViewModel model)
        {
            model.Attempt = attempt;
            model.LearnerName = string.IsNullOrWhiteSpace(learner?.DisplayName) ? learner?.UserName ?? attempt.UserId : learner.DisplayName;
        }

        return Combine(
            Initialize<AttemptRowViewModel>("AssessmentAttempt_Fields_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Learner:1"),
            Initialize<AttemptRowViewModel>("AssessmentAttempt_Quiz_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Quiz:1"),
            Initialize<AttemptRowViewModel>("AssessmentAttempt_Submitted_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Submitted:5"),
            Initialize<AttemptRowViewModel>("AssessmentAttempt_Buttons_SummaryAdmin", Populate).Location("Actions:5"),
            Initialize<AttemptRowViewModel>("AssessmentAttempt_ActionsMenu_SummaryAdmin", Populate).Location("ActionsMenu:10"));
    }
}
