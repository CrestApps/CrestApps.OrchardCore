using CrestApps.OrchardCore.Payroll.Services;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// The election fields of any jurisdiction a country pack gives no form for: a filing status, allowances, extra
/// withholding and exemption.
/// </summary>
public static class GenericElectionFields
{
    /// <summary>
    /// Creates the field set.
    /// </summary>
    /// <returns>The field set for any jurisdiction.</returns>
    public static ElectionFieldSet Create()
    {
        var set = new ElectionFieldSet
        {
            Jurisdiction = "*",
            DisplayName = LocalizationSource.Create("Withholding election", typeof(GenericElectionFields)),
        };

        set.Fields.Add(new ElectionField("FilingStatus", LocalizationSource.Create("Filing status", typeof(GenericElectionFields)), ElectionFieldKind.Text));
        set.Fields.Add(new ElectionField("Allowances", LocalizationSource.Create("Allowances", typeof(GenericElectionFields)), ElectionFieldKind.Integer));
        set.Fields.Add(new ElectionField("ExtraWithholding", LocalizationSource.Create("Extra withholding each period", typeof(GenericElectionFields)), ElectionFieldKind.Amount));
        set.Fields.Add(new ElectionField("Exempt", LocalizationSource.Create("Exempt from withholding", typeof(GenericElectionFields)), ElectionFieldKind.Boolean));

        return set;
    }
}
