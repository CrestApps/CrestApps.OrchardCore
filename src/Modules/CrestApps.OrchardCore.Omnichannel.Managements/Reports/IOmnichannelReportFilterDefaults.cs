using CrestApps.OrchardCore.Reports.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Reports;

/// <summary>
/// Lets an Omnichannel report preset its filters the first time it is opened, before the user has chosen any. The
/// shared filter driver applies the defaults only when the filter form has not been submitted, so a user who then
/// clears a filter is obeyed. The Dialer call detail report uses it to start on the Dialer source.
/// </summary>
public interface IOmnichannelReportFilterDefaults
{
    /// <summary>
    /// Writes the report's default filter values into a filter the user has not touched yet.
    /// </summary>
    /// <param name="filter">The report filter to preset.</param>
    void ApplyDefaults(ReportFilter filter);
}
