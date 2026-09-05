using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

public class OmnichannelHandoffTurnContextTests
{
    [Fact]
    public void Begin_SetsCurrent_AndDisposeClearsIt()
    {
        Assert.Null(OmnichannelHandoffTurnContext.Current);

        using (var scope = OmnichannelHandoffTurnContext.Begin())
        {
            Assert.NotNull(OmnichannelHandoffTurnContext.Current);
            Assert.Same(scope.Turn, OmnichannelHandoffTurnContext.Current);
        }

        Assert.Null(OmnichannelHandoffTurnContext.Current);
    }

    [Fact]
    public void RequestHandoff_WithNoActiveTurn_ReturnsFalse()
    {
        Assert.False(OmnichannelHandoffTurnContext.RequestHandoff("reason"));
    }

    [Fact]
    public async Task RequestHandoff_RecordsOnTheTurn_AcrossAnAwait()
    {
        using var scope = OmnichannelHandoffTurnContext.Begin();

        // The tool runs within the completion's async flow; the value must survive an await hop.
        await Task.Yield();

        var recorded = OmnichannelHandoffTurnContext.RequestHandoff("customer asked for a person");

        Assert.True(recorded);
        Assert.True(scope.Turn.HandoffRequested);
        Assert.Equal("customer asked for a person", scope.Turn.Reason);
    }
}
