using CrestApps.OrchardCore.Reports.Models;

namespace CrestApps.OrchardCore.AI.Chat.Reports;

/// <summary>
/// Sections the AI chat reports share.
/// </summary>
internal static class AIChatReportSections
{
    /// <summary>
    /// The document of a report that needs an AI profile before it can show anything.
    /// </summary>
    /// <param name="title">The section title.</param>
    /// <param name="message">What to choose.</param>
    /// <returns>A document with one section that says so.</returns>
    public static ReportDocument ChooseProfile(string title, string message)
    {
        var section = ReportSection.ForMetrics(title, []);
        section.Description = message;

        return new ReportDocument().Add(section);
    }
}
