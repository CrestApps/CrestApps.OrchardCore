using System.Globalization;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Surveys.Reports;

/// <summary>
/// The filter of the survey reports: the survey, its versions, a collector, a context kind and test responses, plus a
/// segment, a trend or an audience for the reports that use them. A link may preselect them with
/// <c>?ReportFilter.SurveyId={id}</c>.
/// </summary>
internal sealed class SurveyReportFilterDisplayDriver : DisplayDriver<ReportFilter>
{
    private readonly SurveyReportData _data;
    private readonly SurveyVersionStore _versions;
    private readonly SurveyCollectorStore _collectors;
    private readonly ISurveyControlRegistry _registry;
    private readonly SurveyContextKindOptions _contextKinds;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;
    private readonly IStringLocalizerFactory _localizerFactory;

    internal readonly IStringLocalizer S;

    public SurveyReportFilterDisplayDriver(
        SurveyReportData data,
        SurveyVersionStore versions,
        SurveyCollectorStore collectors,
        ISurveyControlRegistry registry,
        IOptions<SurveyContextKindOptions> contextKinds,
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider,
        IStringLocalizerFactory localizerFactory,
        IStringLocalizer<SurveyReportFilterDisplayDriver> stringLocalizer)
    {
        _data = data;
        _versions = versions;
        _collectors = collectors;
        _registry = registry;
        _contextKinds = contextKinds.Value;
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider;
        _localizerFactory = localizerFactory;
        S = stringLocalizer;
        Prefix = SurveyReportKeys.Prefix;
    }

    public override IDisplayResult Edit(ReportFilter filter, BuildEditorContext context)
    {
        if (!SurveyReportNames.IsSurveyReport(filter.ReportName))
        {
            return null;
        }

        return Initialize<SurveyReportFilterViewModel>("SurveyReportFilter_Edit", async model =>
        {
            var cancellationToken = _httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
            var surveys = await _data.ListReadableAsync(_httpContextAccessor.HttpContext?.User, cancellationToken);

            model.ReportName = filter.ReportName;
            model.SurveyId = filter.GetOrDefault<string>(SurveyReportKeys.SurveyId);
            model.Version = filter.GetOrDefault<string>(SurveyReportKeys.Version) ?? SurveyReportKeys.PublishedVersion;
            model.CollectorId = filter.GetOrDefault<string>(SurveyReportKeys.CollectorId);
            model.ContextKind = filter.GetOrDefault<string>(SurveyReportKeys.ContextKind);
            model.IncludeTest = filter.GetOrDefault<bool>(SurveyReportKeys.IncludeTest);
            model.SegmentQuestionId = filter.GetOrDefault<string>(SurveyReportKeys.SegmentQuestionId);
            model.TrendQuestionId = filter.GetOrDefault<string>(SurveyReportKeys.TrendQuestionId);
            model.TrendPeriod = filter.GetOrDefault<string>(SurveyReportKeys.TrendPeriod) ?? "week";
            model.AudienceId = filter.GetOrDefault<string>(SurveyReportKeys.AudienceId);
            model.Status = filter.GetOrDefault<string>(SurveyReportKeys.Status) ?? SurveyReportKeys.CompletedStatus;
            model.Page = filter.GetOrDefault<int?>(SurveyReportKeys.Page);
            model.ShowStatus = filter.ReportName == SurveyReportNames.Responses;
            model.Statuses =
            [
                new SelectListItem(S["Completed"], SurveyReportKeys.CompletedStatus, model.Status == SurveyReportKeys.CompletedStatus),
                new SelectListItem(S["Completed or screened out"], SurveyReportKeys.FinishedStatus, model.Status == SurveyReportKeys.FinishedStatus),
                new SelectListItem(S["In progress"], nameof(Models.SurveyResponseStatus.InProgress), model.Status == nameof(Models.SurveyResponseStatus.InProgress)),
                new SelectListItem(S["Screened out"], nameof(Models.SurveyResponseStatus.ScreenedOut), model.Status == nameof(Models.SurveyResponseStatus.ScreenedOut)),
                new SelectListItem(S["Abandoned"], nameof(Models.SurveyResponseStatus.Abandoned), model.Status == nameof(Models.SurveyResponseStatus.Abandoned)),
                new SelectListItem(S["Voided"], nameof(Models.SurveyResponseStatus.Voided), model.Status == nameof(Models.SurveyResponseStatus.Voided)),
                new SelectListItem(S["Every status"], SurveyReportKeys.AllStatuses, model.Status == SurveyReportKeys.AllStatuses),
            ];
            model.ShowSegment = filter.ReportName == SurveyReportNames.Results;
            model.ShowTrend = filter.ReportName == SurveyReportNames.Trend;
            model.ShowAudience = filter.ReportName == SurveyReportNames.Participation;
            model.Surveys = [.. surveys.Select(survey => new SelectListItem(survey.Title ?? survey.Name, survey.ItemId, survey.ItemId == model.SurveyId))];
            model.TrendPeriods =
            [
                new SelectListItem(S["By day"], "day", model.TrendPeriod == "day"),
                new SelectListItem(S["By week"], "week", model.TrendPeriod == "week"),
                new SelectListItem(S["By month"], "month", model.TrendPeriod == "month"),
            ];
            model.ContextKinds = [.. _contextKinds.Kinds.Values.Select(kind => new SelectListItem(kind.DisplayName is null ? kind.Kind : _localizerFactory.Localize(kind.DisplayName).Value, kind.Kind, kind.Kind == model.ContextKind))];

            var survey = surveys.FirstOrDefault(candidate => candidate.ItemId == model.SurveyId);

            if (survey is not null)
            {
                var versions = await _versions.ListBySurveyAsync(survey.ItemId, cancellationToken);

                model.Versions =
                [
                    new SelectListItem(S["Published version"], SurveyReportKeys.PublishedVersion, model.Version == SurveyReportKeys.PublishedVersion),
                    new SelectListItem(S["All versions"], SurveyReportKeys.AllVersions, model.Version == SurveyReportKeys.AllVersions),
                    .. versions.Select(version =>
                    {
                        var number = version.Number.ToString(CultureInfo.InvariantCulture);

                        return new SelectListItem(S["Version {0}", version.Number], number, model.Version == number);
                    }),
                ];
                model.Collectors = [.. (await _collectors.ListBySurveyAsync(survey.ItemId, cancellationToken)).Select(collector => new SelectListItem(collector.Name, collector.ItemId, collector.ItemId == model.CollectorId))];

                var definition = versions.FirstOrDefault(version => version.Number == survey.PublishedVersionNumber)?.Definition ?? (versions.Count > 0 ? versions[0].Definition : null);

                foreach (var (_, question) in SurveyDefinitionQuestions.All(definition))
                {
                    var descriptor = _registry.GetControl(question.ControlType);
                    var title = question.Title ?? question.Name;

                    if (descriptor?.Has(SurveyControlCapabilities.UsesChoices) == true && question.Rows.Count == 0)
                    {
                        model.SegmentQuestions.Add(new SelectListItem(title, question.Id, question.Id == model.SegmentQuestionId));
                    }

                    if (descriptor?.Has(SurveyControlCapabilities.Answerable) == true
                        && _registry.GetHandler(question.ControlType)?.GetFormulaType(question, null) is global::OrchardCore.DataSources.DataFieldType.Integer or global::OrchardCore.DataSources.DataFieldType.Decimal)
                    {
                        model.TrendQuestions.Add(new SelectListItem(title, question.Id, question.Id == model.TrendQuestionId));
                    }
                }
            }

            if (model.ShowAudience && _serviceProvider.GetService<IUserAudienceService>() is { } audiences)
            {
                model.Audiences = [.. (await audiences.ListAsync(cancellationToken)).Select(audience => new SelectListItem(audience.DisplayName ?? audience.Name, audience.Name, audience.Name == model.AudienceId))];
            }
        }).Location("Content:2");
    }

    public override async Task<IDisplayResult> UpdateAsync(ReportFilter filter, UpdateEditorContext context)
    {
        if (!SurveyReportNames.IsSurveyReport(filter.ReportName))
        {
            return null;
        }

        var model = new SurveyReportFilterViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        filter.Set(SurveyReportKeys.SurveyId, model.SurveyId?.Trim());
        filter.Set(SurveyReportKeys.Version, model.Version?.Trim());
        filter.Set(SurveyReportKeys.CollectorId, model.CollectorId?.Trim());
        filter.Set(SurveyReportKeys.ContextKind, model.ContextKind?.Trim());
        filter.Set(SurveyReportKeys.SegmentQuestionId, model.SegmentQuestionId?.Trim());
        filter.Set(SurveyReportKeys.TrendQuestionId, model.TrendQuestionId?.Trim());
        filter.Set(SurveyReportKeys.TrendPeriod, model.TrendPeriod?.Trim());
        filter.Set(SurveyReportKeys.AudienceId, model.AudienceId?.Trim());
        filter.Set(SurveyReportKeys.Status, model.Status?.Trim());

        if (model.Page is > 1)
        {
            filter.Set(SurveyReportKeys.Page, model.Page.Value);
        }
        else
        {
            filter.Remove(SurveyReportKeys.Page);
        }

        if (model.IncludeTest)
        {
            filter.Set(SurveyReportKeys.IncludeTest, true);
        }
        else
        {
            filter.Remove(SurveyReportKeys.IncludeTest);
        }

        return Edit(filter, context);
    }
}
