using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Reports;

/// <summary>
/// Provides the shared localizer of the Omnichannel reports contributed to the admin Reports area. A report derives
/// from it and implements <see cref="IReport"/> with its static members.
/// </summary>
public abstract class OmnichannelReportBase : IReportRunner
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
    public abstract Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default);
}
