using CrestApps.OrchardCore.Payroll.DataSources;
using CrestApps.OrchardCore.Payroll.Migrations;
using CrestApps.OrchardCore.Payroll.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Payroll;

/// <summary>
/// United States Payroll Filings: adds the federal tax forms (Forms 941, 940, 945, W-2 and 1099-NEC) as tax form
/// records from the United States tax forms recipe, the first time the feature is on. Form W-4 and the federal rules are
/// Taxation's United States Income Tax.
/// </summary>
[Feature(PayrollConstants.Features.UnitedStates)]
public sealed class UnitedStatesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<PayrollTaxFormRecipeSeeder>();
        services.AddDataMigration<UnitedStatesTaxFormsMigrations>();
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

