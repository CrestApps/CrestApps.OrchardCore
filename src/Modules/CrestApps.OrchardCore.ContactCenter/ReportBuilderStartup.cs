using CrestApps.OrchardCore.ContactCenter.Reports.DataSources;
using CrestApps.OrchardCore.ContactCenter.Reports.DataSources.DataSets;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Exposes the Contact Center to the report builder when it is enabled: the Contact Center data source, and the data
/// sets of the records the base feature stores (interactions, their events, call sessions, and call quality). Every
/// other feature registers the data sets of its own records, so a data set is listed only while its feature is on.
/// </summary>
[RequireFeatures(ReportsConstants.BuilderFeature)]
public sealed class ReportBuilderStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IReportDataSource, ContactCenterReportDataSource>()
            .AddScoped<IContactCenterReportDataSet, InteractionReportDataSet>()
            .AddScoped<IContactCenterReportDataSet, InteractionEventReportDataSet>()
            .AddScoped<IContactCenterReportDataSet, CallSessionReportDataSet>()
            .AddScoped<IContactCenterReportDataSet, CallQualityReportDataSet>();
    }
}

/// <summary>
/// Registers the report builder data sets of the work distribution feature: queues, queue groups, and queue items.
/// </summary>
[Feature(ContactCenterConstants.Feature.Queues)]
[RequireFeatures(ReportsConstants.BuilderFeature)]
public sealed class QueuesReportBuilderStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IContactCenterReportDataSet, QueueReportDataSet>()
            .AddScoped<IContactCenterReportDataSet, QueueGroupReportDataSet>()
            .AddScoped<IContactCenterReportDataSet, QueueItemReportDataSet>();
    }
}

/// <summary>
/// Registers the report builder data sets of the outbound dialer feature: dialer profiles and callback requests.
/// </summary>
[Feature(ContactCenterConstants.Feature.Dialer)]
[RequireFeatures(ReportsConstants.BuilderFeature)]
public sealed class DialerReportBuilderStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IContactCenterReportDataSet, DialerProfileReportDataSet>()
            .AddScoped<IContactCenterReportDataSet, CallbackRequestReportDataSet>();
    }
}

/// <summary>
/// Registers the report builder data set of the call recording feature.
/// </summary>
[Feature(ContactCenterConstants.Feature.Recording)]
[RequireFeatures(ReportsConstants.BuilderFeature)]
public sealed class RecordingReportBuilderStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContactCenterReportDataSet, CallRecordingReportDataSet>();
    }
}

/// <summary>
/// Registers the report builder data set of the agent services feature: agent profiles.
/// </summary>
[Feature(ContactCenterConstants.Feature.AgentServices)]
[RequireFeatures(ReportsConstants.BuilderFeature)]
public sealed class AgentServicesReportBuilderStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContactCenterReportDataSet, AgentProfileReportDataSet>();
    }
}

/// <summary>
/// Registers the report builder data set of the agents feature: agent sessions.
/// </summary>
[Feature(ContactCenterConstants.Feature.Agents)]
[RequireFeatures(ReportsConstants.BuilderFeature)]
public sealed class AgentsReportBuilderStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContactCenterReportDataSet, AgentSessionReportDataSet>();
    }
}

/// <summary>
/// Registers the report builder data set of the inbound voice feature: shared voicemails.
/// </summary>
[Feature(ContactCenterConstants.Feature.InboundVoice)]
[RequireFeatures(ReportsConstants.BuilderFeature)]
public sealed class InboundVoiceReportBuilderStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContactCenterReportDataSet, SharedVoicemailReportDataSet>();
    }
}
