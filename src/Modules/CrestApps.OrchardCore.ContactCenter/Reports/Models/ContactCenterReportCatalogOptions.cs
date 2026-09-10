namespace CrestApps.OrchardCore.ContactCenter.Reports.Models;

/// <summary>
/// Holds the declarative catalog of Contact Center enterprise interaction and agent workforce report
/// definitions. Definitions are contributed through the options pipeline so additional reports can be added
/// (or existing ones removed) by another feature without editing service-registration code, and a single
/// provider projects them into individual reports.
/// </summary>
internal sealed class ContactCenterReportCatalogOptions
{
    /// <summary>
    /// Gets the enterprise interaction report definitions in the catalog.
    /// </summary>
    public IList<EnterpriseInteractionReportDefinition> EnterpriseReports { get; } = [];

    /// <summary>
    /// Gets the agent workforce report definitions in the catalog.
    /// </summary>
    public IList<AgentWorkforceReportDefinition> WorkforceReports { get; } = [];
}
