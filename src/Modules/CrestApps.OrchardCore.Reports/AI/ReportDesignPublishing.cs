using System.Globalization;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// What the tools that publish a designed report share. Publishing makes the given report the one that runs and clears
/// the builder's draft, so a tool refuses to publish a report that has unpublished changes in the builder: they would be
/// lost. It publishes on the draft's current revision, so a change someone makes meanwhile is never overwritten.
/// </summary>
internal static class ReportDesignPublishing
{
    /// <summary>
    /// Gets the revision to publish a report on, or why it cannot be published by a tool now.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="design">The published report.</param>
    /// <returns>The revision, or the problem to tell the model.</returns>
    public static async Task<(long Revision, string Problem)> GetRevisionAsync(IServiceProvider services, ReportDesign design)
    {
        var workingCopy = await services.GetRequiredService<ReportDesignHistoryService>().GetWorkingCopyAsync(design);

        if (workingCopy.HasDraft)
        {
            var when = await FormatLocalAsync(services, workingCopy.ModifiedUtc);

            return (workingCopy.Revision, $"The report '{design.DisplayText}' has unpublished changes in the Report Builder{By(workingCopy.ModifiedByName)}{when}. Publish or discard them in the builder first, so they are not lost.");
        }

        return (workingCopy.Revision, null);
    }

    /// <summary>
    /// Describes a publish that did not happen.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="result">The outcome of the publish.</param>
    /// <returns>The problem to tell the model, or <see langword="null"/> when it was published or only the save failed.</returns>
    public static async Task<string> DescribeFailureAsync(IServiceProvider services, ReportHistoryResult result)
        => result.Status switch
        {
            ReportHistoryStatus.Conflict => $"Someone else changed the report meanwhile{By(result.ModifiedByName)}{await FormatLocalAsync(services, result.ModifiedUtc)}. Read it again with getReport and retry.",
            ReportHistoryStatus.Busy => "Another change to this report is in progress. Try again in a moment.",
            ReportHistoryStatus.NotFound => "The report no longer exists. Call listReports to find it.",
            _ => null,
        };

    private static string By(string name)
        => string.IsNullOrEmpty(name) ? string.Empty : $" by {name}";

    private static async Task<string> FormatLocalAsync(IServiceProvider services, DateTime? utc)
    {
        var localClock = services.GetService<ILocalClock>();

        if (utc is null || localClock is null)
        {
            return string.Empty;
        }

        var local = await localClock.ConvertToLocalAsync(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc));

        return " on " + local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }
}
