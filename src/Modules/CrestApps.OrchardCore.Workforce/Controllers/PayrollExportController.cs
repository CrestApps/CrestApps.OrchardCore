using System.Globalization;
using System.Text;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Portal;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.AspNetCore.Authorization;
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
using OrchardCore.Routing;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.Controllers;

/// <summary>
/// Payroll export: runs exports of a pay period's locked timesheets, keeps their files, and sets the CSV columns.
/// </summary>
[Feature(WorkforceConstants.Features.TimeClock)]
public sealed class PayrollExportController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IPayrollExportService _exports;
    private readonly IEnumerable<IPayrollExportProvider> _providers;
    private readonly ITimesheetService _timesheets;
    private readonly IPayPeriodCalendarManager _calendars;
    private readonly ILocalClock _localClock;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayrollExportController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="exports">The payroll exports.</param>
    /// <param name="providers">The export providers.</param>
    /// <param name="timesheets">The timesheets.</param>
    /// <param name="calendars">The pay periods.</param>
    /// <param name="localClock">The local clock.</param>
    /// <param name="notifier">The notifier.</param>
    /// <param name="htmlLocalizer">The HTML localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PayrollExportController(
        IAuthorizationService authorizationService,
        IPayrollExportService exports,
        IEnumerable<IPayrollExportProvider> providers,
        ITimesheetService timesheets,
        IPayPeriodCalendarManager calendars,
        ILocalClock localClock,
        INotifier notifier,
        IHtmlLocalizer<PayrollExportController> htmlLocalizer,
        IStringLocalizer<PayrollExportController> stringLocalizer)
    {
        _authorizationService = authorizationService;
        _exports = exports;
        _providers = providers;
        _timesheets = timesheets;
        _calendars = calendars;
        _localClock = localClock;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Shows the export form and the exports made.
    /// </summary>
    /// <param name="model">The period chosen.</param>
    /// <param name="pagerParameters">The pager parameters.</param>
    /// <param name="pagerOptions">The pager options.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    /// <returns>The page.</returns>
    [Admin("workforce/payroll-export", "WorkforcePayrollExport")]
    public async Task<IActionResult> Index(PayrollExportViewModel model, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions, [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ExportPayroll))
        {
            return Forbid();
        }

        var calendars = await _calendars.ListEnabledAsync(HttpContext.RequestAborted);
        var calendar = calendars.FirstOrDefault(item => item.ItemId == model.CalendarId) ?? (calendars.Count > 0 ? calendars[0] : null);
        var today = await PlanningOptions.TodayAsync(_localClock);

        model.CalendarId = calendar?.ItemId;
        model.CalendarOptions = calendars.Select(item => new SelectListItem(item.Name, item.ItemId, item.ItemId == model.CalendarId)).ToList();
        model.ProviderOptions = _providers.Select(provider => new SelectListItem(provider.DisplayName, provider.Name, provider.Name == model.Provider)).ToList();

        if (calendar is not null)
        {
            // The last period that has ended, unless one is chosen.
            var start = model.PeriodStart.HasValue ? calendar.PeriodOn(model.PeriodStart.Value).Start : calendar.PeriodOn(calendar.PeriodOn(today).Start.AddDays(-1)).Start;

            model.PeriodStart = start;
            model.PeriodOptions = TimesheetsController.PeriodOptions(calendar, today, start);

            foreach (var status in Enum.GetValues<TimesheetStatus>())
            {
                var (count, _) = await _timesheets.PageAsync(new TimesheetQuery { PayPeriodCalendarId = calendar.ItemId, PeriodStart = start, Statuses = [status] }, 0, 0, HttpContext.RequestAborted);

                if (count > 0)
                {
                    model.Counts[status] = count;
                }
            }
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (total, batches) = await _exports.PageAsync(pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        model.List = new AdminListContext<PayrollExportBatch>
        {
            Source = [.. batches],
            Pager = await shapeFactory.PagerAsync(pager, total),
        };

        return View(model);
    }

    /// <summary>
    /// Runs an export.
    /// </summary>
    /// <param name="model">The period and provider.</param>
    /// <returns>A redirect back, with the export to download.</returns>
    [HttpPost]
    [ActionName(nameof(Index))]
    [Admin("workforce/payroll-export", "WorkforcePayrollExport")]
    public async Task<IActionResult> Run(PayrollExportViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ExportPayroll))
        {
            return Forbid();
        }

        var result = model.PeriodStart.HasValue
            ? await _exports.RunAsync(model.Provider ?? _providers.FirstOrDefault()?.Name, model.CalendarId, model.PeriodStart.Value, model.IncludeExported, User.Identity?.Name, HttpContext.RequestAborted)
            : null;

        if (result is null)
        {
            await _notifier.ErrorAsync(H["Choose the pay period."]);
        }
        else if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                await _notifier.ErrorAsync(H["{0}", error]);
            }
        }
        else
        {
            await _notifier.SuccessAsync(H.Plural(result.Value.TimesheetIds.Count, "1 timesheet has been exported. Download the file below.", "{0} timesheets have been exported. Download the file below.", result.Value.TimesheetIds.Count));
        }

        return RedirectToAction(nameof(Index), new { calendarId = model.CalendarId, periodStart = model.PeriodStart?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
    }

    /// <summary>
    /// Downloads an export's file.
    /// </summary>
    /// <param name="id">The export.</param>
    /// <returns>The file.</returns>
    [Admin("workforce/payroll-export/{id}/download", "WorkforcePayrollExportDownload")]
    public async Task<IActionResult> Download(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ExportPayroll))
        {
            return Forbid();
        }

        var batch = await _exports.FindAsync(id, HttpContext.RequestAborted);

        if (batch is null)
        {
            return NotFound();
        }

        return File(Encoding.UTF8.GetBytes(batch.Content ?? string.Empty), batch.ContentType ?? "text/csv", batch.FileName ?? "payroll.csv");
    }

    /// <summary>
    /// Shows the CSV column editor.
    /// </summary>
    /// <returns>The editor.</returns>
    [Admin("workforce/payroll-export/columns", "WorkforcePayrollExportColumns")]
    public async Task<IActionResult> Columns()
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ExportPayroll))
        {
            return Forbid();
        }

        var settings = await _exports.GetCsvSettingsAsync(HttpContext.RequestAborted);
        IEnumerable<PayrollCsvColumn> columns = settings.Columns.Count > 0 ? settings.Columns : PayrollCsvSettings.DefaultColumns;

        return View(new PayrollColumnsViewModel { Columns = [.. columns.Select(column => new PayrollCsvColumn { Header = column.Header, Field = column.Field })] });
    }

    /// <summary>
    /// Saves the CSV columns. Rows with no heading are dropped; none at all puts the built-in columns back.
    /// </summary>
    /// <param name="model">The columns.</param>
    /// <param name="reset">Whether to put the built-in columns back.</param>
    /// <returns>A redirect to the export.</returns>
    [HttpPost]
    [ActionName(nameof(Columns))]
    [Admin("workforce/payroll-export/columns", "WorkforcePayrollExportColumns")]
    public async Task<IActionResult> ColumnsPost(PayrollColumnsViewModel model, bool reset = false)
    {
        if (!await _authorizationService.AuthorizeAsync(User, WorkforcePermissions.ExportPayroll))
        {
            return Forbid();
        }

        var columns = reset
            ? []
            : (model.Columns ?? []).Where(column => !string.IsNullOrWhiteSpace(column.Header)).Select(column => new PayrollCsvColumn { Header = column.Header.Trim(), Field = column.Field }).ToList();

        await _exports.SaveCsvSettingsAsync(new PayrollCsvSettings { Columns = columns }, HttpContext.RequestAborted);
        await _notifier.SuccessAsync(columns.Count == 0 ? H["The built-in columns are used."] : H["The columns have been saved."]);

        return RedirectToAction(nameof(Index));
    }
}
