using System.Globalization;
using CrestApps.Core.AI.Chat;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Chat.Reports;

/// <summary>
/// The data an AI profile extracted from its chat sessions: a row per session that started in the period, with a
/// column per extracted field. It needs an AI profile, since each profile extracts its own fields. It starts on every
/// session, and its export is that table alone, with invariant column names, and needs the export permission.
/// </summary>
public sealed class AIChatExtractedDataReport : IReport, IReportFilterMetadata
{
    private readonly IAIChatSessionExtractedDataStore _store;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatExtractedDataReport"/> class.
    /// </summary>
    /// <param name="store">The extracted data store.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIChatExtractedDataReport(IAIChatSessionExtractedDataStore store, IStringLocalizer<AIChatExtractedDataReport> stringLocalizer)
    {
        _store = store;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the technical name of the report.
    /// </summary>
    public static string Name => "ai-chat-extracted-data";

    /// <summary>
    /// Gets the display name of the report.
    /// </summary>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<AIChatExtractedDataReport>("AI Chat Extracted Data");

    /// <summary>
    /// Gets the description of the report.
    /// </summary>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<AIChatExtractedDataReport>("The fields an AI profile extracted from each chat session that started in the period.");

    /// <summary>
    /// Gets the category of the report.
    /// </summary>
    public static string Category => ReportsConstants.Categories.AIReports;

    /// <summary>
    /// Gets the permission that runs the report.
    /// </summary>
    public static Permission Permission => ChatAnalyticsPermissionProvider.ViewChatAnalytics;

    /// <summary>
    /// Gets the permission that exports the report, beside the one that runs it.
    /// </summary>
    public static Permission ExportPermission => ChatAnalyticsPermissionProvider.ExportChatAnalytics;

    /// <summary>
    /// Gets the date range the report starts on: every session, as the extracted data page did.
    /// </summary>
    public static ReportDefaultDateRange DefaultDateRange => ReportDefaultDateRange.AllTime;

    /// <inheritdoc/>
    public IReadOnlyCollection<string> FilterNames { get; } = [AIChatReportFilters.RequiredProfile];

    /// <inheritdoc/>
    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var profileId = context.Filter.GetOrDefault<string>(AIChatReportFilters.ProfileId);

        if (string.IsNullOrEmpty(profileId))
        {
            return AIChatReportSections.ChooseProfile(S["Extracted data"].Value, S["Choose an AI profile to see the data it extracted."].Value);
        }

        var range = context.Filter.GetDateRange();
        var records = await _store.GetAsync(profileId, range.FromUtc, range.ToUtc, cancellationToken);

        var rows = records
            .OrderByDescending(record => record.SessionStartedUtc)
            .Select(record => (record.SessionId, record.SessionStartedUtc, Values: record.Values.ToDictionary(
                pair => pair.Key,
                pair => string.Join(", ", pair.Value),
                StringComparer.OrdinalIgnoreCase)))
            .ToList();
        var fields = rows
            .SelectMany(row => row.Values.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var tableRows = rows.Select(row => new ReportRow(
        [
            row.SessionStartedUtc.ToString("o", CultureInfo.InvariantCulture),
            row.SessionId,
            .. fields.Select(field => row.Values.GetValueOrDefault(field)),
        ]));

        // An export is one flat table, with the invariant columns of the extracted data page's CSV and a column per
        // field, as scripts read it, even when it has no rows.
        if (context.Mode == ReportRunMode.Export)
        {
            return new ReportDocument().Add(ReportSection.ForTable("ExtractedData",
            [
                new ReportColumn("SessionStartedUtc"),
                new ReportColumn("SessionId"),
                .. fields.Select(field => new ReportColumn(field)),
            ],
            tableRows));
        }

        if (rows.Count == 0)
        {
            return new ReportDocument();
        }

        return new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Summary"].Value,
            [
                new ReportMetric(S["Sessions"].Value, ReportFormat.Number(rows.Count)),
                new ReportMetric(S["Fields"].Value, ReportFormat.Number(fields.Count)),
            ]))
            .Add(ReportSection.ForTable(S["Extracted data"].Value,
            [
                new ReportColumn(S["Session started (UTC)"].Value),
                new ReportColumn(S["Session"].Value),
                .. fields.Select(field => new ReportColumn(field)),
            ],
            tableRows));
    }
}
