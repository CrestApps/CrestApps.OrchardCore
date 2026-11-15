using CrestApps.OrchardCore.FileStorage;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Programs.Core;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using CrestApps.OrchardCore.Learning.Programs.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Programs.Controllers;

/// <summary>
/// Required learning rules: the list, the editor, who a rule covers, the people under it, exemptions and Run now.
/// </summary>
[Feature(ProgramsConstants.Features.RequiredLearning)]
public sealed class RequiredLearningController : Controller
{
    private const long MaxEvidenceBytes = 10 * 1024 * 1024;

    private readonly IAuthorizationService _authorizationService;
    private readonly IRequiredLearningService _requiredLearning;
    private readonly IEnumerable<IRequiredLearningTarget> _targets;
    private readonly IUserAudienceService _audiences;
    private readonly LearnerUserLookup _users;
    private readonly ILocalClock _localClock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public RequiredLearningController(
        IAuthorizationService authorizationService,
        IRequiredLearningService requiredLearning,
        IEnumerable<IRequiredLearningTarget> targets,
        IUserAudienceService audiences,
        LearnerUserLookup users,
        ILocalClock localClock,
        INotifier notifier,
        IHtmlLocalizer<RequiredLearningController> htmlLocalizer,
        IStringLocalizer<RequiredLearningController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _requiredLearning = requiredLearning;
        _targets = targets;
        _audiences = audiences;
        _users = users;
        _localClock = localClock;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the rules.
    /// </summary>
    [Admin("learning/required", "RequiredLearning")]
    public async Task<IActionResult> Index(string search)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningProgramsPermissions.ManageRequiredLearning))
        {
            return Forbid();
        }

        var rules = (await _requiredLearning.ListAsync(HttpContext.RequestAborted))
            .Where(rule => string.IsNullOrWhiteSpace(search) || rule.Name?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) == true)
            .OrderBy(rule => rule.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return View(new RequiredLearningListViewModel
        {
            Search = search,
            List = new AdminListContext<LearningRequirement> { Source = rules },
        });
    }

    /// <summary>
    /// Shows the editor of a new or existing rule.
    /// </summary>
    [Admin("learning/required/edit/{id?}", "RequiredLearningEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningProgramsPermissions.ManageRequiredLearning))
        {
            return Forbid();
        }

        var model = new RequiredLearningEditViewModel { TargetKind = ProgramsConstants.TargetKinds.Course };

        if (!string.IsNullOrEmpty(id))
        {
            var rule = await _requiredLearning.FindByIdAsync(id, HttpContext.RequestAborted);

            if (rule is null)
            {
                return NotFound();
            }

            var names = await _users.GetUserNamesAsync(rule.UserIds);

            model = new RequiredLearningEditViewModel
            {
                Id = rule.ItemId,
                Name = rule.Name,
                Enabled = rule.Enabled,
                TargetKind = rule.TargetKind,
                TargetId = rule.TargetId,
                AudienceName = rule.AudienceName,
                UserNames = string.Join(", ", rule.UserIds.Select(userId => names.TryGetValue(userId, out var name) ? name : userId)),
                DueRule = rule.DueRule,
                DueDate = rule.DueDate,
                DueDays = rule.DueDays,
                ReminderDays = string.Join(", ", rule.ReminderDays),
                EscalateAfterDays = rule.EscalateAfterDays,
            };
        }

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Saves a rule.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("learning/required/edit/{id?}", "RequiredLearningEdit")]
    public async Task<IActionResult> EditPost(RequiredLearningEditViewModel model, string submit)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningProgramsPermissions.ManageRequiredLearning))
        {
            return Forbid();
        }

        var rule = string.IsNullOrEmpty(model.Id) ? new LearningRequirement() : await _requiredLearning.FindByIdAsync(model.Id, HttpContext.RequestAborted);

        if (rule is null)
        {
            return NotFound();
        }

        var userIds = new List<string>();

        foreach (var userName in (model.UserNames ?? string.Empty).Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var userId = await _users.FindUserIdAsync(userName);

            if (userId is null)
            {
                ModelState.AddModelError(nameof(model.UserNames), S["No user is named '{0}'.", userName]);
            }
            else
            {
                userIds.Add(userId);
            }
        }

        var reminders = new List<int>();

        foreach (var value in (model.ReminderDays ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(value, out var days) && days > 0)
            {
                reminders.Add(days);
            }
            else
            {
                ModelState.AddModelError(nameof(model.ReminderDays), S["'{0}' is not a number of days.", value]);
            }
        }

        if (ModelState.IsValid)
        {
            rule.Name = model.Name;
            rule.Enabled = model.Enabled;
            rule.TargetKind = model.TargetKind;
            rule.TargetId = model.TargetId;
            rule.AudienceName = model.AudienceName;
            rule.UserIds = userIds;
            rule.DueRule = model.DueRule;
            rule.DueDate = model.DueRule == RequiredLearningDueRule.FixedDate ? model.DueDate : null;
            rule.DueDays = model.DueDays;
            rule.ReminderDays = reminders;
            rule.EscalateAfterDays = model.EscalateAfterDays;

            var errors = await _requiredLearning.SaveAsync(rule, HttpContext.RequestAborted);

            foreach (var error in errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            if (errors.Count == 0)
            {
                if (submit == "run")
                {
                    var result = await _requiredLearning.RunAsync(rule.ItemId, User.Identity?.Name, HttpContext.RequestAborted);

                    await _notifier.SuccessAsync(H["The rule has been saved and run: it covers {0} people, {1} newly.", result.Covered, result.Added]);

                    return RedirectToAction(nameof(People), new { id = rule.ItemId });
                }

                await _notifier.SuccessAsync(H["The rule has been saved. It runs within the hour, or click Run now."]);

                return RedirectToAction(nameof(Index));
            }
        }

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Deletes a rule and the states of its people. Enrollments it made stay.
    /// </summary>
    [HttpPost]
    [Admin("learning/required/delete/{id}", "RequiredLearningDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningProgramsPermissions.ManageRequiredLearning))
        {
            return Forbid();
        }

        if (!await _requiredLearning.DeleteAsync(id, HttpContext.RequestAborted))
        {
            return NotFound();
        }

        await _notifier.SuccessAsync(H["The rule has been deleted. The enrollments it made stay."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Applies a rule now.
    /// </summary>
    [HttpPost]
    [Admin("learning/required/run/{id}", "RequiredLearningRun")]
    public async Task<IActionResult> Run(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningProgramsPermissions.ManageRequiredLearning))
        {
            return Forbid();
        }

        if (await _requiredLearning.FindByIdAsync(id, HttpContext.RequestAborted) is null)
        {
            return NotFound();
        }

        var result = await _requiredLearning.RunAsync(id, User.Identity?.Name, HttpContext.RequestAborted);

        await _notifier.SuccessAsync(H["The rule covers {0} people: {1} newly required, {2} no longer required, {3} states changed.", result.Covered, result.Added, result.Removed, result.Changed]);

        return RedirectToAction(nameof(People), new { id });
    }

    /// <summary>
    /// Lists who a rule covers today, without changing anything.
    /// </summary>
    [Admin("learning/required/preview/{id}", "RequiredLearningPreview")]
    public async Task<IActionResult> Preview(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningProgramsPermissions.ManageRequiredLearning))
        {
            return Forbid();
        }

        var rule = await _requiredLearning.FindByIdAsync(id, HttpContext.RequestAborted);

        if (rule is null)
        {
            return NotFound();
        }

        var userIds = await _requiredLearning.PreviewAsync(rule, HttpContext.RequestAborted);
        var shown = userIds.Take(500).ToArray();
        var names = await _users.GetUserNamesAsync(shown);
        var already = (await _requiredLearning.ListForUsersAsync(shown, HttpContext.RequestAborted))
            .Where(assignment => assignment.RequirementId == rule.ItemId && assignment.State != RequiredLearningState.NotRequired)
            .Select(assignment => assignment.UserId)
            .ToHashSet(StringComparer.Ordinal);

        return View(new RequiredLearningPreviewViewModel
        {
            Requirement = rule,
            Count = userIds.Count,
            List = new AdminListContext<RequiredLearningPreviewRow>
            {
                Source = shown
                    .Select(userId => new RequiredLearningPreviewRow { UserId = userId, UserName = names.TryGetValue(userId, out var name) ? name : userId, AlreadyRequired = already.Contains(userId) })
                    .OrderBy(row => row.UserName, StringComparer.CurrentCultureIgnoreCase)
                    .ToList(),
            },
        });
    }

    /// <summary>
    /// Lists the people under a rule and their state.
    /// </summary>
    [Admin("learning/required/{id}/people", "RequiredLearningPeople")]
    public async Task<IActionResult> People(string id, RequiredLearningState? state, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningProgramsPermissions.ManageRequiredLearning))
        {
            return Forbid();
        }

        var rule = await _requiredLearning.FindByIdAsync(id, HttpContext.RequestAborted);

        if (rule is null)
        {
            return NotFound();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, items) = await _requiredLearning.PageAsync(id, state, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var routeValues = new RouteValueDictionary { { "id", id } };

        if (state is not null)
        {
            routeValues["state"] = state.ToString();
        }

        var target = _targets.FirstOrDefault(candidate => candidate.Kind == rule.TargetKind);

        return View(new RequiredLearningPeopleViewModel
        {
            Requirement = rule,
            TargetName = target is null ? rule.TargetId : await target.GetDisplayTextAsync(rule.TargetId, HttpContext.RequestAborted) ?? rule.TargetId,
            State = state,
            CanExempt = await _authorizationService.AuthorizeAsync(User, LearningProgramsPermissions.ExemptFromRequiredLearning),
            LastRun = await ProgramsDatesFormat(rule.LastRunUtc),
            List = new AdminListContext<RequiredLearningAssignment>
            {
                Source = items.ToList(),
                Pager = await shapeFactory.PagerAsync(pager, count, routeValues),
            },
        });
    }

    /// <summary>
    /// Shows the form that exempts a person.
    /// </summary>
    [Admin("learning/required/exempt/{id}", "RequiredLearningExempt")]
    public async Task<IActionResult> Exempt(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningProgramsPermissions.ExemptFromRequiredLearning))
        {
            return Forbid();
        }

        var assignment = await _requiredLearning.FindAssignmentAsync(id, HttpContext.RequestAborted);

        if (assignment is null)
        {
            return NotFound();
        }

        return View(await ExemptModelAsync(assignment, new RequiredLearningExemptViewModel { AssignmentId = id }));
    }

    /// <summary>
    /// Exempts a person, with a reason and an optional evidence file kept as a protected file.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Exempt))]
    [Admin("learning/required/exempt/{id}", "RequiredLearningExempt")]
    public async Task<IActionResult> ExemptPost(string id, RequiredLearningExemptViewModel model, IFormFile evidence, [FromServices] IProtectedFileStore<RequiredLearningEvidence> files)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningProgramsPermissions.ExemptFromRequiredLearning))
        {
            return Forbid();
        }

        var assignment = await _requiredLearning.FindAssignmentAsync(id, HttpContext.RequestAborted);

        if (assignment is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(model.Reason))
        {
            ModelState.AddModelError(nameof(model.Reason), S["Say why the person is exempt."]);
        }

        if (evidence is { Length: > MaxEvidenceBytes })
        {
            ModelState.AddModelError(string.Empty, S["The evidence file is larger than 10 MB."]);
        }

        if (!ModelState.IsValid)
        {
            return View(await ExemptModelAsync(assignment, model));
        }

        var exemption = new RequiredLearningExemption { Reason = model.Reason, GrantedBy = User.Identity?.Name };

        if (evidence is { Length: > 0 })
        {
            await using var stream = evidence.OpenReadStream();
            var saved = await files.SaveAsync(evidence.FileName, stream, HttpContext.RequestAborted);

            exemption.EvidenceFileId = saved.FileId;
            exemption.EvidenceFileName = saved.FileName;
        }

        if (await _requiredLearning.ExemptAsync(id, exemption, HttpContext.RequestAborted) is null)
        {
            await _notifier.ErrorAsync(H["A completed or no longer required person can't be exempted."]);
        }
        else
        {
            await _notifier.SuccessAsync(H["The person is exempt."]);
        }

        return RedirectToAction(nameof(People), new { id = assignment.RequirementId });
    }

    /// <summary>
    /// Takes an exemption back.
    /// </summary>
    [HttpPost]
    [Admin("learning/required/unexempt/{id}", "RequiredLearningRemoveExemption")]
    public async Task<IActionResult> RemoveExemption(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, LearningProgramsPermissions.ExemptFromRequiredLearning))
        {
            return Forbid();
        }

        var assignment = await _requiredLearning.RemoveExemptionAsync(id, User.Identity?.Name, HttpContext.RequestAborted);

        if (assignment is null)
        {
            return NotFound();
        }

        await _notifier.SuccessAsync(H["The exemption has been taken back."]);

        return RedirectToAction(nameof(People), new { id = assignment.RequirementId });
    }

    /// <summary>
    /// Opens the evidence of an exemption.
    /// </summary>
    [Admin("learning/required/evidence/{id}", "RequiredLearningEvidence")]
    public async Task<IActionResult> Evidence(string id, [FromServices] IProtectedFileStore<RequiredLearningEvidence> files, [FromServices] IEnumerable<IProtectedFileAccessPolicy<RequiredLearningEvidence>> policies)
    {
        var assignment = await _requiredLearning.FindAssignmentAsync(id, HttpContext.RequestAborted);

        if (assignment?.Exemption?.EvidenceFileId is not { } fileId)
        {
            return NotFound();
        }

        var access = new ProtectedFileAccess { FileId = fileId, ResourceId = assignment.ItemId, User = User };

        foreach (var policy in policies)
        {
            if (!await policy.CanReadAsync(access, HttpContext.RequestAborted))
            {
                return Forbid();
            }
        }

        var file = await files.FindAsync(fileId, HttpContext.RequestAborted);
        var content = file is null ? null : await files.OpenAsync(fileId, HttpContext.RequestAborted);

        if (content is null)
        {
            return NotFound();
        }

        return File(content, "application/octet-stream", file.FileName);
    }

    private async Task<RequiredLearningExemptViewModel> ExemptModelAsync(RequiredLearningAssignment assignment, RequiredLearningExemptViewModel model)
    {
        var rule = await _requiredLearning.FindByIdAsync(assignment.RequirementId, HttpContext.RequestAborted);

        model.AssignmentId = assignment.ItemId;
        model.RequirementId = assignment.RequirementId;
        model.RequirementName = rule?.Name;
        model.UserName = (await _users.GetUserNamesAsync([assignment.UserId])).TryGetValue(assignment.UserId, out var name) ? name : assignment.UserId;

        return model;
    }

    private Task<string> ProgramsDatesFormat(DateTime? utc)
        => Drivers.ProgramsDates.FormatAsync(_localClock, utc, withTime: true);

    private async Task PopulateAsync(RequiredLearningEditViewModel model)
    {
        var targets = new List<RequiredLearningTargetChoice>();

        foreach (var target in _targets)
        {
            targets.Add(new RequiredLearningTargetChoice
            {
                Kind = target.Kind,
                DisplayName = target.DisplayName,
                Options = (await target.GetOptionsAsync(HttpContext.RequestAborted))
                    .Select(option => new SelectListItem(option.DisplayText, option.Id, target.Kind == model.TargetKind && option.Id == model.TargetId))
                    .ToList(),
            });
        }

        model.Targets = targets;
        model.Audiences = (await _audiences.ListAsync(HttpContext.RequestAborted))
            .Select(audience => new SelectListItem(audience.DisplayName, audience.Name, string.Equals(audience.Name, model.AudienceName, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}
