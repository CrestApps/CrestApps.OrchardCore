using CrestApps.OrchardCore.ContactCenter.Reports.DataSources;
using CrestApps.OrchardCore.ContactCenter.Reports.DataSources.DataSets;
using CrestApps.OrchardCore.Reports;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Exposes the Contact Center as a data source of Orchard Core, for reports and data pipelines: the data source, and the data
/// sets of the records the base feature stores (interactions, their events, call sessions, and call quality). Every
/// other feature registers the data sets of its own records, so a data set is listed only while its feature is on.
/// </summary>
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class DataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDataSource<ContactCenterReportDataSource>()
            .AddScoped<IContactCenterReportDataSet, InteractionReportDataSet>()
            .AddScoped<IContactCenterReportDataSet, InteractionEventReportDataSet>()
            .AddScoped<IContactCenterReportDataSet, CallSessionReportDataSet>()
            .AddScoped<IContactCenterReportDataSet, CallQualityReportDataSet>();
    }
}

/// <summary>
/// Registers the data sets of the work distribution feature: queues, queue groups, queue items, and the dialer decision
/// log of the routing decisions it makes (and the dial suppressions of the dialer, which builds on it).
/// </summary>
[Feature(ContactCenterConstants.Feature.Queues)]
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class QueuesDataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IContactCenterReportDataSet, QueueReportDataSet>()
            .AddScoped<IContactCenterReportDataSet, QueueGroupReportDataSet>()
            .AddScoped<IContactCenterReportDataSet, QueueItemReportDataSet>()
            .AddScoped<IContactCenterReportDataSet, DialerDecisionReportDataSet>();
    }
}

/// <summary>
/// Registers the data sets of the outbound dialer feature: dialer profiles and callback requests.
/// </summary>
[Feature(ContactCenterConstants.Feature.Dialer)]
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class DialerDataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IContactCenterReportDataSet, DialerProfileReportDataSet>()
            .AddScoped<IContactCenterReportDataSet, CallbackRequestReportDataSet>();
    }
}

/// <summary>
/// Registers the data set of the paced dialing feature: the pacing cycles it records.
/// </summary>
[Feature(ContactCenterConstants.Feature.DialerPaced)]
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class DialerPacedDataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContactCenterReportDataSet, DialerPacingCycleReportDataSet>();
    }
}

/// <summary>
/// Registers the data set of the call recording feature.
/// </summary>
[Feature(ContactCenterConstants.Feature.Recording)]
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class RecordingDataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContactCenterReportDataSet, CallRecordingReportDataSet>();
    }
}

/// <summary>
/// Registers the data set of the agent services feature: agent profiles.
/// </summary>
[Feature(ContactCenterConstants.Feature.AgentServices)]
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class AgentServicesDataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContactCenterReportDataSet, AgentProfileReportDataSet>();
    }
}

/// <summary>
/// Registers the data sets of the agents feature: agent sessions, and the agent state changes it records.
/// </summary>
[Feature(ContactCenterConstants.Feature.Agents)]
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class AgentsDataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IContactCenterReportDataSet, AgentSessionReportDataSet>()
            .AddScoped<IContactCenterReportDataSet, AgentStateChangeReportDataSet>();
    }
}

/// <summary>
/// Registers the data set of the inbound voice feature: shared voicemails.
/// </summary>
[Feature(ContactCenterConstants.Feature.InboundVoice)]
[RequireFeatures("OrchardCore.DataPipelines.Core")]
public sealed class InboundVoiceDataSourcesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContactCenterReportDataSet, SharedVoicemailReportDataSet>();
    }
}
