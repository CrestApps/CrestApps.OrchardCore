using CrestApps.OrchardCore.ContactCenter.Core.Models.Reports;

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
