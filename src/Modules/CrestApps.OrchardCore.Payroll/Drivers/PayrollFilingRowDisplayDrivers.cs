using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Payroll.Drivers;

/// <summary>
/// The rows of the liability periods.
/// </summary>
internal sealed class TaxLiabilityPeriodDisplayDriver : DisplayDriver<TaxLiabilityPeriod>
{
    public override Task<IDisplayResult> DisplayAsync(TaxLiabilityPeriod period, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        return CombineAsync(
            View("TaxLiabilityPeriod_Fields_SummaryAdmin", period)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("TaxLiabilityPeriod_Tags_SummaryAdmin", period)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"));
    }
}

/// <summary>
/// The rows of the deposits.
/// </summary>
internal sealed class TaxDepositDisplayDriver : DisplayDriver<TaxDeposit>
{
    public override Task<IDisplayResult> DisplayAsync(TaxDeposit deposit, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        return CombineAsync(
            View("TaxDeposit_Fields_SummaryAdmin", deposit)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("TaxDeposit_ActionsMenu_SummaryAdmin", deposit).Location("ActionsMenu:10"));
    }
}

/// <summary>
/// The rows of a person's tax forms in My Pay.
/// </summary>
internal sealed class MyPayTaxFormDisplayDriver : DisplayDriver<MyPayTaxFormViewModel>
{
    public override Task<IDisplayResult> DisplayAsync(MyPayTaxFormViewModel form, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        return CombineAsync(
            View("MyPayTaxForm_Fields_SummaryAdmin", form)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("MyPayTaxForm_Buttons_SummaryAdmin", form).Location("Actions:5"));
    }
}

/// <summary>
/// The rows of the filings.
/// </summary>
internal sealed class PayrollFilingDisplayDriver : DisplayDriver<PayrollFiling>
{
    private readonly PayrollNames _names;

    public PayrollFilingDisplayDriver(PayrollNames names)
    {
        _names = names;
    }

    public override async Task<IDisplayResult> DisplayAsync(PayrollFiling filing, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        var employer = await _names.EmployerAsync(filing.EmployerId);

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{filing.Form} {filing.Period} {employer}".ToLowerInvariant();
        }

        return Combine(
            Initialize<PayrollFilingRowViewModel>("PayrollFiling_Fields_SummaryAdmin", model =>
            {
                model.Filing = filing;
                model.EmployerName = employer;
            })
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("PayrollFiling_Tags_SummaryAdmin", filing)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("PayrollFiling_Buttons_SummaryAdmin", filing).Location("Actions:5"));
    }
}
