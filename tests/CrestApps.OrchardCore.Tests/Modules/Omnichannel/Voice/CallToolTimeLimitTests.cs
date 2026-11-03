using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Handlers;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

public sealed class CallToolTimeLimitTests
{
    [Fact]
    public async Task TimeLimitedFunction_TellsTheModelWhenTheToolIsTooSlow()
    {
        // Arrange
        var slow = AIFunctionFactory.Create(async (CancellationToken token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), token);

            return "never";
        }, "lookup");
        var function = new TimeLimitedAIFunction(slow, TimeSpan.FromMilliseconds(50));

        // Act
        var result = await function.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TimeLimitedAIFunction.TimedOutResult("lookup", TimeSpan.FromMilliseconds(50)), result?.ToString());
    }

    [Fact]
    public async Task TimeLimitedFunction_ReturnsTheAnswerOfAQuickTool_AndReportsItsRun()
    {
        // Arrange
        var started = new List<string>();
        var finished = new List<string>();
        var quick = AIFunctionFactory.Create(() => "42", "lookup");
        var function = new TimeLimitedAIFunction(quick, TimeSpan.FromSeconds(5), started.Add, finished.Add);

        // Act
        var result = await function.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("42", result?.ToString());
        Assert.Equal(["lookup"], started);
        Assert.Equal(["lookup"], finished);
    }

    [Theory]
    [InlineData(20, 20)]
    [InlineData(0, -1)]
    public void TimeLimitedFunction_UsesTheToolsOwnLimit(int toolSeconds, int expectedSeconds)
    {
        // Arrange
        var inner = AIFunctionFactory.Create(() => "ok", new AIFunctionFactoryOptions
        {
            Name = "lookup",
            AdditionalProperties = new Dictionary<string, object>
            {
                [TimeLimitedAIFunction.TimeoutSecondsProperty] = toolSeconds,
            },
        });

        // Act
        var function = new TimeLimitedAIFunction(inner, TimeSpan.FromSeconds(8));

        // Assert
        Assert.Equal(expectedSeconds < 0 ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(expectedSeconds), function.Limit);
    }

    [Fact]
    public async Task LimitRunTime_WrapsProfileToolsButNotSystemTools_AndLeavesTheRegistryEntriesAlone()
    {
        // Arrange
        var lookup = AIFunctionFactory.Create(() => "ok", "lookup");
        var profileEntry = new ToolRegistryEntry
        {
            Id = "lookup",
            Name = "lookup",
            Source = ToolRegistryEntrySource.Local,
            CreateAsync = _ => ValueTask.FromResult<AITool>(lookup),
        };
        var systemEntry = AIConversationTools.SystemTool("endCall", "Ends the call.");
        var context = new AICompletionContext();
        context.AdditionalProperties[FunctionInvocationAICompletionServiceHandler.ScopedEntriesKey] = new List<ToolRegistryEntry> { profileEntry, systemEntry };

        // Act
        var limited = AIConversationTools.LimitRunTime(context, TimeSpan.FromSeconds(8));
        var tool = await limited[0].CreateAsync(null);

        // Assert
        Assert.IsType<TimeLimitedAIFunction>(tool);
        Assert.NotSame(profileEntry, limited[0]);
        Assert.Same(systemEntry, limited[1]);
        Assert.Same(lookup, await profileEntry.CreateAsync(null));
        Assert.Same(limited, context.AdditionalProperties[FunctionInvocationAICompletionServiceHandler.ScopedEntriesKey]);
    }

    [Fact]
    public async Task ToolFiller_SpeaksOnceWhenAToolRunsLong()
    {
        // Arrange
        var spoken = 0;
        using var filler = new VoiceAgentConversationLoop.ToolFiller(_ =>
        {
            Interlocked.Increment(ref spoken);

            return Task.CompletedTask;
        }, TimeSpan.FromMilliseconds(20));

        // Act
        filler.Started("lookup");
        filler.Started("lookup");
        await filler.Pending;

        // Assert
        Assert.True(filler.Spoken);
        Assert.Equal(1, spoken);
    }

    [Fact]
    public async Task ToolFiller_StaysQuietWhenTheToolAnswersInTime()
    {
        // Arrange
        var spoken = 0;
        using var filler = new VoiceAgentConversationLoop.ToolFiller(_ =>
        {
            spoken++;

            return Task.CompletedTask;
        }, TimeSpan.FromMilliseconds(50));

        // Act
        filler.Started("lookup");
        filler.Finished("lookup");
        await filler.Pending;

        // Assert
        Assert.False(filler.Spoken);
        Assert.Equal(0, spoken);
    }

    [Fact]
    public void ToolFillers_SkipOnlyTheEndOfTheFillerLine()
    {
        // Arrange
        TurnBasedToolFillers.Clear();
        var now = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

        // Act
        TurnBasedToolFillers.Spoke("call-1", now);

        // Assert
        Assert.False(TurnBasedToolFillers.TryClaim("call-2", now));
        Assert.True(TurnBasedToolFillers.TryClaim("call-1", now));
        Assert.False(TurnBasedToolFillers.TryClaim("call-1", now));
    }

    [Fact]
    public void ToolFillers_ForgetALineNobodyClaimed()
    {
        // Arrange
        TurnBasedToolFillers.Clear();
        var now = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);
        TurnBasedToolFillers.Spoke("call-3", now);

        // Act
        var claimed = TurnBasedToolFillers.TryClaim("call-3", now.AddMinutes(5));

        // Assert
        Assert.False(claimed);
    }

    [Theory]
    [InlineData(null, "One moment, please.")]
    [InlineData("es-MX", "Un momento, por favor.")]
    [InlineData("de", "Einen Moment, bitte.")]
    [InlineData("ja-JP", "One moment, please.")]
    public void FillerLine_IsInTheCallsLanguage(string language, string expected)
        => Assert.Equal(expected, VoiceAgentConversationLoop.FillerLine(language));
}
