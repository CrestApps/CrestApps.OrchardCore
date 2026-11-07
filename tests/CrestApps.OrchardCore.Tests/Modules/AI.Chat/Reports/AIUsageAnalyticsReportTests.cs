using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Deployments;
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

    [Fact]
    public async Task EachTable_KeepsItsFullFootnote_AndASectionWithoutUsageSaysSo()
    {
        var document = await RunAsync(Filter(), Records(), calls: []);

        var metered = document.Sections.Single(section => section.Title == "Metered usage");
        Assert.Contains("Units a model is not billed in are shown as a dash.", metered.Description);
        Assert.Contains("providers bill them at their own rates", metered.Description);
        Assert.Contains("Audio minutes are measured up to where the last recognized speech ended.", metered.Description);

        var audio = document.Sections.Single(section => section.Title == "Audio usage");
        Assert.Empty(audio.Rows);
        Assert.Contains("Text-to-speech length is known only when the provider reports it or the audio is WAV.", audio.Description);
        Assert.Equal("No speech-to-text or text-to-speech usage was found for the selected filters.", audio.EmptyMessage);

        var voice = document.Sections.Single(section => section.Title == "Voice");
        Assert.Empty(voice.Metrics);
        Assert.Contains("Audio tokens are the audio tokens the realtime provider reported for the call.", voice.Description);
        Assert.Equal("No automated voice calls were recorded for the selected filters.", voice.EmptyMessage);
    }

    [Fact]
    public async Task PricedDeployments_EstimateTheCostOfEveryTable()
    {
        var priced = new AIDeployment { Name = "chat-main" };
        priced.SetPricing("USD",
        [
            new AIDeploymentPrice { Meter = AIUsageMeters.InputTokens, Amount = 2m, Per = 1_000 },
            new AIDeploymentPrice { Meter = AIUsageMeters.OutputTokens, Amount = 4m, Per = 1_000 },
        ]);

        var records = new[]
        {
            new AICompletionUsageRecord { SessionId = "session-1", ProfileId = "profile-1", ClientName = "OpenAI", ModelName = "gpt", DeploymentName = "chat-main", OperationType = AIUsageOperationTypes.Chat, InputTokenCount = 1_000, OutputTokenCount = 500, TotalTokenCount = 1_500 },
            new AICompletionUsageRecord { SessionId = "session-2", ProfileId = "profile-2", ClientName = "OpenAI", ModelName = "gpt-mini", DeploymentName = "chat-small", OperationType = AIUsageOperationTypes.Chat, InputTokenCount = 100, OutputTokenCount = 50, TotalTokenCount = 150 },
        };

        var document = await RunAsync(Filter(), records, calls: [], deployments: [priced]);

        // 1,000 input tokens at 2 per 1,000 and 500 output tokens at 4 per 1,000; the unpriced deployment costs nothing.
        Assert.Equal("USD 4.00", Metrics(document, "Estimated cost")["USD"]);

        var metered = document.Sections.Single(section => section.Title == "Metered usage");
        Assert.Equal("Est. cost", metered.Columns[^1].Label);
        Assert.Contains(metered.Rows, row => row.Cells[^1] == "USD 4.00");
        Assert.Contains(metered.Rows, row => row.Cells[^1] == "-");

        var completions = document.Sections.Single(section => section.Title == "Chat completions by user and model");
        Assert.Contains(completions.Rows, row => row.Cells[^1] == "USD 4.00");
    }

    [Fact]
    public async Task WithoutPrices_ThereIsNoEstimatedCost()
    {
        var document = await RunAsync(Filter(), Records(), calls: []);

        Assert.DoesNotContain(document.Sections, section => section.Title == "Estimated cost");
    }

    [Fact]
    public void TheReport_StartsOnAllTime()
        => Assert.Equal(ReportDateRangePeriod.AllTime, ReportDescriptor.Create<AIUsageAnalyticsReport>().DefaultDateRange.Period);

    private static Task<ReportDocument> RunAsync(ReportFilter filter, AICompletionUsageRecord[] records, AIVoiceSessionSummaryIndex[] calls, AIDeployment[] deployments = null)
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

        var deploymentStore = new Mock<IAIDeploymentStore>();
        deploymentStore
            .Setup(store => store.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(deployments ?? []);

        var report = new AIUsageAnalyticsReport(
            usageService.Object,
            voiceStore.Object,
            profileStore.Object,
            deploymentStore.Object,
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
