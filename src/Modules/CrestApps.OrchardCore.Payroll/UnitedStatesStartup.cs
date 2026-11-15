using CrestApps.OrchardCore.Payroll.Core.UnitedStates;
using CrestApps.OrchardCore.Payroll.DataSources;
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
        services.AddPayrollFilingForm<Form941>();
        services.AddPayrollFilingForm<Form940>();
        services.AddPayrollFilingForm<FormW2>();
    }
}

/// <summary>
/// The W-2 wage records data set and the SSA EFW2 file format, with Data Pipelines Core on.
/// </summary>
[Feature(PayrollConstants.Features.UnitedStates)]
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class UnitedStatesDataSourcesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IPayrollDataSet, WageRecordsDataSet>();
        services.AddDataFileFormat<Efw2DataFileFormat>();
    }
}

