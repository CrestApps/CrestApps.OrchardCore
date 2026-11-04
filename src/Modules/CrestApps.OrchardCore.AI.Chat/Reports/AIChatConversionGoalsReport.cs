using System.Globalization;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Chat.Reports;

/// <summary>
/// How an AI profile's chat sessions scored against its conversion goals: a row per scored session that started in
/// the period, with the total and a column per goal. It needs an AI profile, since each profile has its own goals.
/// </summary>
public sealed class AIChatConversionGoalsReport : IReport, IReportFilterMetadata
{
    private readonly IAIChatSessionEventReader _reader;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatConversionGoalsReport"/> class.
    /// </summary>
    /// <param name="reader">Reads the session events.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIChatConversionGoalsReport(IAIChatSessionEventReader reader, IStringLocalizer<AIChatConversionGoalsReport> stringLocalizer)
    {
        _reader = reader;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the technical name of the report.
    /// </summary>
    public static string Name => "ai-chat-conversion-goals";

    /// <summary>
    /// Gets the display name of the report.
    /// </summary>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<AIChatConversionGoalsReport>("AI Chat Conversion Goals");

    /// <summary>
    /// Gets the description of the report.
    /// </summary>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<AIChatConversionGoalsReport>("The points each chat session of an AI profile scored against its conversion goals.");

    /// <summary>
    /// Gets the category of the report.
    /// </summary>
    public static string Category => ReportsConstants.Categories.AIReports;

    /// <summary>
    /// Gets the permission that runs and exports the report.
    /// </summary>
    public static Permission Permission => ChatAnalyticsPermissionProvider.ViewChatAnalytics;

    /// <inheritdoc/>
    public IReadOnlyCollection<string> FilterNames { get; } = [AIChatReportFilters.RequiredProfile];

    /// <inheritdoc/>
    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var profileId = context.Filter.GetOrDefault<string>(AIChatReportFilters.ProfileId);

        if (string.IsNullOrEmpty(profileId))
        {
            return AIChatReportSections.ChooseProfile(S["Conversion goals"].Value, S["Choose an AI profile to see how its sessions scored."].Value);
        }

        var range = context.Filter.GetDateRange();
        var events = await _reader.ListAsync(new AIChatSessionEventQuery
        {
            ProfileId = profileId,
            FromUtc = range.FromUtc,
            ToUtc = range.ToUtc,
        }, cancellationToken);

        var scored = events
            .Where(evt => evt.ConversionGoalResults.Count > 0 || evt.ConversionScore.HasValue)
            .OrderByDescending(evt => evt.SessionStartedUtc)
            .ToList();

        if (scored.Count == 0)
        {
            return new ReportDocument();
        }

        var goals = scored
            .SelectMany(evt => evt.ConversionGoalResults.Select(result => result.Name))
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ReportDocument()
            .Add(ReportSection.ForMetrics(S["Summary"].Value,
            [
                new ReportMetric(S["Sessions scored"].Value, ReportFormat.Number(scored.Count)),
                new ReportMetric(S["Goals"].Value, ReportFormat.Number(goals.Count)),
            ]))
            .Add(ReportSection.ForTable(S["Conversion goals"].Value,
            [
                new ReportColumn(S["Session started (UTC)"].Value),
                new ReportColumn(S["Session"].Value),
                new ReportColumn(S["Total points"].Value, ReportColumnAlign.End),
                .. goals.Select(goal => new ReportColumn(goal, ReportColumnAlign.End)),
            ],
            scored.Select(evt => Row(evt, goals))));
    }

    private static ReportRow Row(AIChatSessionEvent evt, IReadOnlyList<string> goals)
    {
        var results = evt.ConversionGoalResults
            .Where(result => !string.IsNullOrEmpty(result.Name))
            .GroupBy(result => result.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return new ReportRow(
        [
            evt.SessionStartedUtc.ToString("o", CultureInfo.InvariantCulture),
            evt.SessionId,
            Points(evt.ConversionScore, evt.ConversionMaxScore),
            .. goals.Select(goal => results.TryGetValue(goal, out var result) ? Points(result.Score, result.MaxScore) : string.Empty),
        ]);
    }

    private static string Points(int? score, int? maxScore)
    {
        if (!score.HasValue)
        {
            return string.Empty;
        }

        return maxScore is > 0
            ? $"{score.Value.ToString(CultureInfo.InvariantCulture)} / {maxScore.Value.ToString(CultureInfo.InvariantCulture)}"
            : score.Value.ToString(CultureInfo.InvariantCulture);
    }
}
