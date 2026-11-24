using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Learning.Certifications.ViewModels;
using CrestApps.OrchardCore.Learning.Core.Services;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Certifications.Drivers;

/// <summary>
/// The row of a credit type.
/// </summary>
internal sealed class EducationCreditTypeRowDisplayDriver : DisplayDriver<EducationCreditType>
{
    public override IDisplayResult Display(EducationCreditType type, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        return Combine(
            View("EducationCreditType_Fields_SummaryAdmin", type)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("EducationCreditType_Buttons_SummaryAdmin", type).Location("Actions:5"),
            View("EducationCreditType_ActionsMenu_SummaryAdmin", type).Location("ActionsMenu:10"));
    }
}

/// <summary>
/// The row of external learning on the review list.
/// </summary>
internal sealed class ExternalLearningRowDisplayDriver : DisplayDriver<ExternalLearningSubmission>
{
    private readonly LearnerUserLookup _users;
    private readonly IEducationCreditService _credits;
    private readonly ILocalClock _localClock;

    public ExternalLearningRowDisplayDriver(LearnerUserLookup users, IEducationCreditService credits, ILocalClock localClock)
    {
        _users = users;
        _credits = credits;
        _localClock = localClock;
    }

    public override IDisplayResult Display(ExternalLearningSubmission submission, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape row)
        {
            row.Classes.Add("item");
        }

        async ValueTask Populate(ExternalLearningRowViewModel model)
        {
            model.Submission = submission;
            model.UserName = (await _users.GetUserNamesAsync([submission.UserId])).TryGetValue(submission.UserId, out var name) ? name : submission.UserId;
            model.TypeName = (await _credits.FindTypeAsync(submission.CreditTypeId))?.Name ?? submission.CreditTypeId;
            model.Submitted = (await _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(submission.SubmittedUtc, DateTimeKind.Utc)))).ToString("d");
        }

        return Combine(
            Initialize<ExternalLearningRowViewModel>("ExternalLearningSubmission_Fields_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            Initialize<ExternalLearningRowViewModel>("ExternalLearningSubmission_State_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "State:1"),
            Initialize<ExternalLearningRowViewModel>("ExternalLearningSubmission_Buttons_SummaryAdmin", Populate).Location("Actions:5"));
    }
}
