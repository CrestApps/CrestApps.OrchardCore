using CrestApps.OrchardCore.Skills.Core.ViewModels;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Skills.Core.Drivers;

/// <summary>
/// Adds a Skills section to the editor of a record that holds skills, such as a user or a worker, with one proficiency
/// choice per enabled skill. Only users allowed to assign skills see it.
/// </summary>
/// <remarks>
/// A module adds the section to its own editor with a small driver that says who holds the skills, and registers it
/// with <c>services.AddDisplayDriver&lt;TModel, TDriver&gt;()</c>. The assignments are written when the editor is
/// updated, so the editor's controller cancels the session when the record is not saved, the way Orchard Core's content
/// editor does. Skills that were disabled later are not on the form, so they stay as they are held.
/// </remarks>
/// <typeparam name="TModel">The edited record.</typeparam>
public abstract class SkillSubjectSkillsDisplayDriverBase<TModel> : DisplayDriver<TModel>
    where TModel : class
{
    private readonly ISkillManager _skillManager;
    private readonly ISkillAssignmentService _assignmentService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkillSubjectSkillsDisplayDriverBase{TModel}"/> class.
    /// </summary>
    /// <param name="skillManager">The skill manager.</param>
    /// <param name="assignmentService">The skill assignment service.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor, for the signed-in user.</param>
    /// <param name="authorizationService">The authorization service.</param>
    protected SkillSubjectSkillsDisplayDriverBase(
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

    /// <summary>
    /// Gets the shape type of the section, whose view takes a <see cref="SubjectSkillsViewModel"/>.
    /// </summary>
    protected abstract string ShapeType { get; }

    /// <summary>
    /// Gets where the section is placed in the editor.
    /// </summary>
    protected virtual string Location => "Content:10";

    /// <summary>
    /// Says who holds the skills of the edited record, or returns an empty subject when the record can't hold skills
    /// yet, which hides the section.
    /// </summary>
    /// <param name="model">The edited record.</param>
    /// <param name="isNew">Whether the editor creates the record.</param>
    /// <returns>The subject.</returns>
    protected abstract SkillSubject GetSubject(TModel model, bool isNew);

    /// <inheritdoc/>
    public override async Task<IDisplayResult> EditAsync(TModel model, BuildEditorContext context)
    {
        var subject = GetSubject(model, context.IsNew);

        if (subject.IsEmpty || !await CanAssignAsync())
        {
            return null;
        }

        var skills = await _skillManager.GetEnabledAsync();

        if (skills.Count == 0)
        {
            return null;
        }

        var held = context.IsNew
            ? new Dictionary<string, SkillAssignment>(StringComparer.Ordinal)
            : (await _assignmentService.GetAssignmentsAsync(subject)).ToDictionary(assignment => assignment.SkillId, StringComparer.Ordinal);

        return Initialize<SubjectSkillsViewModel>(ShapeType, viewModel =>
        {
            viewModel.Skills = skills.Select(skill => new SubjectSkillRowViewModel
            {
                SkillId = skill.ItemId,
                Name = skill.Name,
                Category = skill.Category,
                Proficiency = held.TryGetValue(skill.ItemId, out var assignment) ? assignment.Proficiency : 0,
            }).ToList();
        }).Location(Location);
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(TModel model, UpdateEditorContext context)
    {
        var subject = GetSubject(model, context.IsNew);

        if (subject.IsEmpty || !await CanAssignAsync())
        {
            return null;
        }

        var viewModel = new SubjectSkillsViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        var actor = _httpContextAccessor.HttpContext?.User.Identity?.Name;
        var held = (await _assignmentService.GetAssignmentsAsync(subject)).ToDictionary(assignment => assignment.SkillId, StringComparer.Ordinal);
        var enabled = (await _skillManager.GetEnabledAsync()).Select(skill => skill.ItemId).ToHashSet(StringComparer.Ordinal);

        foreach (var row in (viewModel.Skills ?? []).Where(row => !string.IsNullOrEmpty(row.SkillId) && enabled.Contains(row.SkillId)))
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

        return await EditAsync(model, context);
    }

    private async Task<bool> CanAssignAsync()
    {
        var principal = _httpContextAccessor.HttpContext?.User;

        return principal is not null && await _authorizationService.AuthorizeAsync(principal, SkillsPermissions.AssignSkills);
    }
}
