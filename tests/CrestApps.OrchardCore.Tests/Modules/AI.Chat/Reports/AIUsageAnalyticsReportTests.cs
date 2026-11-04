using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Chat.Models;
using CrestApps.OrchardCore.AI.Chat.Reports;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Chat.Reports;

/// <summary>
/// The AI usage report, on the Reports framework: the profile filter and the groupings the owner chooses reach every
/// table, and a period without usage shows nothing.
/// </summary>
public sealed class AIUsageAnalyticsReportTests
{
    private static readonly DateTime _day = new(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task WithoutUsage_TheDocumentIsEmpty()
    {
        var document = await RunAsync(Filter(), records: [], calls: []);

        Assert.False(document.HasContent);
    }

    [Fact]
    public async Task TheProfileFilterAndTheGroupings_ApplyToCompletionsAndVoiceCalls()
    {
        var filter = Filter();
        filter.Set(AIChatReportFilters.ProfileId, "profile-1");
        filter.Set(AIChatReportFilters.CompletionGroupBy, nameof(AICompletionUsageGroupBy.Deployment));
        filter.Set(AIChatReportFilters.VoiceGroupBy, nameof(AIVoiceUsageGroupBy.Engine));

        var document = await RunAsync(filter, Records(), Calls());

        // Completions: only profile-1's, grouped by deployment.
        Assert.Equal("30", Metrics(document, "Chat completions")["Total tokens"]);
        var completions = document.Sections.Single(section => section.Title == "Chat completions by deployment");
        Assert.Equal("chat-main", Assert.Single(completions.Rows).Cells[0]);

        // Voice: only profile-1's call, grouped by engine, with its session's text tokens.
        Assert.Equal("1", Metrics(document, "Voice")["Calls"]);
        var voice = document.Sections.Single(section => section.Title == "Voice usage by Engine");
        var row = Assert.Single(voice.Rows);
        Assert.Equal("Realtime", row.Cells[0]);
        Assert.Equal("30", row.Cells[11]);

        // Every metered request of the profile, whatever its kind.
        Assert.NotNull(document.Sections.Single(section => section.Title == "Metered usage"));
    }

    [Fact]
    public async Task WithoutAProfile_EveryProfileIsCounted_GroupedByUserAndModel()
    {
        var document = await RunAsync(Filter(), Records(), Calls());

        Assert.Equal("100", Metrics(document, "Chat completions")["Total tokens"]);
        Assert.Equal("2", Metrics(document, "Voice")["Calls"]);
        Assert.Contains(document.Sections, section => section.Title == "Chat completions by user and model");
    }

    private static Task<ReportDocument> RunAsync(ReportFilter filter, AICompletionUsageRecord[] records, AIVoiceSessionSummaryIndex[] calls)
    {
        var usageService = new Mock<IAICompletionUsageService>();
        usageService
            .Setup(service => service.GetAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);

        var voiceStore = new Mock<IAIVoiceSessionSummaryStore>();
        voiceStore
            .Setup(store => store.GetAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(calls);

        var profileStore = new Mock<IAIProfileStore>();
        profileStore
            .Setup(store => store.GetByTypeAsync(AIProfileType.Chat))
            .ReturnsAsync(
            [
                new AIProfile { ItemId = "profile-1", Name = "service", DisplayText = "Service reminders" },
                new AIProfile { ItemId = "profile-2", Name = "sales", DisplayText = "Sales follow-up" },
            ]);

        var report = new AIUsageAnalyticsReport(
            usageService.Object,
            voiceStore.Object,
            profileStore.Object,
            ReportTestClocks.LocalClock(),
            ReportTestClocks.Clock(),
            new PassThroughStringLocalizer<AIUsageAnalyticsReport>());

        return report.RunAsync(new ReportContext(filter), TestContext.Current.CancellationToken);
    }

    private static AICompletionUsageRecord[] Records()
        =>
        [
            new AICompletionUsageRecord { SessionId = "session-1", ProfileId = "profile-1", DeploymentName = "chat-main", TotalTokenCount = 30 },
            new AICompletionUsageRecord { SessionId = "session-2", ProfileId = "profile-2", DeploymentName = "chat-small", TotalTokenCount = 70 },
        ];

    private static AIVoiceSessionSummaryIndex[] Calls()
        =>
        [
            new AIVoiceSessionSummaryIndex { ActivityId = "activity-1", AISessionId = "session-1", AIProfileId = "profile-1", Engine = nameof(AIVoiceSessionEngine.Realtime), Outcome = nameof(AIVoiceSessionOutcome.CompletedByAI), SessionDurationMs = 60_000 },
            new AIVoiceSessionSummaryIndex { ActivityId = "activity-2", AISessionId = "session-2", AIProfileId = "profile-2", Engine = nameof(AIVoiceSessionEngine.TurnBased), Outcome = nameof(AIVoiceSessionOutcome.CompletedByAI), SessionDurationMs = 30_000 },
        ];

    private static ReportFilter Filter()
    {
        var filter = new ReportFilter { ReportName = AIUsageAnalyticsReport.Name };
        filter.SetDateRange(new ReportDateRange { FromUtc = _day, ToUtc = _day.AddDays(1).AddTicks(-1), Key = "custom" });

        return filter;
    }

    private static Dictionary<string, string> Metrics(ReportDocument document, string title)
        => document.Sections.Single(section => section.Title == title).Metrics.ToDictionary(metric => metric.Label, metric => metric.Value);
}
