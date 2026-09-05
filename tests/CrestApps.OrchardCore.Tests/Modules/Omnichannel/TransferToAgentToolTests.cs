using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

public class TransferToAgentToolTests
{
    [Fact]
    public async Task Invoke_RecordsHandoffOnTheCurrentTurn()
    {
        var tool = new TransferToAgentTool();
        var services = new ServiceCollection().BuildServiceProvider();

        var arguments = new AIFunctionArguments(new Dictionary<string, object>
        {
            ["reason"] = "customer asked for a person",
        })
        {
            Services = services,
        };

        using var scope = OmnichannelHandoffTurnContext.Begin();

        await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        Assert.True(scope.Turn.HandoffRequested);
        Assert.Equal("customer asked for a person", scope.Turn.Reason);
    }

    [Fact]
    public async Task Invoke_WithNoActiveTurn_DoesNotThrow_AndReportsUnavailable()
    {
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
