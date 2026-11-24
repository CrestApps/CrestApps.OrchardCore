namespace CrestApps.OrchardCore.Payroll.ViewModels;

/// <summary>
/// The Payroll card of the tax form editor.
/// </summary>
public class PayrollTaxFormSettingsViewModel
{
    public string Workers { get; set; }

    public string PeopleJurisdiction { get; set; }

    public bool ListJurisdictionAmounts { get; set; }

    public string PayCodeFieldPrefix { get; set; }
}
