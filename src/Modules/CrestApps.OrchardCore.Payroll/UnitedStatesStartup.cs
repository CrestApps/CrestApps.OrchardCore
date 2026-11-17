using CrestApps.OrchardCore.Payroll.Core.UnitedStates;
using CrestApps.OrchardCore.Payroll.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Payroll;

/// <summary>
/// United States Payroll Filings: Forms 941, 940, 945, W-2 and 1099-NEC made from pay statements. Form W-4 and the
/// federal rules are Taxation's United States Income Tax.
/// </summary>
[Feature(PayrollConstants.Features.UnitedStates)]
public sealed class UnitedStatesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPayrollFilingForm<Form941>();
        services.AddPayrollFilingForm<Form940>();
        services.AddPayrollFilingForm<FormW2>();
        services.AddPayrollFilingForm<Form1099Nec>();
        services.AddPayrollFilingForm<Form945>();
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

