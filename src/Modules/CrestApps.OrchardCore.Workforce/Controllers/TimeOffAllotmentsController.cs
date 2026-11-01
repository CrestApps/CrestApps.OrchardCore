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
/// The time-off allotments: the list and the editor.
/// </summary>
[Feature(WorkforceConstants.Features.TimeOff)]
public sealed class TimeOffAllotmentsController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ITimeOffAllotmentManager _allotmentManager;
    private readonly ITimeOffTypeManager _typeManager;
    private readonly ITeamManager _teamManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeOffAllotmentsController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="allotmentManager">The manager.</param>
    /// <param name="typeManager">The time-off type manager.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimeOffAllotmentsController(
        IAuthorizationService authorizationService,
        ITimeOffAllotmentManager allotmentManager,
        ITimeOffTypeManager typeManager,
        ITeamManager teamManager,
        INotifier notifier,
        IHtmlLocalizer<TimeOffAllotmentsController> htmlLocalizer,
        IStringLocalizer<TimeOffAllotmentsController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _allotmentManager = allotmentManager;
        _typeManager = typeManager;
        _teamManager = teamManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the time-off allotments.
    /// </summary>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The list.</returns>
    [Admin("workforce/time-off-allotments", "WorkforceTimeOffAllotments")]
    public async Task<IActionResult> Index(PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeOffPolicies))
        {
            return Forbid();
        }

        var items = (await _allotmentManager.GetAllAsync(HttpContext.RequestAborted))
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var pager = new Pager(pagerParameters, pagerOptions.Value);

        return View(new WorkforceListViewModel
        {
            List = new AdminListContext<TimeOffAllotment>
            {
                Source = items.Skip(pager.GetStartIndex()).Take(pager.PageSize).ToList(),
                Pager = await shapeFactory.PagerAsync(pager, items.Count),
            },
        });
    }

    /// <summary>
    /// Shows the editor of new or existing time-off allotments.
    /// </summary>
    /// <param name="id">The id, or empty for new.</param>
    /// <returns>The editor.</returns>
    [Admin("workforce/time-off-allotments/edit/{id?}", "WorkforceTimeOffAllotmentsEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeOffPolicies))
        {
            return Forbid();
        }

        var item = string.IsNullOrEmpty(id) ? await _allotmentManager.NewAsync() : await _allotmentManager.FindByIdAsync(id);

        if (item is null)
        {
            return NotFound();
        }

        var model = new TimeOffAllotmentEditViewModel
        {
            Id = id,
            Name = item.Name,
            TeamId = item.TeamId,
            TypeIds = item.TypeIds.ToArray(),
            Slots = Enumerable.Range(0, 7).Select(index => item.SlotsOn(_monday.AddDays(index))).ToArray(),
            Dates = string.Join(Environment.NewLine, item.Dates.OrderBy(date => date.Date).Select(date => string.Create(CultureInfo.InvariantCulture, $"{date.Date:yyyy-MM-dd} = {date.Slots}"))),
            AutoApprove = item.AutoApprove,
            WhenFull = item.WhenFull.ToString(),
            Enabled = item.Enabled,
        };

        await PopulateAsync(model);

        return View(model);
    }

    /// <summary>
    /// Saves new or existing time-off allotments.
    /// </summary>
    /// <param name="model">The fields.</param>
    /// <returns>A redirect to the list, or the editor with errors.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("workforce/time-off-allotments/edit/{id?}", "WorkforceTimeOffAllotmentsEdit")]
    public async Task<IActionResult> EditPost(TimeOffAllotmentEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeOffPolicies))
        {
            return Forbid();
        }

        var isNew = string.IsNullOrEmpty(model.Id);
        var item = isNew ? await _allotmentManager.NewAsync() : await _allotmentManager.FindByIdAsync(model.Id);

        if (item is null)
        {
            return NotFound();
        }

        var dates = new List<TimeOffAllotmentDate>();

        foreach (var line in (model.Dates ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);

            if (parts.Length == 2
                && DateOnly.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var slots))
            {
                dates.Add(new TimeOffAllotmentDate { Date = date, Slots = slots });
            }
            else
            {
                ModelState.AddModelError(nameof(model.Dates), S["This date isn't understood: {0}. Write the date, an equals sign and the number of people, such as 2026-12-24 = 1.", line]);
            }
        }

        var slotsByMonday = (model.Slots ?? []).Concat(Enumerable.Repeat(0, 7)).Take(7).ToArray();

        item.Name = model.Name;
        item.TeamId = model.TeamId;
        item.TypeIds = (model.TypeIds ?? []).Where(typeId => !string.IsNullOrEmpty(typeId)).Distinct(StringComparer.Ordinal).ToList();

        // The form lists Monday first; the allotment stores Sunday first, as DayOfWeek does.
        item.WeekdaySlots = Enumerable.Range(0, 7).Select(day => slotsByMonday[(day + 6) % 7]).ToList();
        item.Dates = dates;
        item.AutoApprove = model.AutoApprove;
        item.WhenFull = Enum.TryParse<TimeOffAllotmentFullAction>(model.WhenFull, out var whenFull) ? whenFull : TimeOffAllotmentFullAction.Waitlist;
        item.Enabled = model.Enabled;

        if (ModelState.IsValid)
        {
            var validation = await _allotmentManager.ValidateAsync(item);

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
            await _allotmentManager.CreateAsync(item);
        }
        else
        {
            await _allotmentManager.UpdateAsync(item);
        }

        await _notifier.SuccessAsync(H["The allotment has been saved."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes time-off allotments.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    [Admin("workforce/time-off-allotments/delete/{id}", "WorkforceTimeOffAllotmentsDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ManageTimeOffPolicies))
        {
            return Forbid();
        }

        var item = await _allotmentManager.FindByIdAsync(id);

        if (item is not null)
        {
            await _allotmentManager.DeleteAsync(item);
            await _notifier.SuccessAsync(H["The allotment has been deleted."]);
        }

        return RedirectToAction(nameof(Index));
    }

    // Any Monday, to read the weekday numbers in Monday-first order.
    private static readonly DateOnly _monday = new(2026, 1, 5);

    private async Task PopulateAsync(TimeOffAllotmentEditViewModel model)
    {
        var selectedTypes = new HashSet<string>(model.TypeIds ?? [], StringComparer.Ordinal);
        var selectedTeams = new HashSet<string>([model.TeamId ?? string.Empty], StringComparer.Ordinal);

        model.TypeOptions = (await _typeManager.GetAllAsync(HttpContext.RequestAborted))
            .OrderBy(type => type.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(type => new SelectListItem(type.Name, type.ItemId, selectedTypes.Contains(type.ItemId)))
            .ToList();
        model.TeamOptions = (await _teamManager.ListTreeAsync(HttpContext.RequestAborted))
            .Select(item => new SelectListItem(new string(' ', item.Depth * 4) + item.Team.Name, item.Team.ItemId, selectedTeams.Contains(item.Team.ItemId)))
            .ToList();
    }
}
