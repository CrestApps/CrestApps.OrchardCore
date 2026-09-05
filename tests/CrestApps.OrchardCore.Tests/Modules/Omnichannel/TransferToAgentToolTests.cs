using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

public class TransferToAgentToolTests
{
    [Fact]
    public async Task Invoke_RecordsHandoffOnTheScopedTurn()
    {
        // The tool resolves the turn from the completion's own scope, so the handler that opened that scope reads
        // back exactly the decision this invocation recorded.
        var tool = new TransferToAgentTool();
        var turn = new OmnichannelHandoffTurn();
        var services = new ServiceCollection()
            .AddSingleton<IOmnichannelHandoffTurn>(turn)
            .BuildServiceProvider();

        var arguments = new AIFunctionArguments(new Dictionary<string, object>
        {
            ["reason"] = "customer asked for a person",
        })
        {
            Services = services,
        };

        await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        Assert.True(turn.HandoffRequested);
        Assert.Equal("customer asked for a person", turn.Reason);
    }

    [Fact]
    public async Task Invoke_TellsTheModelTheTransferWasQueued_WhenATurnIsAvailable()
    {
        var tool = new TransferToAgentTool();
        var services = new ServiceCollection()
            .AddSingleton<IOmnichannelHandoffTurn>(new OmnichannelHandoffTurn())
            .BuildServiceProvider();

        var arguments = new AIFunctionArguments(new Dictionary<string, object>())
        {
            Services = services,
        };

        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        Assert.Contains("queued", result?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Invoke_WithNoTurnRegistered_DoesNotThrow_AndReportsUnavailable()
    {
        // A context with no turn is one where handoff is not wired up. The model is told so rather than the tool
        // failing the whole completion.
        var tool = new TransferToAgentTool();
        var services = new ServiceCollection().BuildServiceProvider();

        var arguments = new AIFunctionArguments(new Dictionary<string, object>())
        {
            Services = services,
        };

        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        Assert.Contains("not available", result?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
