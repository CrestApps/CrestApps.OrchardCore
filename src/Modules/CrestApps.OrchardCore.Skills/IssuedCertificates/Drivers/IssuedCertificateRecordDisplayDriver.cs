using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Skills.IssuedCertificates.Drivers;

/// <summary>
/// Adds the certificate document and the verification page to the row of a certificate the site issued.
/// </summary>
internal sealed class IssuedCertificateRecordDisplayDriver : DisplayDriver<CertificationRecord>
{
    public override IDisplayResult Display(CertificationRecord record, BuildDisplayContext context)
    {
        if (string.IsNullOrEmpty(record.VerificationCode))
        {
            return null;
        }

        return Initialize<CertificationRecordRowViewModel>("CertificationRecord_IssuedButtons_SummaryAdmin", model => model.Record = record)
            .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:6")
            .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Actions:6");
    }
}
