using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Skills.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Skills.Drivers;

/// <summary>
/// The rows of the certificate list.
/// </summary>
internal sealed class CertificationRecordRowDisplayDriver : DisplayDriver<CertificationRecord>
{
    private readonly ICertificationManager _certificationManager;

    public CertificationRecordRowDisplayDriver(ICertificationManager certificationManager)
    {
        _certificationManager = certificationManager;
    }

    public override async Task<IDisplayResult> DisplayAsync(CertificationRecord record, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape row)
        {
            row.Classes.Add("item");
        }

        var certification = string.IsNullOrEmpty(record.CertificationId)
            ? null
            : await _certificationManager.FindByIdAsync(record.CertificationId);

        void Populate(CertificationRecordRowViewModel model)
        {
            model.Record = record;
            model.CertificationName = certification?.Name;
        }

        return Combine(
            Initialize<CertificationRecordRowViewModel>("CertificationRecord_Fields_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            Initialize<CertificationRecordRowViewModel>("CertificationRecord_Tags_SummaryAdmin", Populate)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            Initialize<CertificationRecordRowViewModel>("CertificationRecord_Buttons_SummaryAdmin", Populate).Location("Actions:5"),
            Initialize<CertificationRecordRowViewModel>("CertificationRecord_ActionsMenu_SummaryAdmin", Populate).Location("ActionsMenu:10"));
    }
}
