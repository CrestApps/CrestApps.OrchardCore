using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Learning.Drivers;

/// <summary>
/// Shows a person's enrollments on the user editor to those who may view learner records. It only shows: enrollments are
/// changed on the enrollment pages, where each change keeps its reason.
/// </summary>
internal sealed class UserLearningDisplayDriver : DisplayDriver<User>
{
    private readonly LearnerRecordSummaryService _summaryService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserLearningDisplayDriver(
        LearnerRecordSummaryService summaryService,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _summaryService = summaryService;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<IDisplayResult> EditAsync(User user, BuildEditorContext context)
    {
        var principal = _httpContextAccessor.HttpContext?.User;

        if (string.IsNullOrEmpty(user.UserId) || principal is null || !await _authorizationService.AuthorizeAsync(principal, LearningPermissions.ViewLearnerRecords))
        {
            return null;
        }

        return Initialize<LearnerRecordSectionViewModel>("LearnerRecordSection", model => _summaryService.FillAsync(model, user.UserId))
            .Location("Content:20");
    }
}
