using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// An entry point could map a number to exactly one queue, so every caller to a shared line reached the same
/// team and was transferred by hand. The flow is a small declarative tree, and the machine that walks it is
/// driven by provider gather events — which are at-least-once and can arrive out of order, so the same key press
/// delivered twice must not advance the caller two steps, and a stale delivery must not drag them backwards.
/// </summary>
public sealed class IvrFlowStateMachineTests
{
    [Fact]
    public void AFlowWithNoMenu_RoutesStraightToTheConfiguredTarget()
    {
        // Arrange
        // Existing tenants have no IVR. They must keep behaving exactly as they did, which is what an implicit
        // single-action flow expresses.
        var flow = new IvrFlow();
        var state = new IvrFlowState();

        // Act
        var step = IvrFlowStateMachine.Start(flow, state);

        // Assert
        Assert.Equal(IvrStepKind.Done, step.Kind);
        Assert.Null(step.NodeId);
    }

    [Fact]
    public void Start_PromptsWithTheRootMenu()
    {
        // Arrange
        var flow = Flow();
        var state = new IvrFlowState();

        // Act
        var step = IvrFlowStateMachine.Start(flow, state);

        // Assert
        Assert.Equal(IvrStepKind.Prompt, step.Kind);
        Assert.Equal("root", step.NodeId);
        Assert.Equal("Press 1 for sales, 2 for support.", step.Prompt);
    }

    [Fact]
    public void APressedDigit_TakesTheMatchingAction()
    {
        // Arrange
        var flow = Flow();
        var state = new IvrFlowState { CurrentNodeId = "root" };

        // Act
        var step = IvrFlowStateMachine.Advance(flow, state, "1", deliveryId: "g1");

        // Assert
        Assert.Equal(IvrStepKind.RouteToQueue, step.Kind);
        Assert.Equal("sales", step.TargetId);
    }

    [Fact]
    public void ASubMenuDigit_PromptsTheChildMenu()
    {
        // Arrange
        var flow = Flow();
        var state = new IvrFlowState { CurrentNodeId = "root" };

        // Act
        var step = IvrFlowStateMachine.Advance(flow, state, "2", deliveryId: "g1");

        // Assert
        Assert.Equal(IvrStepKind.Prompt, step.Kind);
        Assert.Equal("support", step.NodeId);
        Assert.Equal("support", state.CurrentNodeId);
    }

    [Fact]
    public void TheSameGatherDeliveredTwice_AdvancesTheCallerOnce()
    {
        // Arrange
        // Provider webhooks are at-least-once. Acting twice on one key press would take a caller who chose
        // "support" two levels deep into a menu they never navigated.
        var flow = Flow();
        var state = new IvrFlowState { CurrentNodeId = "root" };

        // Act
        var first = IvrFlowStateMachine.Advance(flow, state, "2", deliveryId: "g1");
        var second = IvrFlowStateMachine.Advance(flow, state, "2", deliveryId: "g1");

        // Assert
        Assert.Equal("support", state.CurrentNodeId);
        Assert.Equal(first.Kind, second.Kind);
        Assert.Equal(first.NodeId, second.NodeId);
    }

    [Fact]
    public void AStaleGatherForAnEarlierNode_DoesNotDragTheCallerBackwards()
    {
        // Arrange
        // A delivery that arrives after the caller has already moved on describes a menu they have left. Acting
        // on it would send somebody who is deep in the support menu back to the top.
        var flow = Flow();
        var state = new IvrFlowState { CurrentNodeId = "root" };

        IvrFlowStateMachine.Advance(flow, state, "2", deliveryId: "g1");

        // Act
        var stale = IvrFlowStateMachine.Advance(flow, state, "1", deliveryId: "g0", forNodeId: "root");

        // Assert
        Assert.Equal(IvrStepKind.Ignored, stale.Kind);
        Assert.Equal("support", state.CurrentNodeId);
    }

    [Fact]
    public void AnUnmappedDigit_RepromptsRatherThanDroppingTheCaller()
    {
        // Arrange
        var flow = Flow();
        var state = new IvrFlowState { CurrentNodeId = "root" };

        // Act
        var step = IvrFlowStateMachine.Advance(flow, state, "9", deliveryId: "g1");

        // Assert
        Assert.Equal(IvrStepKind.Prompt, step.Kind);
        Assert.Equal("root", step.NodeId);
        Assert.Equal(1, state.Attempts);
    }

    [Fact]
    public void RepeatedWrongDigits_FallBackRatherThanLoopingForever()
    {
        // Arrange
        // A caller who cannot work the menu — a rotary phone, a bad line, a language they do not read — must
        // still reach a person. Re-prompting forever is how somebody ends up listening to a machine until they
        // give up.
        var flow = Flow(maxRetries: 2);
        var state = new IvrFlowState { CurrentNodeId = "root" };

        IvrFlowStateMachine.Advance(flow, state, "9", deliveryId: "g1");
        IvrFlowStateMachine.Advance(flow, state, "9", deliveryId: "g2");

        // Act
        var step = IvrFlowStateMachine.Advance(flow, state, "9", deliveryId: "g3");

        // Assert
        Assert.Equal(IvrStepKind.RouteToQueue, step.Kind);
        Assert.Equal("fallback", step.TargetId);
    }

    [Fact]
    public void NoInputAtAll_CountsAsAFailedAttempt()
    {
        // Arrange
        // A caller who says nothing is usually a caller who cannot, so silence has to reach the fallback by the
        // same route a wrong digit does.
        var flow = Flow(maxRetries: 1);
        var state = new IvrFlowState { CurrentNodeId = "root" };

        IvrFlowStateMachine.Advance(flow, state, digits: null, deliveryId: "g1");

        // Act
        var step = IvrFlowStateMachine.Advance(flow, state, digits: null, deliveryId: "g2");

        // Assert
        Assert.Equal(IvrStepKind.RouteToQueue, step.Kind);
        Assert.Equal("fallback", step.TargetId);
    }

    [Fact]
    public void EnteringAMenu_ResetsTheAttemptCount()
    {
        // Arrange
        // Attempts belong to the menu the caller is on. Carrying them across would punish somebody for one
        // fumbled key press at the top for the rest of the call.
        var flow = Flow();
        var state = new IvrFlowState { CurrentNodeId = "root" };

        IvrFlowStateMachine.Advance(flow, state, "9", deliveryId: "g1");

        // Act
        IvrFlowStateMachine.Advance(flow, state, "2", deliveryId: "g2");

        // Assert
        Assert.Equal(0, state.Attempts);
    }

    [Theory]
    [InlineData("1", "2", "1")]
    [InlineData("2", "2", "2")]
    [InlineData("9", "9", "9")]
    public void ReplayingTheWholeSequence_ReachesTheSameState(string first, string second, string third)
    {
        // Arrange
        // The provider may redeliver any prefix of the conversation. Replaying it must land the caller where the
        // original run did, or a retry moves somebody who was already where they wanted to be.
        var flow = Flow();

        var live = new IvrFlowState { CurrentNodeId = "root" };
        var replay = new IvrFlowState { CurrentNodeId = "root" };

        // Act
        foreach (var (digit, delivery) in new[] { (first, "g1"), (second, "g2"), (third, "g3") })
        {
            IvrFlowStateMachine.Advance(flow, live, digit, delivery);
        }

        foreach (var (digit, delivery) in new[] { (first, "g1"), (first, "g1"), (second, "g2"), (third, "g3"), (third, "g3") })
        {
            IvrFlowStateMachine.Advance(flow, replay, digit, delivery);
        }

        // Assert
        Assert.Equal(live.CurrentNodeId, replay.CurrentNodeId);
        Assert.Equal(live.Attempts, replay.Attempts);
    }

    private static IvrFlow Flow(int maxRetries = 3)
    {
        var flow = new IvrFlow
        {
            RootNodeId = "root",
            MaxRetries = maxRetries,
            FallbackAction = new IvrAction { Kind = IvrActionKind.RouteToQueue, TargetId = "fallback" },
        };

        flow.Nodes.Add(new IvrNode
        {
            NodeId = "root",
            Prompt = "Press 1 for sales, 2 for support.",
            Options =
            [
                new IvrOption { Digit = "1", Action = new IvrAction { Kind = IvrActionKind.RouteToQueue, TargetId = "sales" } },
                new IvrOption { Digit = "2", Action = new IvrAction { Kind = IvrActionKind.SubMenu, TargetId = "support" } },
            ],
        });

        flow.Nodes.Add(new IvrNode
        {
            NodeId = "support",
            Prompt = "Press 1 for billing.",
            Options =
            [
                new IvrOption { Digit = "1", Action = new IvrAction { Kind = IvrActionKind.RouteToQueue, TargetId = "billing" } },
            ],
        });

        return flow;
    }
}
