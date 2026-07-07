using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Reports;

/// <summary>
/// Provides the shared category and permission for the Omnichannel reports contributed to the admin
/// Reports area.
/// </summary>
public abstract class OmnichannelReportBase : IReport
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelReportBase"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer used for the report labels.</param>
    protected OmnichannelReportBase(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the string localizer used for the report labels.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public abstract LocalizedString DisplayName { get; }

    /// <inheritdoc/>
    public abstract LocalizedString Description { get; }

    /// <inheritdoc/>
    public string Category => ReportsConstants.Categories.Omnichannel;

    /// <inheritdoc/>
    public Permission Permission => OmnichannelConstants.Permissions.ViewReports;

    /// <inheritdoc/>
    public abstract Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default);
}
