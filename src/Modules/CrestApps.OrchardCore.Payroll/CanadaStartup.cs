using CrestApps.OrchardCore.Payroll.Core.Canada;
using CrestApps.OrchardCore.Payroll.Core.TaxForms;
using CrestApps.OrchardCore.Payroll.DataSources;
using CrestApps.OrchardCore.Payroll.Drivers;
using CrestApps.OrchardCore.Payroll.Migrations;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Core.TaxForms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Payroll;

/// <summary>
/// Canada Payroll Filings: adds the T4 and RL-1 tax forms as tax form records from the Canada tax forms recipe the first
/// time the feature is on, the DENTALCODE() formula function and the employer's Canada card. The source deductions are
/// Taxation's Canada Income Tax, remitted on the schedule of the employer's CA and CA-QC registrations.
/// </summary>
[Feature(PayrollConstants.Features.Canada)]
public sealed class CanadaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTaxFormFunctions<CanadaTaxFormFunctions>();
        services.AddDataMigration<CanadaTaxFormsMigrations>();
        services.AddDisplayDriver<Employer, EmployerCanadaDisplayDriver>();
    }
}

/// <summary>
/// With Taxation's Canada Income Tax on: an employee of an employer registered with the CRA needs exactly one province
/// of employment.
/// </summary>
[Feature(PayrollConstants.Features.Payroll)]
[RequireFeatures(TaxationConstants.Feature.IncomeTaxCanada)]
public sealed class CanadaIncomeTaxStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPayProfileBlockerProvider, CanadaProvinceOfEmploymentBlockers>());
    }
}

/// <summary>
/// The T4 slips data set and the CRA T4 XML file format, with Data Pipelines Core on.
/// </summary>
[Feature(PayrollConstants.Features.Canada)]
[RequireFeatures(OrchardCoreFeatureIds.DataPipelinesCore)]
public sealed class CanadaDataSourcesStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IPayrollDataSet, T4SlipsDataSet>();
        services.AddDataFileFormat<T4XmlDataFileFormat>();
    }
}
