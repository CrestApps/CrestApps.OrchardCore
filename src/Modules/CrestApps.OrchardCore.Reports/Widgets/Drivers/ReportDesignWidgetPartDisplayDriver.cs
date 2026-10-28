using System.Globalization;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Widgets.Models;
using CrestApps.OrchardCore.Reports.Widgets.Services;
using CrestApps.OrchardCore.Reports.Widgets.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Reports.Widgets.Drivers;

/// <summary>
/// Shows a report built in the report builder, or a reusable view as a table, on the admin dashboard, and lets the
/// widget choose the report or view, its period, what of it to show and how long to keep the result.
/// </summary>
public sealed class ReportDesignWidgetPartDisplayDriver : ContentPartDisplayDriver<ReportDesignWidgetPart>
{
    private const string ReportPrefix = "report:";
    private const string ViewPrefix = "view:";

    private readonly ReportDesignWidgetService _service;
    private readonly ReportDesignService _designService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDesignWidgetPartDisplayDriver"/> class.
    /// </summary>
    /// <param name="service">The service that runs and keeps what the widget shows.</param>
    /// <param name="designService">The store of reports and views built in the report builder.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="httpContextAccessor">The accessor of the current request.</param>
    /// <param name="localClock">The clock of the site's time zone.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ReportDesignWidgetPartDisplayDriver(
        ReportDesignWidgetService service,
        ReportDesignService designService,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        ILocalClock localClock,
        IStringLocalizer<ReportDesignWidgetPartDisplayDriver> stringLocalizer)
    {
        _service = service;
        _designService = designService;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        _localClock = localClock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override IDisplayResult Display(ReportDesignWidgetPart part, BuildPartDisplayContext context)
    {
        return Initialize<ReportDesignWidgetViewModel>(GetDisplayShapeType(context), async model =>
        {
            var user = _httpContextAccessor.HttpContext.User;
            var result = await _service.GetAsync(part, user, _httpContextAccessor.HttpContext.RequestAborted);

            model.Result = result;

            if (result.Outcome != ReportWidgetOutcome.Shown)
            {
                return;
            }

            var period = Enum.IsDefined(part.Period) ? part.Period : ReportDesignWidgetPart.DefaultPeriod;

            model.PeriodText = result.PeriodApplied
                ? ReportWidgetChoices.DescribePeriod(S, period, result.From, result.To)
                : S["Its own dates"].Value;

            var generated = await _localClock.ConvertToLocalAsync(result.GeneratedUtc);
            model.UpdatedText = S["Updated {0}", generated.DateTime.ToString("g", CultureInfo.CurrentCulture)].Value;

            // Whoever sees a report may open its page; a view has no page of its own, only its designer.
            model.CanOpen = !result.IsView || await CanChangeViewAsync(result.ItemId);
        }).Location("DetailAdmin", "Content:5").Location("Detail", "Content:5");
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ReportDesignWidgetPart part, BuildPartEditorContext context)
    {
        return Initialize<ReportDesignWidgetEditViewModel>(GetEditorShapeType(context), async model =>
        {
            model.Source = !string.IsNullOrEmpty(part.ReportId)
                ? ReportPrefix + part.ReportId
                : !string.IsNullOrEmpty(part.ViewId)
                    ? ViewPrefix + part.ViewId
                    : null;
            model.Period = part.Period;
            model.From = part.From;
            model.To = part.To;
            model.Content = part.Show;
            model.MaxRows = part.MaxRows;
            model.CacheMinutes = part.CacheMinutes;
            model.Periods = ReportWidgetChoices.Periods(S);
            model.Contents = ReportWidgetChoices.Contents(S);
            model.Sources = await BuildSourcesAsync();
        });
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ReportDesignWidgetPart part, UpdatePartEditorContext context)
    {
        var model = new ReportDesignWidgetEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var (reportId, viewId) = await ResolveSourceAsync(model.Source);

        if (reportId is null && viewId is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Source), S["Choose a report or a view you may open."]);
        }

        ReportWidgetChoices.Validate(model, context.Updater, Prefix, S, ReportDesignWidgetPart.DefaultPeriod, ReportDesignWidgetPart.DefaultContent);

        part.ReportId = reportId;
        part.ViewId = viewId;
        part.Period = model.Period;
        part.From = model.Period == ReportWidgetPeriod.Custom ? model.From : null;
        part.To = model.Period == ReportWidgetPeriod.Custom ? model.To : null;
        part.Show = model.Content;
        part.MaxRows = Math.Clamp(model.MaxRows, 0, ReportWidgetChoices.MaxRows);
        part.CacheMinutes = Math.Clamp(model.CacheMinutes, 0, ReportWidgetChoices.MaxCacheMinutes);

        return await EditAsync(part, context);
    }

    // The published reports and the views the editor may open: a widget can't be pointed at one they can't see.
    private async Task<IList<SelectListItem>> BuildSourcesAsync()
    {
        var user = _httpContextAccessor.HttpContext.User;
        var items = new List<SelectListItem>();
        var reports = new SelectListGroup { Name = S["Reports"] };
        var views = new SelectListGroup { Name = S["Views (shown as a table)"] };

        var groups = await _designService.GetGroupTreeAsync();

        foreach (var (design, groupPath) in (await _designService.GetAllAsync())
            .Select(design => (design, groups.GetPathText(design.GroupId)))
            .OrderBy(entry => entry.Item2, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(entry => entry.design.DisplayText, StringComparer.CurrentCultureIgnoreCase))
        {
            if (await _authorizationService.AuthorizeAsync(user, ReportDesignerPermissions.ViewAllReportDesigns, design))
            {
                items.Add(new SelectListItem(string.IsNullOrEmpty(groupPath) ? design.DisplayText : $"{groupPath}: {design.DisplayText}", ReportPrefix + design.ItemId)
                {
                    Group = reports,
                });
            }
        }

        foreach (var view in await _designService.GetAllViewsAsync())
        {
            if (await _authorizationService.AuthorizeAsync(user, ReportDesignerPermissions.ViewAllReportDesigns, view))
            {
                items.Add(new SelectListItem(view.DisplayText, ViewPrefix + view.ItemId)
                {
                    Group = views,
                });
            }
        }

        return items;
    }

    // Reads the chosen report or view, which must exist and be one the editor may open.
    private async Task<(string ReportId, string ViewId)> ResolveSourceAsync(string source)
    {
        var user = _httpContextAccessor.HttpContext.User;

        if (source?.StartsWith(ReportPrefix, StringComparison.Ordinal) == true)
        {
            var design = await _designService.FindAsync(source[ReportPrefix.Length..]);

            if (design is not null && await _authorizationService.AuthorizeAsync(user, ReportDesignerPermissions.ViewAllReportDesigns, design))
            {
                return (design.ItemId, null);
            }
        }
        else if (source?.StartsWith(ViewPrefix, StringComparison.Ordinal) == true)
        {
            var view = await _designService.FindViewAsync(source[ViewPrefix.Length..]);

            if (view is not null && await _authorizationService.AuthorizeAsync(user, ReportDesignerPermissions.ViewAllReportDesigns, view))
            {
                return (null, view.ItemId);
            }
        }

        return (null, null);
    }

    private async Task<bool> CanChangeViewAsync(string viewId)
    {
        var view = await _designService.FindViewAsync(viewId);

        return view is not null &&
            await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, ReportDesignerPermissions.ManageAllReportDesigns, view);
    }
}
