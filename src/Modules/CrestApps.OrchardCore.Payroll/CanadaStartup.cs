using CrestApps.OrchardCore.Payroll.Core.Canada;
using CrestApps.OrchardCore.Payroll.DataSources;
using CrestApps.OrchardCore.Payroll.Drivers;
using CrestApps.OrchardCore.Payroll.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Payroll;

/// <summary>
/// Canada Payroll Filings: the T4 slips and Summary, and the RL-1 slips and Summary, made from pay statements. The
/// source deductions are Taxation's Canada Income Tax, remitted on the schedule of the employer's CA and CA-QC
/// registrations.
/// </summary>
[Feature(PayrollConstants.Features.Canada)]
public sealed class CanadaStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPayrollFilingForm<FormT4>();
        services.AddPayrollFilingForm<FormRl1>();
        services.AddDisplayDriver<Employer, EmployerCanadaDisplayDriver>();
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
