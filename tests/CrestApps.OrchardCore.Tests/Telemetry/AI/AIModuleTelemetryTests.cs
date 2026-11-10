using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Core.Telemetry;
using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telemetry.AI;

/// <summary>
/// The AI modules emit the instruments and spans the observability docs list, on the <c>CrestApps.OrchardCore.AI</c>
/// meter, with bounded tags only. The names are a public contract that dashboards and alerts depend on.
/// </summary>
public sealed class AIModuleTelemetryTests
{
    [Fact]
    public void Meter_IsNamedForTheArea()
    {
        Assert.Equal("CrestApps.OrchardCore.AI", AIModuleTelemetry.Meter.Name);
        Assert.Equal("CrestApps.OrchardCore.AI", AIModuleTelemetry.ActivitySource.Name);
    }

    [Fact]
    public async Task TimeLimitedFunction_WhenTheToolIsTooSlow_CountsTheTimeoutAndFailsTheCoreToolCall()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var toolSource = "test" + Guid.NewGuid().ToString("n");
        var coreOutcomes = new ConcurrentQueue<string>();
        using var coreListener = ListenToCoreToolDuration(toolSource, coreOutcomes);

        var slow = AIFunctionFactory.Create(async (CancellationToken token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);

            return "never";
        }, "lookup");

        var function = new TimeLimitedAIFunction(slow, TimeSpan.FromMilliseconds(50))
        {
            ToolSource = toolSource,
        };

        // Act
        var result = await AIToolInvocation.InvokeAsync(function, new AIFunctionArguments(), toolSource, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TimeLimitedAIFunction.TimedOutResult("lookup", TimeSpan.FromMilliseconds(50)), result?.ToString());

        var timeout = Assert.Single(capture.Find("crestapps.ai.tool.timeouts", AIModuleTelemetry.ToolSourceTag, toolSource));
        Assert.Equal(1, timeout.Value);
        Assert.DoesNotContain("tool.name", timeout.Tags.Keys);

        // The tool hands the model a result instead of throwing, yet CrestApps.Core still counts the call as failed.
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeError, Assert.Single(coreOutcomes));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public void GetToolSource_MapsEveryRegistrySourceToTheValuesCrestAppsCoreRecords()
    {
        Assert.Equal("local", AIModuleTelemetry.GetToolSource(ToolRegistryEntrySource.Local));
        Assert.Equal("mcp", AIModuleTelemetry.GetToolSource(ToolRegistryEntrySource.McpServer));
        Assert.Equal("system", AIModuleTelemetry.GetToolSource(ToolRegistryEntrySource.System));
        Assert.Equal("agent", AIModuleTelemetry.GetToolSource(ToolRegistryEntrySource.Agent));
        Assert.Equal("a2a", AIModuleTelemetry.GetToolSource(ToolRegistryEntrySource.A2AAgent));
    }

    [Fact]
    public async Task EvaluateTurnsAsync_RecordsTheJudgeVerdictWithBoundedTagsAndTheIdsOnTheSpanOnly()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var sessionId = "session" + Guid.NewGuid().ToString("n");
        var evaluator = new Mock<IAIConversationEvaluator>();
        evaluator
            .Setup(x => x.EvaluateAsync(It.IsAny<AIConversationEvaluationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIConversationEvaluation { Score = 90, Passed = true });

        var service = new AIConversationEvaluationService(
            evaluator.Object,
            Mock.Of<IAIConversationEvaluationStore>(),
            Mock.Of<IAIChatSessionManager>(),
            Mock.Of<IAIChatSessionPromptStore>(),
            Mock.Of<IAIProfileManager>(),
            Mock.Of<IAICompletionContextBuilder>(),
            NullLogger<AIConversationEvaluationService>.Instance);

        // Act
        var evaluation = await service.EvaluateTurnsAsync(
            profile: null,
            new AIChatSession { SessionId = sessionId },
            [],
            ["Confirms the order number"],
            70,
            "Checkout review",
            instructions: "Be helpful.",
            judgeDeploymentName: null,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(evaluation);

        var span = Assert.Single(capture.Spans, activity => Equals(activity.GetTagItem("crestapps.session.id"), sessionId));
        Assert.Equal("crestapps.ai.eval.judge", span.OperationName);
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeOk, span.GetTagItem(CrestAppsOrchardCoreTelemetry.OutcomeTag));

        Assert.Contains(capture.Find("crestapps.ai.eval.runs", AIModuleTelemetry.KindTag, AIModuleTelemetry.KindJudge), measurement =>
            Equals(measurement.Tags[CrestAppsOrchardCoreTelemetry.ResultTag], AIModuleTelemetry.ResultPassed));

        Assert.Contains(capture.Find("crestapps.ai.eval.duration", AIModuleTelemetry.KindTag, AIModuleTelemetry.KindJudge), measurement =>
            Equals(measurement.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag], CrestAppsOrchardCoreTelemetry.OutcomeOk));

        // The label and the criteria are free text, so they never reach a metric.
        Assert.DoesNotContain(capture.Measurements, measurement => measurement.Tags.ContainsValue("Checkout review"));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public void RecordAlert_CountsOpenedAndResolvedIncidentsByMetric()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var metric = "Metric" + Guid.NewGuid().ToString("n");

        // Act
        AIModuleTelemetry.RecordAlert(AIModuleTelemetry.AlertsRaised, metric);
        AIModuleTelemetry.RecordAlert(AIModuleTelemetry.AlertsResolved, metric);

        // Assert
        Assert.Single(capture.Find("crestapps.ai.alerts.raised", AIModuleTelemetry.MetricTag, metric));
        Assert.Single(capture.Find("crestapps.ai.alerts.resolved", AIModuleTelemetry.MetricTag, metric));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public void RecordDocuments_CountsSourceDocumentsByProviderModeAndAction()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var provider = "Provider" + Guid.NewGuid().ToString("n");

        // Act
        AIModuleTelemetry.RecordDocuments(3, provider, AIModuleTelemetry.ModeFull, AIModuleTelemetry.ActionIndexed);
        AIModuleTelemetry.RecordDocuments(0, provider, AIModuleTelemetry.ModeFull, AIModuleTelemetry.ActionFailed);

        // Assert
        var documents = Assert.Single(capture.Find("crestapps.ai.datasource.documents", AIModuleTelemetry.ProviderTag, provider));
        Assert.Equal(3, documents.Value);
        Assert.Equal(AIModuleTelemetry.ModeFull, documents.Tags[AIModuleTelemetry.ModeTag]);
        Assert.Equal(AIModuleTelemetry.ActionIndexed, documents.Tags[AIModuleTelemetry.ActionTag]);
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Theory]
    [InlineData(null, "network")]
    [InlineData(408, "timeout")]
    [InlineData(429, "status_429")]
    [InlineData(503, "status_5xx")]
    [InlineData(409, "other")]
    public void SignedWebhookRetryReason_IsBounded(int? statusCode, string expected)
    {
        Assert.Equal(expected, SendSignedWebhookTask.GetRetryReason(statusCode));
    }

    private static MeterListener ListenToCoreToolDuration(string toolSource, ConcurrentQueue<string> outcomes)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Name == "crestapps.core.ai.tool.duration")
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };

        listener.SetMeasurementEventCallback<double>((_, _, tags, _) =>
        {
            string source = null;
            string outcome = null;

            foreach (var tag in tags)
            {
                if (tag.Key == "tool.source")
                {
                    source = tag.Value as string;
                }
                else if (tag.Key == CrestAppsOrchardCoreTelemetry.OutcomeTag)
                {
                    outcome = tag.Value as string;
                }
            }

            if (source == toolSource)
            {
                outcomes.Enqueue(outcome);
            }
        });

        listener.Start();

        return listener;
    }
}
