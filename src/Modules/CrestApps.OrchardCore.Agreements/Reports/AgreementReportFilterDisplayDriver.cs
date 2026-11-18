using CrestApps.OrchardCore.Agreements.ViewModels;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Agreements.Reports;

/// <summary>
/// The agreement the agreement reports are about: one, or every active agreement with covered hours.
/// </summary>
internal sealed class AgreementReportFilterDisplayDriver : DisplayDriver<ReportFilter>
{
    private readonly AgreementReportData _data;

    public AgreementReportFilterDisplayDriver(AgreementReportData data)
    {
        _data = data;
    }

    public override IDisplayResult Edit(ReportFilter filter, BuildEditorContext context)
    {
        if (!IsAgreementReport(filter))
        {
            return null;
        }

        return Initialize<AgreementReportFilterViewModel>("AgreementReportFilter_Edit", async model =>
        {
            model.AgreementId = filter.GetOrDefault<string>(AgreementReportData.AgreementFilterKey);
            model.Agreements = [.. (await _data.ListChoicesAsync(default)).Select(choice => new SelectListItem(choice.Text, choice.Id, choice.Id == model.AgreementId))];
        }).Location("Content:2");
    }

    public override async Task<IDisplayResult> UpdateAsync(ReportFilter filter, UpdateEditorContext context)
    {
        if (!IsAgreementReport(filter))
        {
            return null;
        }

        var model = new AgreementReportFilterViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);
        filter.Set(AgreementReportData.AgreementFilterKey, model.AgreementId?.Trim());

        return Edit(filter, context);
    }

    private static bool IsAgreementReport(ReportFilter filter)
        => filter.ReportName == AgreementUsageReport.Name || filter.ReportName == AgreementBurnDownReport.Name;
}
