using CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Produces the historical Contact Center reports used by supervisors and administrators to understand
/// call activity, agent productivity, queue usage, and campaign/subject progress.
/// </summary>
public interface IContactCenterReportingService
{
    /// <summary>
    /// Builds the call insights report over the inclusive UTC period.
    /// </summary>
    /// <param name="fromUtc">The inclusive lower UTC bound.</param>
    /// <param name="toUtc">The inclusive upper UTC bound.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The call insights report.</returns>
    Task<CallInsightsReport> GetCallInsightsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the filtered call insights report over the inclusive UTC period.
    /// </summary>
    /// <param name="fromUtc">The inclusive lower UTC bound.</param>
    /// <param name="toUtc">The inclusive upper UTC bound.</param>
    /// <param name="criteria">The report dimension filters.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The call insights report.</returns>
    Task<CallInsightsReport> GetCallInsightsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        ContactCenterReportCriteria criteria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the agent productivity report over the inclusive UTC period.
    /// </summary>
    /// <param name="fromUtc">The inclusive lower UTC bound.</param>
    /// <param name="toUtc">The inclusive upper UTC bound.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The agent productivity report.</returns>
    Task<AgentProductivityReport> GetAgentProductivityAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the filtered agent productivity report over the inclusive UTC period.
    /// </summary>
    /// <param name="fromUtc">The inclusive lower UTC bound.</param>
    /// <param name="toUtc">The inclusive upper UTC bound.</param>
    /// <param name="criteria">The report dimension filters.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The agent productivity report.</returns>
    Task<AgentProductivityReport> GetAgentProductivityAsync(
        DateTime fromUtc,
        DateTime toUtc,
        ContactCenterReportCriteria criteria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the queue usage report over the inclusive UTC period, including live waiting depth.
    /// </summary>
    /// <param name="fromUtc">The inclusive lower UTC bound.</param>
    /// <param name="toUtc">The inclusive upper UTC bound.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The queue usage report.</returns>
    Task<QueueUsageReport> GetQueueUsageAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the filtered queue usage report over the inclusive UTC period.
    /// </summary>
    /// <param name="fromUtc">The inclusive lower UTC bound.</param>
    /// <param name="toUtc">The inclusive upper UTC bound.</param>
    /// <param name="criteria">The report dimension filters.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The queue usage report.</returns>
    Task<QueueUsageReport> GetQueueUsageAsync(
        DateTime fromUtc,
        DateTime toUtc,
        ContactCenterReportCriteria criteria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the campaign summary report over the inclusive UTC period, showing completed versus pending
    /// activities for each campaign.
    /// </summary>
    /// <param name="fromUtc">The inclusive lower UTC bound.</param>
    /// <param name="toUtc">The inclusive upper UTC bound.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The campaign summary report.</returns>
    Task<CampaignSummaryReport> GetCampaignSummaryAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the filtered campaign summary report over the inclusive UTC period.
    /// </summary>
    /// <param name="fromUtc">The inclusive lower UTC bound.</param>
    /// <param name="toUtc">The inclusive upper UTC bound.</param>
    /// <param name="criteria">The report dimension filters.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The campaign summary report.</returns>
    Task<CampaignSummaryReport> GetCampaignSummaryAsync(
        DateTime fromUtc,
        DateTime toUtc,
        ContactCenterReportCriteria criteria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the CRM activities the campaign summary counts over the inclusive UTC period: those created in it, narrowed
    /// by the criteria.
    /// </summary>
    /// <param name="fromUtc">The inclusive lower UTC bound.</param>
    /// <param name="toUtc">The inclusive upper UTC bound.</param>
    /// <param name="criteria">The report dimension filters.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The activities, read from the activity index.</returns>
    Task<IReadOnlyList<OmnichannelActivityIndex>> GetCampaignActivitiesAsync(
        DateTime fromUtc,
        DateTime toUtc,
        ContactCenterReportCriteria criteria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the campaign summary report of activities the caller has already read and narrowed, such as with
    /// <see cref="GetCampaignActivitiesAsync(DateTime, DateTime, ContactCenterReportCriteria, CancellationToken)"/> and
    /// a filter of its own.
    /// </summary>
    /// <param name="fromUtc">The inclusive lower UTC bound the activities were read for.</param>
    /// <param name="toUtc">The inclusive upper UTC bound the activities were read for.</param>
    /// <param name="activities">The activities to count.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The campaign summary report.</returns>
    Task<CampaignSummaryReport> BuildCampaignSummaryAsync(
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlyList<OmnichannelActivityIndex> activities,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the subject inventory report over the inclusive UTC period, showing completed versus pending
    /// activities for each subject type.
    /// </summary>
    /// <param name="fromUtc">The inclusive lower UTC bound.</param>
    /// <param name="toUtc">The inclusive upper UTC bound.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The subject inventory report.</returns>
    Task<SubjectInventoryReport> GetSubjectInventoryAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the filtered subject inventory report over the inclusive UTC period.
    /// </summary>
    /// <param name="fromUtc">The inclusive lower UTC bound.</param>
    /// <param name="toUtc">The inclusive upper UTC bound.</param>
    /// <param name="criteria">The report dimension filters.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The subject inventory report.</returns>
    Task<SubjectInventoryReport> GetSubjectInventoryAsync(
        DateTime fromUtc,
        DateTime toUtc,
        ContactCenterReportCriteria criteria,
        CancellationToken cancellationToken = default);
}
