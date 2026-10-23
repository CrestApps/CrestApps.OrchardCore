using System.Globalization;
using CrestApps.OrchardCore.Reports.Services;
using CrestApps.OrchardCore.Reports.Widgets.Models;
using CrestApps.OrchardCore.Reports.Widgets.Services;
using CrestApps.OrchardCore.Reports.Widgets.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Reports.Widgets.Drivers;

/// <summary>
/// Shows a report on the admin dashboard and lets the widget choose the report, its period, what of it to show and how
/// long to keep the result.
/// </summary>
public sealed class ReportWidgetPartDisplayDriver : ContentPartDisplayDriver<ReportWidgetPart>
{
    private readonly ReportWidgetService _service;
    private readonly IReportManager _reportManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    public ReportWidgetPartDisplayDriver(
        ReportWidgetService service,
        IReportManager reportManager,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizerFactory localizerFactory,
        ILocalClock localClock,
        IStringLocalizer<ReportWidgetPartDisplayDriver> stringLocalizer)
    {
        _service = service;
        _reportManager = reportManager;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        _localizerFactory = localizerFactory;
        _localClock = localClock;
        S = stringLocalizer;
    }

    public override IDisplayResult Display(ReportWidgetPart part, BuildPartDisplayContext context)
    {
        var settings = context.TypePartDefinition.GetSettings<ReportWidgetPartSettings>();

        return Initialize<ReportWidgetViewModel>(GetDisplayShapeType(context), async model =>
        {
            var user = _httpContextAccessor.HttpContext.User;
            var result = await _service.GetAsync(part, settings, user, _httpContextAccessor.HttpContext.RequestAborted);

            model.Result = result;
            model.Outcome = result.Outcome;

            if (result.Report is null)
            {
                return;
            }

            model.ReportName = result.Report.Name;
            model.ReportDisplayName = _localizerFactory.Localize(result.Report.DisplayName).Value;
            model.CanOpenReport = await _authorizationService.AuthorizeAsync(user, result.Report.Permission);

            if (result.Outcome != ReportWidgetOutcome.Shown)
            {
                return;
            }

            var period = part.Period ?? settings.DefaultPeriod;
            model.PeriodText = DescribePeriod(period, result.From, result.To);

            var generated = await _localClock.ConvertToLocalAsync(result.GeneratedUtc);
            model.UpdatedText = S["Updated {0}", generated.DateTime.ToString("g", CultureInfo.CurrentCulture)].Value;
        }).Location("DetailAdmin", "Content:5").Location("Detail", "Content:5");
    }

    public override IDisplayResult Edit(ReportWidgetPart part, BuildPartEditorContext context)
    {
        var settings = context.TypePartDefinition.GetSettings<ReportWidgetPartSettings>();

        return Initialize<ReportWidgetEditViewModel>(GetEditorShapeType(context), async model =>
        {
            var fixedReport = string.IsNullOrEmpty(settings.ReportName) ? null : _reportManager.GetDescriptor(settings.ReportName);

            model.ReportIsFixed = fixedReport is not null;
            model.FixedReportDisplayName = fixedReport is null ? null : _localizerFactory.Localize(fixedReport.DisplayName).Value;
            model.ReportName = part.ReportName;
            model.Period = part.Period ?? settings.DefaultPeriod;
            model.From = part.From;
            model.To = part.To;
            model.Content = part.Show ?? settings.DefaultContent;
            model.MaxRows = part.MaxRows ?? settings.DefaultMaxRows;
            model.CacheMinutes = part.CacheMinutes;
            model.Periods = BuildPeriods();
            model.Contents = BuildContents();
            model.Reports = fixedReport is null ? await BuildReportsAsync() : [];
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(ReportWidgetPart part, UpdatePartEditorContext context)
    {
        var settings = context.TypePartDefinition.GetSettings<ReportWidgetPartSettings>();
        var model = new ReportWidgetEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrEmpty(settings.ReportName))
        {
            if (string.IsNullOrEmpty(model.ReportName) || _reportManager.GetDescriptor(model.ReportName) is null)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ReportName), S["Choose the report the widget shows."]);
            }

            part.ReportName = model.ReportName;
        }

        if (!Enum.IsDefined(model.Period))
        {
            model.Period = settings.DefaultPeriod;
        }

        if (model.Period == ReportWidgetPeriod.Custom && model.From is null && model.To is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.From), S["Give at least the first or the last day of the period."]);
        }

        if (model.MaxRows is < 0 or > 100)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MaxRows), S["Show between 0 (all) and 100 rows."]);
        }

        if (model.CacheMinutes is < 0 or > 24 * 60)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.CacheMinutes), S["Keep the result between 0 (don't) and 1440 minutes (a day)."]);
        }

        part.Period = model.Period;
        part.From = model.Period == ReportWidgetPeriod.Custom ? model.From : null;
        part.To = model.Period == ReportWidgetPeriod.Custom ? model.To : null;
        part.Show = Enum.IsDefined(model.Content) ? model.Content : settings.DefaultContent;
        part.MaxRows = Math.Clamp(model.MaxRows, 0, 100);
        part.CacheMinutes = Math.Clamp(model.CacheMinutes, 0, 24 * 60);

        return await EditAsync(part, context);
    }

    private IList<SelectListItem> BuildPeriods()
        =>
        [
            new(S["Today"], nameof(ReportWidgetPeriod.Today)),
            new(S["Yesterday"], nameof(ReportWidgetPeriod.Yesterday)),
            new(S["Last 7 days"], nameof(ReportWidgetPeriod.Last7Days)),
            new(S["Last 30 days"], nameof(ReportWidgetPeriod.Last30Days)),
            new(S["Last 90 days"], nameof(ReportWidgetPeriod.Last90Days)),
            new(S["This month"], nameof(ReportWidgetPeriod.ThisMonth)),
            new(S["Last month"], nameof(ReportWidgetPeriod.LastMonth)),
            new(S["This year"], nameof(ReportWidgetPeriod.ThisYear)),
            new(S["Last 12 months"], nameof(ReportWidgetPeriod.Last12Months)),
            new(S["All time"], nameof(ReportWidgetPeriod.AllTime)),
            new(S["Custom dates"], nameof(ReportWidgetPeriod.Custom)),
        ];

    private IList<SelectListItem> BuildContents()
        =>
        [
            new(S["Figures and tables"], nameof(ReportWidgetContent.FiguresAndTables)),
            new(S["Figures only"], nameof(ReportWidgetContent.FiguresOnly)),
            new(S["Tables only"], nameof(ReportWidgetContent.TablesOnly)),
            new(S["Charts only"], nameof(ReportWidgetContent.ChartsOnly)),
            new(S["Everything"], nameof(ReportWidgetContent.Everything)),
        ];

    private async Task<IList<SelectListItem>> BuildReportsAsync()
    {
        var user = _httpContextAccessor.HttpContext.User;
        var items = new List<SelectListItem>();

        foreach (var report in _reportManager.GetReports()
            .OrderBy(report => report.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(report => _localizerFactory.Localize(report.DisplayName).Value, StringComparer.CurrentCultureIgnoreCase))
        {
            if (!await _authorizationService.AuthorizeAsync(user, report.Permission))
            {
                continue;
            }

            var name = _localizerFactory.Localize(report.DisplayName).Value;

            items.Add(new SelectListItem(string.IsNullOrEmpty(report.Category) ? name : $"{report.Category}: {name}", report.Name));
        }

        return items;
    }

    private string DescribePeriod(ReportWidgetPeriod period, DateTime? from, DateTime? to)
        => period switch
        {
            ReportWidgetPeriod.Today => S["Today"].Value,
            ReportWidgetPeriod.Yesterday => S["Yesterday"].Value,
            ReportWidgetPeriod.Last7Days => S["Last 7 days"].Value,
            ReportWidgetPeriod.Last30Days => S["Last 30 days"].Value,
            ReportWidgetPeriod.Last90Days => S["Last 90 days"].Value,
            ReportWidgetPeriod.ThisMonth => S["This month"].Value,
            ReportWidgetPeriod.LastMonth => S["Last month"].Value,
            ReportWidgetPeriod.ThisYear => S["This year"].Value,
            ReportWidgetPeriod.Last12Months => S["Last 12 months"].Value,
            ReportWidgetPeriod.AllTime => S["All time"].Value,
            _ => from.HasValue && to.HasValue
                ? S["{0} to {1}", from.Value.ToString("d", CultureInfo.CurrentCulture), to.Value.ToString("d", CultureInfo.CurrentCulture)].Value
                : from.HasValue
                    ? S["Since {0}", from.Value.ToString("d", CultureInfo.CurrentCulture)].Value
                    : S["Until {0}", (to ?? DateTime.MinValue).ToString("d", CultureInfo.CurrentCulture)].Value,
        };
}
