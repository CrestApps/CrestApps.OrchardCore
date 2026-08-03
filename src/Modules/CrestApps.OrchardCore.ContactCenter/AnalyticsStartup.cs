using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.Drivers;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Providers;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Registers the reporting and analytics experience: the reporting service that aggregates interactions
/// and activities into productivity, call insights, queue usage, and campaign/subject progress reports,
/// and the Reports admin navigation.
/// </summary>
[Feature(ContactCenterConstants.Feature.Analytics)]
public sealed class AnalyticsStartup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;
    private readonly IStringLocalizer S;

    public AnalyticsStartup(
        IShellConfiguration shellConfiguration,
        IStringLocalizer<AnalyticsStartup> stringLocalizer)
    {
        _shellConfiguration = shellConfiguration;
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContactCenterReportingService, ContactCenterReportingService>();
        services.AddScoped<IContactCenterReportCapabilityGuard, ContactCenterReportCapabilityGuard>();
        services.AddDisplayDriver<ReportFilter, ContactCenterReportFilterDisplayDriver>();

        services
            .AddOptions<ContactCenterReportingOptions>()
            .Bind(_shellConfiguration.GetSection("CrestApps_ContactCenter:Reporting"))
            .Validate(
                options => options.MaximumReportRange > TimeSpan.Zero,
                "'CrestApps_ContactCenter:Reporting:MaximumReportRange' must be greater than zero.")
            .ValidateOnStart();

        services
            .AddScoped<IReport, CallInsightsReportProvider>()
            .AddScoped<IReport, AgentProductivityReportProvider>()
            .AddScoped<IReport, QueueUsageReportProvider>()
            .AddScoped<IReport, CampaignSummaryReportProvider>()
            .AddScoped<IReport, SubjectInventoryReportProvider>();

        AddEnterpriseReport(services, "contact-center-executive-performance", () => S["Executive performance dashboard"], () => S["Enterprise KPI cards and interactive charts for interaction demand, accessibility, channel mix, queue service level, agent workload, and operating efficiency."], EnterpriseInteractionReportKind.ExecutiveSummary, ReportsConstants.Categories.Executive);
        AddEnterpriseReport(services, "contact-center-interaction-volume-trend", () => S["Interaction volume trend"], () => S["Daily offered, answered, abandoned, and failed interaction volume."], EnterpriseInteractionReportKind.VolumeTrend, ReportsConstants.Categories.Operations);
        AddEnterpriseReport(services, "contact-center-interval-performance", () => S["Interval performance"], () => S["Daily interaction outcomes, answer and abandonment rates, speed of answer, and handle time."], EnterpriseInteractionReportKind.IntervalPerformance, ReportsConstants.Categories.Operations);
        AddEnterpriseReport(services, "contact-center-channel-performance", () => S["Channel performance"], () => S["Interaction performance grouped by voice, chat, email, SMS, and other supported channels."], EnterpriseInteractionReportKind.ChannelPerformance, ReportsConstants.Categories.Operations);
        AddEnterpriseReport(services, "contact-center-direction-performance", () => S["Direction performance"], () => S["Inbound and outbound interaction performance with consistent outcome and duration metrics."], EnterpriseInteractionReportKind.DirectionPerformance, ReportsConstants.Categories.Operations);
        AddEnterpriseReport(services, "contact-center-provider-performance", () => S["Provider performance"], () => S["Interaction outcomes and duration metrics grouped by executing communications provider."], EnterpriseInteractionReportKind.ProviderPerformance, ReportsConstants.Categories.Technical);
        AddEnterpriseReport(services, "contact-center-outcome-performance", () => S["Interaction outcome summary"], () => S["Interaction volume and response metrics grouped by normalized lifecycle outcome."], EnterpriseInteractionReportKind.OutcomePerformance, ReportsConstants.Categories.Operations);
        AddEnterpriseReport(services, "contact-center-interaction-detail", () => S["Interaction detail"], () => S["One row per interaction with routing, agent, provider, timing, and transfer details."], EnterpriseInteractionReportKind.InteractionDetail, ReportsConstants.Categories.ComplianceAudit);
        AddEnterpriseReport(services, "contact-center-transfer-analysis", () => S["Transfer analysis"], () => S["Transfer volume, completion, destination type, result, and completion time."], EnterpriseInteractionReportKind.TransferAnalysis, ReportsConstants.Categories.QueueRouting);
        AddEnterpriseReport(services, "contact-center-recording-coverage", () => S["Recording coverage"], () => S["Answered voice interaction recording coverage grouped by provider."], EnterpriseInteractionReportKind.RecordingCoverage, ReportsConstants.Categories.ComplianceAudit);
        AddEnterpriseReport(services, "contact-center-queue-service-level", () => S["Queue service level"], () => S["Queue service level calculated from answered-within-threshold interactions and eligible offered interactions."], EnterpriseInteractionReportKind.QueueServiceLevel, ReportsConstants.Categories.QueueRouting);
        AddEnterpriseReport(services, "contact-center-queue-abandonment", () => S["Queue abandonment analysis"], () => S["Inbound queue offered, answered, abandoned, abandonment rate, and wait before abandonment."], EnterpriseInteractionReportKind.QueueAbandonment, ReportsConstants.Categories.QueueRouting);
        AddEnterpriseReport(services, "contact-center-agent-handle-time", () => S["Agent handle time analysis"], () => S["Per-agent handled volume, connected time, wrap-up time, and total handle time."], EnterpriseInteractionReportKind.AgentHandleTime, ReportsConstants.Categories.AgentPerformance);
        AddEnterpriseReport(services, "contact-center-wrap-up-performance", () => S["Agent wrap-up performance"], () => S["Per-agent wrap-up starts, completions, completion rate, average duration, and total duration."], EnterpriseInteractionReportKind.WrapUpPerformance, ReportsConstants.Categories.AgentPerformance);
        AddEnterpriseReport(services, "contact-center-hour-of-day-performance", () => S["Hour-of-day performance"], () => S["Interaction demand, outcomes, and response metrics grouped by UTC hour."], EnterpriseInteractionReportKind.HourOfDayPerformance, ReportsConstants.Categories.Operations);
        AddEnterpriseReport(services, "contact-center-day-of-week-performance", () => S["Day-of-week performance"], () => S["Interaction demand, outcomes, and response metrics grouped by weekday."], EnterpriseInteractionReportKind.DayOfWeekPerformance, ReportsConstants.Categories.Operations);
        AddEnterpriseReport(services, "contact-center-queue-performance", () => S["Queue performance summary"], () => S["Interaction volume, outcomes, answer rate, abandonment, ASA, and AHT by queue."], EnterpriseInteractionReportKind.QueuePerformance, ReportsConstants.Categories.QueueRouting);
        AddEnterpriseReport(services, "contact-center-queue-wait-time", () => S["Queue wait time analysis"], () => S["Total, average, and maximum customer wait time by queue."], EnterpriseInteractionReportKind.QueueWaitTime, ReportsConstants.Categories.QueueRouting);
        AddEnterpriseReport(services, "contact-center-queue-handle-time", () => S["Queue handle time analysis"], () => S["Total, average, and maximum connected plus wrap-up time by queue."], EnterpriseInteractionReportKind.QueueHandleTime, ReportsConstants.Categories.QueueRouting);
        AddEnterpriseReport(services, "contact-center-queue-transfer-performance", () => S["Queue transfer performance"], () => S["Handled interactions, transferred interactions, transfer events, and transfer rate by queue."], EnterpriseInteractionReportKind.QueueTransferPerformance, ReportsConstants.Categories.QueueRouting);
        AddEnterpriseReport(services, "contact-center-agent-volume", () => S["Agent interaction volume"], () => S["Handled, answered, failed, transferred, recorded, and average handle time by agent."], EnterpriseInteractionReportKind.AgentVolume, ReportsConstants.Categories.AgentPerformance);
        AddEnterpriseReport(services, "contact-center-agent-outcomes", () => S["Agent outcome performance"], () => S["Interaction outcomes and average handle time by agent, ordered by failed volume."], EnterpriseInteractionReportKind.AgentOutcome, ReportsConstants.Categories.AgentPerformance);
        AddEnterpriseReport(services, "contact-center-agent-inbound", () => S["Agent inbound performance"], () => S["Inbound handled volume, outcomes, transfers, recording count, and handle time by agent."], EnterpriseInteractionReportKind.AgentInbound, ReportsConstants.Categories.AgentPerformance);
        AddEnterpriseReport(services, "contact-center-agent-outbound", () => S["Agent outbound performance"], () => S["Outbound handled volume, outcomes, transfers, recording count, and handle time by agent."], EnterpriseInteractionReportKind.AgentOutbound, ReportsConstants.Categories.AgentPerformance);
        AddEnterpriseReport(services, "contact-center-agent-transfers", () => S["Agent transfer performance"], () => S["Transfer volume and supporting interaction metrics by agent."], EnterpriseInteractionReportKind.AgentTransferPerformance, ReportsConstants.Categories.AgentPerformance);
        AddEnterpriseReport(services, "contact-center-agent-recording-coverage", () => S["Agent recording coverage"], () => S["Recorded interaction volume and supporting performance metrics by agent."], EnterpriseInteractionReportKind.AgentRecordingCoverage, ReportsConstants.Categories.ComplianceAudit);
        AddEnterpriseReport(services, "contact-center-queue-usage-billing", () => S["Queue usage for billing"], () => S["Interaction counts, connected time, wrap-up, queue wait, transfers, and recordings by queue for invoice support."], EnterpriseInteractionReportKind.QueueUsageBilling, ReportsConstants.Categories.BillingUsage);
        AddEnterpriseReport(services, "contact-center-agent-usage-billing", () => S["Agent usage for billing"], () => S["Interaction counts and measured service time by agent for staffing, payroll, and chargeback support."], EnterpriseInteractionReportKind.AgentUsageBilling, ReportsConstants.Categories.BillingUsage);
        AddEnterpriseReport(services, "contact-center-provider-usage-billing", () => S["Provider usage for billing"], () => S["Interaction counts and measured service time by communications provider for vendor invoice reconciliation."], EnterpriseInteractionReportKind.ProviderUsageBilling, ReportsConstants.Categories.BillingUsage);
        AddEnterpriseReport(services, "contact-center-channel-usage-billing", () => S["Channel usage for billing"], () => S["Interaction counts and measured service time by channel for service allocation and chargeback."], EnterpriseInteractionReportKind.ChannelUsageBilling, ReportsConstants.Categories.BillingUsage);
        AddEnterpriseReport(services, "contact-center-daily-usage-billing", () => S["Daily usage for billing"], () => S["Daily interaction counts and measured service time for invoice period reconciliation."], EnterpriseInteractionReportKind.DailyUsageBilling, ReportsConstants.Categories.BillingUsage);
        AddEnterpriseReport(services, "contact-center-long-interactions", () => S["Long interaction detail"], () => S["Interaction-level audit of connected sessions lasting at least 15 minutes."], EnterpriseInteractionReportKind.LongInteractionDetail, ReportsConstants.Categories.ComplianceAudit);
        AddEnterpriseReport(services, "contact-center-failed-interactions", () => S["Failed interaction detail"], () => S["Interaction-level audit of failed communications."], EnterpriseInteractionReportKind.FailedInteractionDetail, ReportsConstants.Categories.Technical);
        AddEnterpriseReport(services, "contact-center-abandoned-interactions", () => S["Abandoned interaction detail"], () => S["Interaction-level audit of inbound customers who left before answer."], EnterpriseInteractionReportKind.AbandonedInteractionDetail, ReportsConstants.Categories.ComplianceAudit);
        AddEnterpriseReport(services, "contact-center-high-wait-interactions", () => S["High-wait interaction detail"], () => S["Interaction-level audit of customers who waited at least 60 seconds."], EnterpriseInteractionReportKind.HighWaitDetail, ReportsConstants.Categories.QueueRouting);
        AddEnterpriseReport(services, "contact-center-lifecycle-duration", () => S["Interaction lifecycle duration"], () => S["Average wait, connected, wrap-up, and end-to-end duration by interaction status."], EnterpriseInteractionReportKind.LifecycleDuration, ReportsConstants.Categories.Operations);
        AddEnterpriseReport(services, "contact-center-call-leg-performance", () => S["Call leg performance"], () => S["Provider call-leg volume, answer state, status, and average duration for technical operations."], EnterpriseInteractionReportKind.CallLegPerformance, ReportsConstants.Categories.Technical);

        AddWorkforceReport(services, "contact-center-agent-time-summary", () => S["Agent time summary"], () => S["Observed signed-in, available, busy, wrap-up, break, and other not-ready time by agent."], AgentWorkforceReportKind.TimeSummary, ReportsConstants.Categories.WorkforcePayroll);
        AddWorkforceReport(services, "contact-center-agent-daily-timecard", () => S["Daily agent timecard"], () => S["Daily observed on-duty, productive presence, working, and break time by agent."], AgentWorkforceReportKind.DailyTimecard, ReportsConstants.Categories.WorkforcePayroll);
        AddWorkforceReport(services, "contact-center-presence-status-duration", () => S["Presence status duration"], () => S["Total observed duration and share of signed-in time for every presence status."], AgentWorkforceReportKind.StatusDuration, ReportsConstants.Categories.WorkforcePayroll);
        AddWorkforceReport(services, "contact-center-agent-break-analysis", () => S["Agent break and away analysis"], () => S["Break count, total time, average duration, and longest duration by agent."], AgentWorkforceReportKind.BreakAnalysis, ReportsConstants.Categories.WorkforcePayroll);
        AddWorkforceReport(services, "contact-center-ready-not-ready", () => S["Ready versus not-ready time"], () => S["Ready, actively working, and not-ready time by agent."], AgentWorkforceReportKind.ReadyNotReady, ReportsConstants.Categories.WorkforcePayroll);
        AddWorkforceReport(services, "contact-center-agent-utilization", () => S["Agent utilization"], () => S["Busy plus wrap-up time divided by total observed signed-in time."], AgentWorkforceReportKind.Utilization, ReportsConstants.Categories.AgentPerformance);
        AddWorkforceReport(services, "contact-center-agent-occupancy", () => S["Agent occupancy"], () => S["Busy plus wrap-up time divided by available, reserved, busy, and wrap-up time."], AgentWorkforceReportKind.Occupancy, ReportsConstants.Categories.AgentPerformance);
        AddWorkforceReport(services, "contact-center-presence-reasons", () => S["Presence reason breakdown"], () => S["Observed presence duration and interval count by status and reason."], AgentWorkforceReportKind.ReasonBreakdown, ReportsConstants.Categories.WorkforcePayroll);
        AddWorkforceReport(services, "contact-center-presence-audit", () => S["Agent presence audit"], () => S["Detailed auditable presence transitions with status, reason, and membership counts."], AgentWorkforceReportKind.PresenceAudit, ReportsConstants.Categories.ComplianceAudit);
        AddWorkforceReport(services, "contact-center-queue-signed-in-hours", () => S["Queue signed-in hours"], () => S["Observed signed-in agent time attributed to queue memberships."], AgentWorkforceReportKind.QueueMembershipHours, ReportsConstants.Categories.WorkforcePayroll);
        AddWorkforceReport(services, "contact-center-campaign-signed-in-hours", () => S["Campaign signed-in hours"], () => S["Observed signed-in agent time attributed to campaign memberships."], AgentWorkforceReportKind.CampaignMembershipHours, ReportsConstants.Categories.WorkforcePayroll);
        AddWorkforceReport(services, "contact-center-payroll-timecard", () => S["Payroll timecard inputs"], () => S["Observed on-duty, productive presence, break, meeting, training, and other not-ready time for payroll review; pay rates and schedules are not applied."], AgentWorkforceReportKind.PayrollTimecard, ReportsConstants.Categories.WorkforcePayroll);
    }

    private static void AddEnterpriseReport(
        IServiceCollection services,
        string name,
        Func<LocalizedString> displayName,
        Func<LocalizedString> description,
        EnterpriseInteractionReportKind kind,
        string category)
    {
        var definition = new EnterpriseInteractionReportDefinition(
            name,
            displayName,
            description,
            kind,
            category,
            [
                ContactCenterReportFilter.QueueGroupId,
                ContactCenterReportFilter.QueueId,
                ContactCenterReportFilter.AgentId,
                ContactCenterReportFilter.Channel,
                ContactCenterReportFilter.Direction,
            ]);

        services.AddScoped<IReport>(serviceProvider => new EnterpriseInteractionReportProvider(
            serviceProvider.GetRequiredService<global::YesSql.ISession>(),
            serviceProvider.GetRequiredService<IActivityQueueManager>(),
            serviceProvider.GetRequiredService<IAgentProfileManager>(),
            definition,
            serviceProvider.GetRequiredService<IContactCenterReportCapabilityGuard>(),
            serviceProvider.GetRequiredService<IStringLocalizer<EnterpriseInteractionReportProvider>>(),
            serviceProvider.GetRequiredService<IOptions<ContactCenterReportingOptions>>().Value.MaximumReportRange));
    }

    private static void AddWorkforceReport(
        IServiceCollection services,
        string name,
        Func<LocalizedString> displayName,
        Func<LocalizedString> description,
        AgentWorkforceReportKind kind,
        string category)
    {
        var definition = new AgentWorkforceReportDefinition(
            name,
            displayName,
            description,
            kind,
            category,
            [ContactCenterReportFilter.AgentId]);

        services.AddScoped<IReport>(serviceProvider => new AgentWorkforceReportProvider(
            serviceProvider.GetRequiredService<IInteractionEventStore>(),
            serviceProvider.GetRequiredService<IAgentProfileManager>(),
            serviceProvider.GetRequiredService<ICatalogManager<OmnichannelCampaign>>(),
            definition,
            serviceProvider.GetRequiredService<IContactCenterReportCapabilityGuard>(),
            serviceProvider.GetRequiredService<IStringLocalizer<AgentWorkforceReportProvider>>()));
    }
}
