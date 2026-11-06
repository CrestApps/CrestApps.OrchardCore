using System.Globalization;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.Services;
using CrestApps.OrchardCore.AI.Chat.ViewModels;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Chat.Reports;

/// <summary>
/// The AI usage of a period: every metered request sent to an AI provider, the audio requests, the chat completions
/// of chat sessions and chat interactions, and the automated voice calls, each table grouped as the filter asks.
/// </summary>
public sealed class AIUsageAnalyticsReport : IReport, IReportFilterMetadata
{
    private readonly IAICompletionUsageService _usageService;
    private readonly IAIVoiceSessionSummaryStore _voiceSessionStore;
    private readonly IAIProfileStore _profileStore;
    private readonly ILocalClock _localClock;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIUsageAnalyticsReport"/> class.
    /// </summary>
    /// <param name="usageService">The usage service.</param>
    /// <param name="voiceSessionStore">The per-call AI voice session summaries.</param>
    /// <param name="profileStore">The AI profile store, for profile names.</param>
    /// <param name="localClock">The local clock, for the site's time zone.</param>
    /// <param name="clock">The clock, for converting call times to the site's time zone.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AIUsageAnalyticsReport(
        IAICompletionUsageService usageService,
        IAIVoiceSessionSummaryStore voiceSessionStore,
        IAIProfileStore profileStore,
        ILocalClock localClock,
        IClock clock,
        IStringLocalizer<AIUsageAnalyticsReport> stringLocalizer)
    {
        _usageService = usageService;
        _voiceSessionStore = voiceSessionStore;
        _profileStore = profileStore;
        _localClock = localClock;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the technical name of the report.
    /// </summary>
    public static string Name => "ai-usage-analytics";

    /// <summary>
    /// Gets the display name of the report.
    /// </summary>
    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<AIUsageAnalyticsReport>("AI Usage Analytics");

    /// <summary>
    /// Gets the description of the report.
    /// </summary>
    public static LocalizationSource Description { get; } = LocalizationSource.Create<AIUsageAnalyticsReport>("Provider usage by model, user, deployment, profile and connection, audio usage, and the talk time of automated voice calls.");

    /// <summary>
    /// Gets the category of the report.
    /// </summary>
    public static string Category => ReportsConstants.Categories.BillingUsage;

    /// <summary>
    /// Gets the permission that runs and exports the report.
    /// </summary>
    public static Permission Permission => ChatAnalyticsPermissionProvider.ViewChatAnalytics;

    /// <summary>
    /// Gets the date range the report starts on: all usage, as the usage analytics page did.
    /// </summary>
    public static ReportDefaultDateRange DefaultDateRange => ReportDefaultDateRange.AllTime;

    /// <inheritdoc/>
    public IReadOnlyCollection<string> FilterNames { get; } = [AIChatReportFilters.Profile, AIChatReportFilters.UsageGrouping];

    /// <inheritdoc/>
    public async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var filter = context.Filter;
        var range = filter.GetDateRange();
        var profileId = filter.GetOrDefault<string>(AIChatReportFilters.ProfileId);
        var meteringGroupBy = filter.GetOrDefault<AIUsageReportGrouping>(AIChatReportFilters.MeteringGroupBy);
        var audioGroupBy = filter.GetOrDefault<AIAudioUsageReportGrouping>(AIChatReportFilters.AudioGroupBy);
        var groupBy = filter.GetOrDefault<AICompletionUsageGroupBy>(AIChatReportFilters.CompletionGroupBy);
        var voiceGroupBy = filter.GetOrDefault<AIVoiceUsageGroupBy>(AIChatReportFilters.VoiceGroupBy);

        var records = await _usageService.GetAsync(range.FromUtc, range.ToUtc, cancellationToken);
        var voiceSessions = await _voiceSessionStore.GetAsync(range.FromUtc, range.ToUtc, cancellationToken);

        // Metered usage covers every request sent to a provider, of every kind and from every feature, not only
        // the chat completions the completions table counts.
        var meteredRecords = string.IsNullOrEmpty(profileId)
            ? records
            : records.Where(record => string.Equals(record.ProfileId, profileId, StringComparison.Ordinal)).ToList();
        var calls = AIVoiceUsageReport.Calls(voiceSessions, profileId);

        if (meteredRecords.Count == 0 && calls.Count == 0)
        {
            return new ReportDocument();
        }

        var profileNames = await AIChatReportProfiles.GetNamesAsync(_profileStore);
        var timeZone = await _localClock.GetLocalTimeZoneAsync();

        DateTime ToLocal(DateTime utc)
            => _clock.ConvertToTimeZone(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)), timeZone).DateTime;

        var document = new ReportDocument();

        AddMetering(document, meteredRecords, meteringGroupBy);
        AddAudio(document, meteredRecords, audioGroupBy);
        AddCompletions(document, AICompletionUsageReport.Relevant(records, profileId), groupBy, profileNames);

        // A call's text tokens are the completions recorded against its chat session: the turn-based replies and
        // the review that concludes every call.
        AddVoice(
            document,
            calls,
            voiceGroupBy,
            profileNames,
            ToLocal,
            AICompletionUsageReport.TokensBySession(meteredRecords),
            AICompletionUsageReport.AudioTokensBySession(meteredRecords));

        return document;
    }

    private void AddMetering(ReportDocument document, IReadOnlyCollection<AICompletionUsageRecord> records, AIUsageReportGrouping groupBy)
    {
        var rows = AIUsageReport.Build(records, groupBy);
        var breakdown = groupBy switch
        {
            AIUsageReportGrouping.ModelAndCategory => S["Category"].Value,
            AIUsageReportGrouping.ModelAndPurpose => S["Purpose"].Value,
            _ => null,
        };

        var columns = new List<ReportColumn>
        {
            new(S["Provider"].Value),
            new(S["Model"].Value),
            new(S["Kind"].Value),
        };

        if (breakdown is not null)
        {
            columns.Add(new ReportColumn(breakdown));
        }

        columns.AddRange(
        [
            new(S["Requests"].Value, ReportColumnAlign.End),
            new(S["Input"].Value, ReportColumnAlign.End),
            new(S["Cached input"].Value, ReportColumnAlign.End),
            new(S["Output"].Value, ReportColumnAlign.End),
            new(S["Reasoning"].Value, ReportColumnAlign.End),
            new(S["Audio in"].Value, ReportColumnAlign.End),
            new(S["Audio out"].Value, ReportColumnAlign.End),
            new(S["Tokens"].Value, ReportColumnAlign.End),
            new(S["Audio minutes"].Value, ReportColumnAlign.End),
            new(S["Characters"].Value, ReportColumnAlign.End),
            new(S["Images"].Value, ReportColumnAlign.End),
            new(S["Avg latency"].Value, ReportColumnAlign.End),
        ]);

        var section = ReportSection.ForTable(S["Metered usage"].Value, columns, rows.Select(row =>
        {
            var cells = new List<string> { row.ClientName, row.ModelName, KindLabel(row.OperationType) };

            if (breakdown is not null)
            {
                cells.Add(BreakdownLabel(groupBy == AIUsageReportGrouping.ModelAndCategory ? row.ContextType : row.Purpose));
            }

            cells.AddRange(
            [
                ReportFormat.Number(row.RequestCount),
                Count(row.InputTokenCount),
                Count(row.CachedInputTokenCount),
                Count(row.OutputTokenCount),
                Count(row.ReasoningTokenCount),
                Count(row.InputAudioTokenCount),
                Count(row.OutputAudioTokenCount),
                Count(row.TotalTokenCount),
                row.AudioDurationMs > 0 ? (row.AudioDurationMs / 60000d).ToString("N1", CultureInfo.InvariantCulture) : "-",
                Count(row.CharacterCount),
                Count(row.ImageCount),
                Latency(row.AverageResponseLatencyMs),
            ]);

            return new ReportRow(cells);
        }));

        section.Description = S["{0} requests. Every request sent to an AI provider, of every kind and from every feature. Each round trip to a model is one request, so a reply that plans, searches and calls tools counts each of those calls. Units a model is not billed in are shown as a dash. Cached input, reasoning and audio tokens are part of the input and output totals; providers bill them at their own rates. Audio minutes are measured up to where the last recognized speech ended.", ReportFormat.Number(records.Count)].Value;
        section.EmptyMessage = S["No metered usage was found for the selected filters."].Value;
        document.Add(section);
    }

    private void AddAudio(ReportDocument document, IReadOnlyCollection<AICompletionUsageRecord> records, AIAudioUsageReportGrouping groupBy)
    {
        var rows = AIAudioUsageReport.Build(records, groupBy);
        var breakdown = groupBy switch
        {
            AIAudioUsageReportGrouping.ModelAndVoice => S["Voice"].Value,
            AIAudioUsageReportGrouping.ModelAndLanguage => S["Language"].Value,
            _ => null,
        };

        var columns = new List<ReportColumn>
        {
            new(S["Provider"].Value),
            new(S["Model"].Value),
            new(S["Kind"].Value),
        };

        if (breakdown is not null)
        {
            columns.Add(new ReportColumn(breakdown));
        }

        columns.AddRange(
        [
            new(S["Requests"].Value, ReportColumnAlign.End),
            new(S["Hours"].Value, ReportColumnAlign.End),
            new(S["Speech hours"].Value, ReportColumnAlign.End),
            new(S["Characters"].Value, ReportColumnAlign.End),
            new(S["Audio in"].Value, ReportColumnAlign.End),
            new(S["Audio out"].Value, ReportColumnAlign.End),
            new(S["Segments"].Value, ReportColumnAlign.End),
            new(S["Tokens"].Value, ReportColumnAlign.End),
            new(S["Avg latency"].Value, ReportColumnAlign.End),
        ]);

        var section = ReportSection.ForTable(S["Audio usage"].Value, columns, rows.Select(row =>
        {
            var cells = new List<string> { row.ClientName, row.ModelName, KindLabel(row.OperationType) };

            if (breakdown is not null)
            {
                cells.Add(groupBy == AIAudioUsageReportGrouping.ModelAndVoice ? row.VoiceId : row.Language);
            }

            var hours = row.AudioDurationMs > 0 ? row.AudioHours.ToString("N2", CultureInfo.InvariantCulture) : "-";

            if (row.AudioDurationMs > 0 && row.RequestsWithDurationCount < row.RequestCount)
            {
                hours += "*";
            }

            cells.AddRange(
            [
                ReportFormat.Number(row.RequestCount),
                hours,
                row.SpeechDurationMs > 0 ? (row.SpeechDurationMs / 3600000d).ToString("N2", CultureInfo.InvariantCulture) : "-",
                Count(row.CharacterCount),
                Bytes(row.AudioInputBytes),
                Bytes(row.AudioOutputBytes),
                Count(row.SegmentCount),
                Count(row.TotalTokenCount),
                Latency(row.AverageResponseLatencyMs),
            ]);

            return new ReportRow(cells);
        }));

        section.Description = S["{0} requests. Speech-to-text and text-to-speech requests, with the hours of audio, characters, audio size and tokens each model was billed for. An asterisk means some requests did not report an audio length, so the hours are a minimum. Speech-to-text hours run to where the last recognized speech ended; speech hours leave out the silence before the first word. Text-to-speech length is known only when the provider reports it or the audio is WAV.", ReportFormat.Number(rows.Sum(row => (long)row.RequestCount))].Value;
        section.EmptyMessage = S["No speech-to-text or text-to-speech usage was found for the selected filters."].Value;
        document.Add(section);
    }

    private void AddCompletions(
        ReportDocument document,
        IReadOnlyList<AICompletionUsageRecord> records,
        AICompletionUsageGroupBy groupBy,
        IReadOnlyDictionary<string, string> profileNames)
    {
        if (records.Count == 0)
        {
            var empty = ReportSection.ForMetrics(S["Chat completions"].Value, []);
            empty.EmptyMessage = S["No usage data was found for the selected filters."].Value;
            document.Add(empty);

            return;
        }

        document.Add(ReportSection.ForMetrics(S["Chat completions"].Value,
        [
            new ReportMetric(S["Provider calls"].Value, ReportFormat.Number(records.Count)),
            new ReportMetric(S["Chat sessions"].Value, ReportFormat.Number(records.Select(record => record.SessionId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).Count())),
            new ReportMetric(S["Chat interactions"].Value, ReportFormat.Number(records.Select(record => record.InteractionId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).Count())),
            new ReportMetric(S["Total tokens"].Value, ReportFormat.Number(records.Sum(record => (long)record.TotalTokenCount))),
        ]));

        var byUser = groupBy == AICompletionUsageGroupBy.UserAndModel;
        var columns = new List<ReportColumn>();

        if (byUser)
        {
            columns.Add(new ReportColumn(S["User"].Value));
            columns.Add(new ReportColumn(S["Authenticated"].Value));
            columns.Add(new ReportColumn(S["Client"].Value));
            columns.Add(new ReportColumn(S["Model"].Value));
        }
        else
        {
            columns.Add(new ReportColumn(groupBy switch
            {
                AICompletionUsageGroupBy.Model => S["Model"].Value,
                AICompletionUsageGroupBy.Deployment => S["Deployment"].Value,
                AICompletionUsageGroupBy.Profile => S["AI profile"].Value,
                AICompletionUsageGroupBy.Connection => S["Connection"].Value,
                _ => S["Group"].Value,
            }));
        }

        columns.AddRange(
        [
            new(S["Calls"].Value, ReportColumnAlign.End),
            new(S["Sessions"].Value, ReportColumnAlign.End),
            new(S["Interactions"].Value, ReportColumnAlign.End),
            new(S["Input"].Value, ReportColumnAlign.End),
            new(S["Output"].Value, ReportColumnAlign.End),
            new(S["Tokens"].Value, ReportColumnAlign.End),
            new(S["Avg latency"].Value, ReportColumnAlign.End),
        ]);

        document.Add(ReportSection.ForTable(S["Chat completions by {0}", GroupName(groupBy)].Value, columns,
            AICompletionUsageReport.BuildRows(records, groupBy, profileNames).Select(row => CompletionRow(row, byUser))));
    }

    private ReportRow CompletionRow(AICompletionUsageSummaryViewModel row, bool byUser)
    {
        var cells = byUser
            ? new List<string> { row.UserLabel, row.IsAuthenticated ? S["Yes"].Value : S["No"].Value, row.ClientName, row.ModelName }
            : [row.GroupLabel];

        cells.AddRange(
        [
            ReportFormat.Number(row.TotalCalls),
            ReportFormat.Number(row.TotalSessions),
            ReportFormat.Number(row.TotalChatInteractions),
            ReportFormat.Number(row.TotalInputTokens),
            ReportFormat.Number(row.TotalOutputTokens),
            ReportFormat.Number(row.TotalTokens),
            Latency(row.AverageResponseLatencyMs),
        ]);

        return new ReportRow(cells);
    }

    private void AddVoice(
        ReportDocument document,
        IReadOnlyList<AIVoiceSessionSummaryIndex> calls,
        AIVoiceUsageGroupBy groupBy,
        IReadOnlyDictionary<string, string> profileNames,
        Func<DateTime, DateTime> toLocal,
        IReadOnlyDictionary<string, long> textTokensBySession,
        IReadOnlyDictionary<string, long> audioTokensBySession)
    {
        if (calls.Count == 0)
        {
            var empty = ReportSection.ForMetrics(S["Voice"].Value, []);
            empty.Description = VoiceDescription();
            empty.EmptyMessage = S["No automated voice calls were recorded for the selected filters."].Value;
            document.Add(empty);

            return;
        }

        var totals = AIVoiceUsageReport.Summarize(label: null, calls, textTokensBySession, audioTokensBySession);

        document.Add(ReportSection.ForMetrics(S["Voice"].Value,
        [
            new ReportMetric(S["Calls"].Value, ReportFormat.Number(totals.Calls)),
            new ReportMetric(S["AI minutes"].Value, Minutes(totals.AIMinutes)),
            new ReportMetric(S["Caller minutes"].Value, Minutes(totals.CallerMinutes)),
            new ReportMetric(S["Silence minutes"].Value, Minutes(totals.SilenceMinutes)),
            new ReportMetric(S["Average call"].Value, totals.AverageSessionSeconds.HasValue ? ReportFormat.Duration(totals.AverageSessionSeconds.Value) : "-"),
            new ReportMetric(S["Handoff rate"].Value, ReportFormat.Percent(totals.HandoffRate)),
            new ReportMetric(S["Barge-ins per call"].Value, totals.BargeInsPerCall?.ToString("N2", CultureInfo.InvariantCulture) ?? "-"),
        ]));

        var rows = AIVoiceUsageReport.BuildRows(calls, groupBy, profileNames, toLocal, textTokensBySession, audioTokensBySession);
        var section = ReportSection.ForTable(S["Voice usage by {0}", VoiceGroupName(groupBy)].Value,
        [
            new ReportColumn(VoiceGroupName(groupBy)),
            new ReportColumn(S["Calls"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Call minutes"].Value, ReportColumnAlign.End),
            new ReportColumn(S["AI minutes"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Caller minutes"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Silence minutes"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Avg call"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Avg first word"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Handoff rate"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Barge-ins / call"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Idle prompts"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Text tokens"].Value, ReportColumnAlign.End),
            new ReportColumn(S["Audio tokens"].Value, ReportColumnAlign.End),
        ],
        rows.Select(row => new ReportRow(
        [
            row.Label,
            ReportFormat.Number(row.Calls),
            Minutes(row.SessionMinutes),
            Minutes(row.AIMinutes),
            Minutes(row.CallerMinutes),
            Minutes(row.SilenceMinutes),
            row.AverageSessionSeconds.HasValue ? ReportFormat.Duration(row.AverageSessionSeconds.Value) : "-",
            row.AverageTimeToFirstAudioMs.HasValue ? Latency(row.AverageTimeToFirstAudioMs.Value) : "-",
            ReportFormat.Percent(row.HandoffRate),
            row.BargeInsPerCall?.ToString("N2", CultureInfo.InvariantCulture) ?? "-",
            ReportFormat.Number(row.IdlePrompts),
            ReportFormat.Number(row.TextTokens),
            row.AudioTokens.HasValue ? ReportFormat.Number(row.AudioTokens.Value) : "-",
        ])));

        section.Description = VoiceDescription();
        document.Add(section);
    }

    private string VoiceDescription()
        => S["Automated voice calls held by an AI profile, measured from the call audio: how long the caller heard the assistant, how long the caller spoke, and how long neither did. Figures a call's engine cannot measure are left out of that figure rather than counted as zero. Text tokens are the text tokens recorded against each call's chat session: turn-based replies, the text part of realtime responses, and the review that concludes the call. Audio tokens are the audio tokens the realtime provider reported for the call."].Value;

    private string GroupName(AICompletionUsageGroupBy groupBy)
        => groupBy switch
        {
            AICompletionUsageGroupBy.Model => S["model"].Value,
            AICompletionUsageGroupBy.Deployment => S["deployment"].Value,
            AICompletionUsageGroupBy.Profile => S["AI profile"].Value,
            AICompletionUsageGroupBy.Connection => S["connection"].Value,
            _ => S["user and model"].Value,
        };

    private string VoiceGroupName(AIVoiceUsageGroupBy groupBy)
        => groupBy switch
        {
            AIVoiceUsageGroupBy.Profile => S["AI profile"].Value,
            AIVoiceUsageGroupBy.Campaign => S["Campaign"].Value,
            AIVoiceUsageGroupBy.Channel => S["Channel"].Value,
            AIVoiceUsageGroupBy.Engine => S["Engine"].Value,
            AIVoiceUsageGroupBy.Day => S["Day"].Value,
            _ => S["Deployment (model)"].Value,
        };

    private string KindLabel(string operationType)
        => operationType switch
        {
            AIUsageOperationTypes.Chat => S["Chat"].Value,
            AIUsageOperationTypes.Embedding => S["Embeddings"].Value,
            AIUsageOperationTypes.Image => S["Images"].Value,
            AIUsageOperationTypes.SpeechToText => S["Speech-to-text"].Value,
            AIUsageOperationTypes.TextToSpeech => S["Text-to-speech"].Value,
            AIUsageOperationTypes.Realtime => S["Realtime voice"].Value,
            AIUsageOperationTypes.RealtimeTranscription => S["Realtime transcription"].Value,
            _ => operationType,
        };

    private string BreakdownLabel(string value)
        => value switch
        {
            nameof(AIChatSession) => S["Chat sessions"].Value,
            nameof(ChatInteraction) => S["Chat interactions"].Value,
            AIUsageReport.Unspecified => S["Unspecified"].Value,
            _ => value,
        };

    private static string Count(long value)
        => value > 0 ? ReportFormat.Number(value) : "-";

    private static string Bytes(long value)
        => value > 0 ? (value / 1048576d).ToString("N1", CultureInfo.InvariantCulture) + " MB" : "-";

    private static string Latency(double milliseconds)
        => milliseconds > 0 ? Math.Round(milliseconds).ToString("N0", CultureInfo.InvariantCulture) + " ms" : "-";

    private static string Minutes(double? minutes)
        => minutes?.ToString("N1", CultureInfo.InvariantCulture) ?? "-";
}
