using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

public class OmnichannelHandoffHelperTests
{
    [Fact]
    public void IsHandoffEnabled_RequiresBothToggleAndQueue()
    {
        Assert.False(OmnichannelHandoffHelper.IsHandoffEnabled(null));
        Assert.False(OmnichannelHandoffHelper.IsHandoffEnabled(new SubjectFlowSettings { EnableAgentHandoff = false, HandoffQueueId = "q1" }));
        Assert.False(OmnichannelHandoffHelper.IsHandoffEnabled(new SubjectFlowSettings { EnableAgentHandoff = true, HandoffQueueId = null }));
        Assert.False(OmnichannelHandoffHelper.IsHandoffEnabled(new SubjectFlowSettings { EnableAgentHandoff = true, HandoffQueueId = "  " }));
        Assert.True(OmnichannelHandoffHelper.IsHandoffEnabled(new SubjectFlowSettings { EnableAgentHandoff = true, HandoffQueueId = "q1" }));
    }

    [Fact]
    public void BuildHandoffInstructions_WhenDisabled_ReturnsNull()
    {
        var flow = new SubjectFlowSettings { EnableAgentHandoff = false, HandoffQueueId = "q1", HandoffOnUserRequest = true };

        Assert.Null(OmnichannelHandoffHelper.BuildHandoffInstructions(flow));
    }

    [Fact]
    public void BuildHandoffInstructions_WhenNoTriggerSelected_ReturnsNull()
    {
        var flow = new SubjectFlowSettings
        {
            EnableAgentHandoff = true,
            HandoffQueueId = "q1",
            HandoffOnUserRequest = false,
            HandoffOnQualifiedLead = false,
            HandoffOnFrustration = false,
        };

        Assert.Null(OmnichannelHandoffHelper.BuildHandoffInstructions(flow));
    }

    [Fact]
    public void BuildHandoffInstructions_IncludesOnlySelectedTriggers_AndTheTool()
    {
        var flow = new SubjectFlowSettings
        {
            EnableAgentHandoff = true,
            HandoffQueueId = "q1",
            HandoffOnUserRequest = true,
            HandoffOnQualifiedLead = false,
            HandoffOnFrustration = true,
        };

        var instructions = OmnichannelHandoffHelper.BuildHandoffInstructions(flow);

        Assert.NotNull(instructions);
        // The guidance directs the model to call the transfer tool, not to emit a marker.
        Assert.Contains("transfer_to_agent", instructions);
        Assert.Contains("asks to speak to a human", instructions);
        Assert.Contains("frustrated", instructions);
        // The qualified-lead trigger was not selected, so its guidance must not appear.
        Assert.DoesNotContain("good fit", instructions);
    }
}
