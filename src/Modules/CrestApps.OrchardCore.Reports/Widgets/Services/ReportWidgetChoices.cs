using System.Globalization;
using CrestApps.OrchardCore.Reports.Widgets.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Reports.Widgets.Services;

/// <summary>
/// The choices every report dashboard widget offers: the period, what to show, the rows per table and how long to keep
/// the result. The widget of a registered report and the widget of a report built in the report builder share them.
/// </summary>
public static class ReportWidgetChoices
{
    /// <summary>
    /// The most rows of each table a widget shows.
    /// </summary>
    public const int MaxRows = 100;

    /// <summary>
    /// The most minutes a widget keeps what it shows: a day.
    /// </summary>
    public const int MaxCacheMinutes = 24 * 60;

    /// <summary>
    /// Lists the periods a widget can cover.
    /// </summary>
    /// <param name="S">The localizer of the widget's driver.</param>
    /// <returns>The periods.</returns>
    public static IList<SelectListItem> Periods(IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(S);

        return
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
    }

    /// <summary>
    /// Lists what of a report a widget can show.
    /// </summary>
    /// <param name="S">The localizer of the widget's driver.</param>
    /// <returns>The choices.</returns>
    public static IList<SelectListItem> Contents(IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(S);

        return
        [
            new(S["Figures and tables"], nameof(ReportWidgetContent.FiguresAndTables)),
            new(S["Figures only"], nameof(ReportWidgetContent.FiguresOnly)),
            new(S["Tables only"], nameof(ReportWidgetContent.TablesOnly)),
            new(S["Charts only"], nameof(ReportWidgetContent.ChartsOnly)),
            new(S["Everything"], nameof(ReportWidgetContent.Everything)),
        ];
    }

    /// <summary>
    /// Describes the period a widget covers, in words.
    /// </summary>
    /// <param name="S">The localizer of the widget's driver.</param>
    /// <param name="period">The period.</param>
    /// <param name="from">The first moment of a custom period, in the site's time zone.</param>
    /// <param name="to">The last moment of a custom period, in the site's time zone.</param>
    /// <returns>The words.</returns>
    public static string DescribePeriod(IStringLocalizer S, ReportWidgetPeriod period, DateTime? from, DateTime? to)
    {
        ArgumentNullException.ThrowIfNull(S);

        return period switch
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

    /// <summary>
    /// Checks the choices posted from a widget's editor: an unknown period or content falls back to the defaults, and a
    /// custom period without a day, or rows or minutes out of range, are reported.
    /// </summary>
    /// <param name="model">The posted choices; an unknown period or content is replaced by the default.</param>
    /// <param name="updater">The updater whose model state gets the errors.</param>
    /// <param name="prefix">The prefix of the editor's fields.</param>
    /// <param name="S">The localizer of the widget's driver.</param>
    /// <param name="defaultPeriod">The period to use when the posted one is unknown.</param>
    /// <param name="defaultContent">What to show when the posted choice is unknown.</param>
    public static void Validate(
        ReportWidgetOptionsViewModel model,
        IUpdateModel updater,
        string prefix,
        IStringLocalizer S,
        ReportWidgetPeriod defaultPeriod,
        ReportWidgetContent defaultContent)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(updater);
        ArgumentNullException.ThrowIfNull(S);

        if (!Enum.IsDefined(model.Period))
        {
            model.Period = defaultPeriod;
        }

        if (!Enum.IsDefined(model.Content))
        {
            model.Content = defaultContent;
        }

        if (model.Period == ReportWidgetPeriod.Custom && model.From is null && model.To is null)
        {
            updater.ModelState.AddModelError(prefix, nameof(model.From), S["Give at least the first or the last day of the period."]);
        }

        if (model.MaxRows is < 0 or > MaxRows)
        {
            updater.ModelState.AddModelError(prefix, nameof(model.MaxRows), S["Show between 0 (all) and 100 rows."]);
        }

        if (model.CacheMinutes is < 0 or > MaxCacheMinutes)
        {
            updater.ModelState.AddModelError(prefix, nameof(model.CacheMinutes), S["Keep the result between 0 (don't) and 1440 minutes (a day)."]);
        }
    }
}
