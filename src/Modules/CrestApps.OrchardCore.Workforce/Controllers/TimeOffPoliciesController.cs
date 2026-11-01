using System.Globalization;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.Controllers;

/// <summary>
/// The time-off policies: the list and the editor.
/// </summary>
[Feature(WorkforceConstants.Features.TimeOff)]
public sealed class TimeOffPoliciesController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ITimeOffPolicyManager _policyManager;
    private readonly ITimeOffTypeManager _typeManager;
    private readonly ITeamManager _teamManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeOffPoliciesController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="policyManager">The manager.</param>
    /// <param name="typeManager">The time-off type manager.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimeOffPoliciesController(
        IAuthorizationService authorizationService,
        ITimeOffPolicyManager policyManager,
        ITimeOffTypeManager typeManager,
        ITeamManager teamManager,
        INotifier notifier,
        IHtmlLocalizer<TimeOffPoliciesController> htmlLocalizer,
        IStringLocalizer<TimeOffPoliciesController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _policyManager = policyManager;
        _typeManager = typeManager;
        _teamManager = teamManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the time-off policies.
    /// </summary>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("workforce/time-off-policies", "WorkforceTimeOffPolicies")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeOffPolicies))
        {
            return Forbid();
        }

        var items = (await _policyManager.GetAllAsync(HttpContext.RequestAborted))
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var pager = new Pager(pagerParameters, pagerOptions.Value);

        return View(new WorkforceListViewModel
        {
            List = new AdminListContext<TimeOffPolicy>
            {
                Source = items.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList(),
                Pager = await shapeFactory.PagerAsync(pager, items.Count),
            },
        });
    }

    /// <summary>
    /// Shows the editor of new or existing time-off policies.
    /// </summary>
    /// <param name="id">The id, or empty for new.</param>
    /// <returns>The editor.</returns>
    [Admin("workforce/time-off-policies/edit/{id?}", "WorkforceTimeOffPoliciesEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeOffPolicies))
        {
            return Forbid();
        }

        var item = string.IsNullOrEmpty(id) ? await _policyManager.NewAsync() : await _policyManager.FindByIdAsync(id);

        if (item is null)
        {
            return NotFound();
        }

        var teamAssignments = item.Assignments.Where(assignment => !string.IsNullOrEmpty(assignment.TeamId)).ToArray();
        var model = new TimeOffPolicyEditViewModel
        {
            Id = id,
            Name = item.Name,
            TypeId = item.TypeId,
            AccrualMethod = item.AccrualMethod.ToString(),
            Frequency = item.Frequency.ToString(),
            AnchorDate = item.AnchorDate,
            Rate = item.Rate,
            TenureTiers = string.Join(Environment.NewLine, item.TenureTiers.OrderBy(tier => tier.FromMonths).Select(tier => string.Create(CultureInfo.InvariantCulture, $"{tier.FromMonths} = {tier.Rate:0.####}"))),
            WaitingPeriodDays = item.WaitingPeriodDays,
            MaxBalance = item.MaxBalance,
            CarryoverMax = item.CarryoverMax,
            ResetMonth = item.ResetMonth,
            ResetDay = item.ResetDay,
            NegativeBalanceLimit = item.NegativeBalanceLimit,
            TeamIds = teamAssignments.Select(assignment => assignment.TeamId).Distinct(StringComparer.Ordinal).ToArray(),
            EffectiveFrom = teamAssignments.FirstOrDefault()?.EffectiveFrom,
            EffectiveTo = teamAssignments.FirstOrDefault()?.EffectiveTo,
            Enabled = item.Enabled,
            WorkerAssignments = item.Assignments.Count(assignment => !string.IsNullOrEmpty(assignment.WorkerId)),
        };

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Saves new or existing time-off policies.
    /// </summary>
    /// <param name="model">The fields.</param>
    /// <returns>A redirect to the list, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("workforce/time-off-policies/edit/{id?}", "WorkforceTimeOffPoliciesEdit")]
    public async Task<IActionResult> EditPost(TimeOffPolicyEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeOffPolicies))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(model.Id);
        var item = isNew ? await _policyManager.NewAsync() : await _policyManager.FindByIdAsync(model.Id);

        if (item is null)
        {
            return NotFound();
        }

        var tiers = new List<TimeOffTenureTier>();

        foreach (var line in (model.TenureTiers ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);

            if (parts.Length == 2
                && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var months)
                && decimal.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var rate))
            {
                tiers.Add(new TimeOffTenureTier { FromMonths = months, Rate = rate });
            }
            else
            {
                ModelState.AddModelError(nameof(model.TenureTiers), S["This rate isn't understood: {0}. Write the months of service, an equals sign and the rate, such as 12 = 10.", line]);
            }
        }

        item.Name = model.Name;
        item.TypeId = model.TypeId;
        item.AccrualMethod = Enum.TryParse<TimeOffAccrualMethod>(model.AccrualMethod, out var method) ? method : TimeOffAccrualMethod.None;
        item.Frequency = Enum.TryParse<TimeOffAccrualFrequency>(model.Frequency, out var frequency) ? frequency : TimeOffAccrualFrequency.Monthly;
        item.AnchorDate = model.AnchorDate;
        item.Rate = model.Rate;
        item.TenureTiers = tiers;
        item.WaitingPeriodDays = model.WaitingPeriodDays;
        item.MaxBalance = model.MaxBalance;
        item.CarryoverMax = model.CarryoverMax;
        item.ResetMonth = model.ResetMonth;
        item.ResetDay = model.ResetDay;
        item.NegativeBalanceLimit = model.NegativeBalanceLimit;
        item.Enabled = model.Enabled;

        // Team assignments are edited here; workers assigned one by one keep theirs.
        item.Assignments = item.Assignments
            .Where(assignment => !string.IsNullOrEmpty(assignment.WorkerId))
            .Concat((model.TeamIds ?? []).Where(teamId => !string.IsNullOrEmpty(teamId)).Distinct(StringComparer.Ordinal)
                .Select(teamId => new TimeOffPolicyAssignment { TeamId = teamId, EffectiveFrom = model.EffectiveFrom, EffectiveTo = model.EffectiveTo }))
            .ToList();

        if (ModelState.IsValid)
        {
            var validation = await _policyManager.ValidateAsync(item);

            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(error.MemberNames.FirstOrDefault() ?? string.Empty, error.ErrorMessage);
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateAsync(model);

            return View(model);
        }

        if (isNew)
        {
            await _policyManager.CreateAsync(item);
        }
        else
        {
            await _policyManager.UpdateAsync(item);
        }

        await _notifier.SuccessAsync(H["The time-off policy has been saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes time-off policies.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("workforce/time-off-policies/delete/{id}", "WorkforceTimeOffPoliciesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeOffPolicies))
        {
            return Forbid();
        }

        var item = await _policyManager.FindByIdAsync(id);

        if (item is not null)
        {
            await _policyManager.DeleteAsync(item);
            await _notifier.SuccessAsync(H["The time-off policy has been deleted."]);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAsync(TimeOffPolicyEditViewModel model)
    {
        var selectedTypes = new HashSet<string>([model.TypeId ?? string.Empty], StringComparer.Ordinal);
        var selectedTeams = new HashSet<string>(model.TeamIds ?? [], StringComparer.Ordinal);

        model.TypeOptions = (await _typeManager.GetAllAsync(HttpContext.RequestAborted))
            .OrderBy(type => type.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(type => new SelectListItem(type.Name, type.ItemId, selectedTypes.Contains(type.ItemId)))
            .ToList();
        model.TeamOptions = (await _teamManager.ListTreeAsync(HttpContext.RequestAborted))
            .Select(item => new SelectListItem(new string(' ', item.Depth * 4) + item.Team.Name, item.Team.ItemId, selectedTeams.Contains(item.Team.ItemId)))
            .ToList();
    }
}
