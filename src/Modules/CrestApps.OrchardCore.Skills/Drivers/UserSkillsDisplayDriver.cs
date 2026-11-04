using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Skills.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Skills.Drivers;

/// <summary>
/// Adds a Skills section to the user editor, with one proficiency choice per enabled skill. Only users allowed to
/// assign skills see it.
/// </summary>
internal sealed class UserSkillsDisplayDriver : DisplayDriver<User>
{
    private readonly ISkillManager _skillManager;
    private readonly ISkillAssignmentService _assignmentService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public UserSkillsDisplayDriver(
        ISkillManager skillManager,
        ISkillAssignmentService assignmentService,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _skillManager = skillManager;
        _assignmentService = assignmentService;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    public override async Task<IDisplayResult> EditAsync(User user, BuildEditorContext context)
    {
        if (string.IsNullOrEmpty(user.UserId) || !await CanAssignAsync())
        {
            return null;
        }

        var skills = await _skillManager.GetEnabledAsync();

        if (skills.Count == 0)
        {
            return null;
        }

        var held = (await _assignmentService.GetAssignmentsAsync(SkillSubject.User(user.UserId))).ToDictionary(assignment => assignment.SkillId, StringComparer.Ordinal);

        return Initialize<UserSkillsViewModel>("UserSkills_Edit", model =>
        {
            model.Skills = skills.Select(skill => new UserSkillRowViewModel
            {
                SkillId = skill.ItemId,
                Name = skill.Name,
                Category = skill.Category,
                Proficiency = held.TryGetValue(skill.ItemId, out var assignment) ? assignment.Proficiency : 0,
            }).ToList();
        }).Location("Content:12");
    }

    public override async Task<IDisplayResult> UpdateAsync(User user, UpdateEditorContext context)
    {
        if (string.IsNullOrEmpty(user.UserId) || !await CanAssignAsync())
        {
            return null;
        }

        var model = new UserSkillsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var subject = SkillSubject.User(user.UserId);
        var actor = _httpContextAccessor.HttpContext?.User.Identity?.Name;
        var held = (await _assignmentService.GetAssignmentsAsync(subject)).ToDictionary(assignment => assignment.SkillId, StringComparer.Ordinal);
        var enabled = (await _skillManager.GetEnabledAsync()).Select(skill => skill.ItemId).ToHashSet(StringComparer.Ordinal);

        // Only the enabled skills are on the form, so skills that were disabled later stay as the user holds them.
        foreach (var row in model.Skills.Where(row => !string.IsNullOrEmpty(row.SkillId) && enabled.Contains(row.SkillId)))
        {
            held.TryGetValue(row.SkillId, out var existing);

            if (!SkillProficiency.IsValid(row.Proficiency))
            {
                if (existing is not null)
                {
                    await _assignmentService.RemoveAsync(subject, row.SkillId, actor);
                }

                continue;
            }

            var result = await _assignmentService.AssignAsync(subject, new SkillAssignmentInput
            {
                SkillId = row.SkillId,
                Proficiency = row.Proficiency,
                EffectiveFrom = existing?.EffectiveFrom,
                EffectiveTo = existing?.EffectiveTo,
            }, actor);

            foreach (var error in result.Errors)
            {
                context.Updater.ModelState.AddModelError(Prefix, error);
            }
        }

        return await EditAsync(user, context);
    }

    private async Task<bool> CanAssignAsync()
    {
        var principal = _httpContextAccessor.HttpContext?.User;

        return principal is not null && await _authorizationService.AuthorizeAsync(principal, SkillsPermissions.AssignSkills);
    }
}
