using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Providers;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Reports;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// Projects the configured <see cref="ContactCenterReportCatalogOptions"/> into individual enterprise
/// interaction and agent workforce reports. A single provider registration replaces one service
/// registration per report, keeping the report catalog data-driven and extensible.
/// </summary>
internal sealed class ContactCenterReportProvider : IReportProvider
{
    private readonly ISession _session;
    private readonly IActivityQueueManager _queueManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly IInteractionEventStore _eventStore;
    private readonly ICatalogManager<OmnichannelCampaign> _campaignManager;
    private readonly IContactCenterReportCapabilityGuard _capabilityGuard;
    private readonly IStringLocalizer<EnterpriseInteractionReportProvider> _enterpriseLocalizer;
    private readonly IStringLocalizer<AgentWorkforceReportProvider> _workforceLocalizer;
    private readonly ContactCenterReportCatalogOptions _catalogOptions;
    private readonly TimeSpan _maximumReportRange;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterReportProvider"/> class.
    /// </summary>
    /// <param name="session">The YesSql session used by interaction reports.</param>
    /// <param name="queueManager">The activity queue manager.</param>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="eventStore">The interaction event store used by workforce reports.</param>
    /// <param name="campaignManager">The campaign catalog manager.</param>
    /// <param name="capabilityGuard">The guard that decides whether producing capabilities are enabled.</param>
    /// <param name="enterpriseLocalizer">The localizer for enterprise interaction reports.</param>
    /// <param name="workforceLocalizer">The localizer for agent workforce reports.</param>
    /// <param name="catalogOptions">The configured report catalog.</param>
    /// <param name="reportingOptions">The reporting options that bound the report range.</param>
    public ContactCenterReportProvider(
        ISession session,
        IActivityQueueManager queueManager,
        IAgentProfileManager agentManager,
        IInteractionEventStore eventStore,
        ICatalogManager<OmnichannelCampaign> campaignManager,
        IContactCenterReportCapabilityGuard capabilityGuard,
        IStringLocalizer<EnterpriseInteractionReportProvider> enterpriseLocalizer,
        IStringLocalizer<AgentWorkforceReportProvider> workforceLocalizer,
        IOptions<ContactCenterReportCatalogOptions> catalogOptions,
        IOptions<ContactCenterReportingOptions> reportingOptions)
    {
        _session = session;
        _queueManager = queueManager;
        _agentManager = agentManager;
        _eventStore = eventStore;
        _campaignManager = campaignManager;
        _capabilityGuard = capabilityGuard;
        _enterpriseLocalizer = enterpriseLocalizer;
        _workforceLocalizer = workforceLocalizer;
        _catalogOptions = catalogOptions.Value;
        _maximumReportRange = reportingOptions.Value.MaximumReportRange;
    }

    /// <inheritdoc/>
    public IEnumerable<IReport> GetReports()
    {
        foreach (var definition in _catalogOptions.EnterpriseReports)
        {
            yield return new EnterpriseInteractionReportProvider(
                _session,
                _queueManager,
                _agentManager,
                definition,
                _capabilityGuard,
                _enterpriseLocalizer,
                _maximumReportRange);
        }

        foreach (var definition in _catalogOptions.WorkforceReports)
        {
            yield return new AgentWorkforceReportProvider(
                _eventStore,
                _agentManager,
                _campaignManager,
                definition,
                _capabilityGuard,
                _workforceLocalizer);
        }
    }
}
