using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.ViewModels;
using CrestApps.OrchardCore.Users;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Drivers;

/// <summary>
/// The rows of the enrollment list, in the List and Grid layouts.
/// </summary>
internal sealed class EnrollmentRowDisplayDriver : DisplayDriver<Enrollment>
{
    private readonly IUserSearchService _userSearchService;
    private readonly IContentManager _contentManager;

    public EnrollmentRowDisplayDriver(
        IUserSearchService userSearchService,
        IContentManager contentManager)
    {
        _userSearchService = userSearchService;
        _contentManager = contentManager;
    }

    public override async Task<IDisplayResult> DisplayAsync(Enrollment enrollment, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape row)
        {
            row.Classes.Add("item");
        }

        var learners = await _userSearchService.GetAsync([enrollment.UserId]);
        var learner = learners.Count > 0 ? learners[0] : null;
        var course = await _contentManager.GetAsync(enrollment.CourseId, VersionOptions.Latest);

        void Populate(EnrollmentRowViewModel model)
        {
            model.Enrollment = enrollment;
            model.LearnerName = string.IsNullOrWhiteSpace(learner?.DisplayName) ? learner?.UserName ?? enrollment.UserId : learner.DisplayName;
            model.CourseTitle = course?.DisplayText ?? enrollment.CourseId;
        }

        return Combine(
            Initialize<EnrollmentRowViewModel>("Enrollment_Fields_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Learner:1"),
            Initialize<EnrollmentRowViewModel>("Enrollment_Course_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Course:1"),
            Initialize<EnrollmentRowViewModel>("Enrollment_Tags_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            Initialize<EnrollmentRowViewModel>("Enrollment_Buttons_SummaryAdmin", Populate).Location("Actions:5"),
            Initialize<EnrollmentRowViewModel>("Enrollment_ActionsMenu_SummaryAdmin", Populate).Location("ActionsMenu:10"));
    }
}
