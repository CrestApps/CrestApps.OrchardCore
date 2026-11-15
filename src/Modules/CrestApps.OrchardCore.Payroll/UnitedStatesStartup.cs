using CrestApps.OrchardCore.Payroll.Core.UnitedStates;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Payroll;

/// <summary>
/// United States Payroll: the Form W-4 election fields and the federal withholding method. The federal tax rules come
/// as dated recipes (Tools > Recipes).
/// </summary>
[Feature(PayrollConstants.Features.UnitedStates)]
public sealed class UnitedStatesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddElectionFieldSet(UnitedStatesElectionFields.Create());
        services.AddPayrollWithholdingMethod<UnitedStatesFormW4WithholdingMethod>();
    }
}
