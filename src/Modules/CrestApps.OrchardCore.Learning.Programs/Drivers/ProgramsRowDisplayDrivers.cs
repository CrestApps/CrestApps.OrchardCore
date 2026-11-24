using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Programs.Core;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using CrestApps.OrchardCore.Learning.Programs.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Programs.Drivers;

/// <summary>
/// The row of a person on the people of a learning path.
/// </summary>
internal sealed class LearningPathProgressRowDisplayDriver : DisplayDriver<LearningPathProgress>
{
    private readonly LearnerUserLookup _users;
    private readonly IContentManager _contentManager;

    public LearningPathProgressRowDisplayDriver(LearnerUserLookup users, IContentManager contentManager)
    {
        _users = users;
        _contentManager = contentManager;
    }

    public override IDisplayResult Display(LearningPathProgress progress, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape row)
        {
            row.Classes.Add("item");
        }

        async ValueTask Populate(LearningPathProgressRowViewModel model)
        {
            model.Progress = progress;
            model.UserName = (await _users.GetUserNamesAsync([progress.UserId])).TryGetValue(progress.UserId, out var name) ? name : progress.UserId;

            var path = await _contentManager.GetAsync(progress.PathId, VersionOptions.Published);

            model.StageCount = path is not null && path.TryGet<LearningPathPart>(out var part) ? LearningPathRules.Stages(part).Count : 0;
        }

        return Combine(
            Initialize<LearningPathProgressRowViewModel>("LearningPathProgress_Fields_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            Initialize<LearningPathProgressRowViewModel>("LearningPathProgress_State_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "State:1"));
    }
}

/// <summary>
/// The row of a rule on the required learning list.
/// </summary>
internal sealed class LearningRequirementRowDisplayDriver : DisplayDriver<LearningRequirement>
{
    private readonly IEnumerable<IRequiredLearningTarget> _targets;
    private readonly IUserAudienceService _audiences;

    public LearningRequirementRowDisplayDriver(IEnumerable<IRequiredLearningTarget> targets, IUserAudienceService audiences)
    {
        _targets = targets;
        _audiences = audiences;
    }

    public override IDisplayResult Display(LearningRequirement requirement, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape row)
        {
            row.Classes.Add("item");
        }

        async ValueTask Populate(LearningRequirementRowViewModel model)
        {
            var target = _targets.FirstOrDefault(candidate => candidate.Kind == requirement.TargetKind);

            model.Requirement = requirement;
            model.TargetKindName = target?.DisplayName ?? requirement.TargetKind;
            model.TargetName = target is null ? requirement.TargetId : await target.GetDisplayTextAsync(requirement.TargetId) ?? requirement.TargetId;
            model.AudienceName = string.IsNullOrEmpty(requirement.AudienceName)
                ? null
                : (await _audiences.ListAsync()).FirstOrDefault(audience => string.Equals(audience.Name, requirement.AudienceName, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? requirement.AudienceName;
        }

        return Combine(
            Initialize<LearningRequirementRowViewModel>("LearningRequirement_Fields_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            Initialize<LearningRequirementRowViewModel>("LearningRequirement_Who_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Who:1"),
            Initialize<LearningRequirementRowViewModel>("LearningRequirement_Buttons_SummaryAdmin", Populate).Location("Actions:5"),
            Initialize<LearningRequirementRowViewModel>("LearningRequirement_ActionsMenu_SummaryAdmin", Populate).Location("ActionsMenu:10"));
    }
}

/// <summary>
/// The row of a person under a required learning rule.
/// </summary>
internal sealed class RequiredLearningAssignmentRowDisplayDriver : DisplayDriver<RequiredLearningAssignment>
{
    private readonly LearnerUserLookup _users;
    private readonly ILocalClock _localClock;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RequiredLearningAssignmentRowDisplayDriver(LearnerUserLookup users, ILocalClock localClock, IAuthorizationService authorizationService, IHttpContextAccessor httpContextAccessor)
    {
        _users = users;
        _localClock = localClock;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override IDisplayResult Display(RequiredLearningAssignment assignment, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape row)
        {
            row.Classes.Add("item");
        }

        async ValueTask Populate(RequiredLearningAssignmentRowViewModel model)
        {
            model.Assignment = assignment;
            model.UserName = (await _users.GetUserNamesAsync([assignment.UserId])).TryGetValue(assignment.UserId, out var name) ? name : assignment.UserId;
            model.Due = await ProgramsDates.FormatAsync(_localClock, assignment.DueUtc);
            model.Completed = await ProgramsDates.FormatAsync(_localClock, assignment.CompletedUtc);
            model.CanExempt = _httpContextAccessor.HttpContext?.User is { } user && await _authorizationService.AuthorizeAsync(user, LearningProgramsPermissions.ExemptFromRequiredLearning);
        }

        return Combine(
            Initialize<RequiredLearningAssignmentRowViewModel>("RequiredLearningAssignment_Fields_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            Initialize<RequiredLearningAssignmentRowViewModel>("RequiredLearningAssignment_State_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "State:1"),
            Initialize<RequiredLearningAssignmentRowViewModel>("RequiredLearningAssignment_Dates_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Dates:1"),
            Initialize<RequiredLearningAssignmentRowViewModel>("RequiredLearningAssignment_ActionsMenu_SummaryAdmin", Populate).Location("ActionsMenu:10"));
    }
}

/// <summary>
/// Shows dates in the site's time zone.
/// </summary>
internal static class ProgramsDates
{
    public static async Task<string> FormatAsync(ILocalClock localClock, DateTime? utc, bool withTime = false)
    {
        if (utc is not { } value)
        {
            return null;
        }

        var local = await localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)));

        return withTime ? local.ToString("g") : local.ToString("d");
    }
}
