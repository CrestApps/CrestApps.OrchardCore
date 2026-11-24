using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.ViewModels;
using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Staff.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Drivers;

/// <summary>
/// The Learning section of the staff profile: the person's enrollments, for those who may view learner records.
/// </summary>
internal sealed class StaffLearningProfileDisplayDriver : DisplayDriver<StaffMember>
{
    /// <summary>
    /// The name of the section on the staff profile page.
    /// </summary>
    public const string SectionName = "Learning";

    private readonly LearnerRecordSummaryService _summaryService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public StaffLearningProfileDisplayDriver(
        LearnerRecordSummaryService summaryService,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _summaryService = summaryService;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<IDisplayResult> DisplayAsync(StaffMember staff, BuildDisplayContext context)
    {
        var principal = _httpContextAccessor.HttpContext?.User;

        if (context.DisplayType != StaffProfileSections.DisplayType
            || string.IsNullOrEmpty(staff.UserId)
            || principal is null
            || !await _authorizationService.AuthorizeAsync(principal, LearningPermissions.ViewLearnerRecords))
        {
            return null;
        }

        return Initialize<LearnerRecordSectionViewModel>("LearnerRecordSection", async model => await _summaryService.FillAsync(model, staff.UserId))
            .Location(StaffProfileSections.DisplayType, StaffProfileSections.Location(SectionName, "1"));
    }
}
