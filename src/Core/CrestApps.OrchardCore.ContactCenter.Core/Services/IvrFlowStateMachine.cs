using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Walks a caller through an <see cref="IvrFlow"/>.
/// <para>
/// It is a pure function of the flow, the caller's state and one gather delivery, so the same delivery applied
/// twice lands the caller in the same place and a delivery that describes a menu they have already left is
/// ignored. Provider gather events are at-least-once and can arrive out of order; without both properties a
/// redelivery takes a caller two levels deeper than they chose, and a late one drags them back to the top.
/// </para>
/// </summary>
public static class IvrFlowStateMachine
{
    /// <summary>
    /// Returns the first thing the caller should hear.
    /// </summary>
    /// <param name="flow">The flow attached to the entry point.</param>
    /// <param name="state">The caller's state, which this updates.</param>
    public static IvrStep Start(IvrFlow flow, IvrFlowState state)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(state);

        var root = FindNode(flow, flow.RootNodeId);

        // No menu means this entry point behaves exactly as it did before flows existed.
        if (root is null)
        {
            return IvrStep.Done;
        }

        state.CurrentNodeId = root.NodeId;
        state.Attempts = 0;

        return Prompt(root);
    }

    /// <summary>
    /// Applies a gather delivery and returns what the caller should hear or be routed to next.
    /// </summary>
    /// <param name="flow">The flow attached to the entry point.</param>
    /// <param name="state">The caller's state, which this updates.</param>
    /// <param name="digits">What the caller pressed, or null when they pressed nothing.</param>
    /// <param name="deliveryId">The provider's identifier for this delivery.</param>
    /// <param name="forNodeId">
    /// The menu the delivery was collected on, when the provider reports it. A delivery for a menu the caller has
    /// already left is stale and is ignored.
    /// </param>
    public static IvrStep Advance(IvrFlow flow, IvrFlowState state, string digits, string deliveryId, string forNodeId = null)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(state);

        var node = FindNode(flow, state.CurrentNodeId);

        if (node is null)
        {
            return IvrStep.Done;
        }

        // Already applied. Returning what the caller is currently hearing — rather than nothing — means a
        // redelivered webhook re-plays the prompt instead of leaving the line silent.
        if (!string.IsNullOrEmpty(deliveryId) && state.AppliedDeliveryIds.Contains(deliveryId, StringComparer.Ordinal))
        {
            return Prompt(node);
        }

        // A delivery collected on a menu the caller has since left describes a decision they have already made
        // and moved past; acting on it would send them backwards.
        if (!string.IsNullOrEmpty(forNodeId) && !string.Equals(forNodeId, state.CurrentNodeId, StringComparison.Ordinal))
        {
            return IvrStep.Ignored;
        }

        if (!string.IsNullOrEmpty(deliveryId))
        {
            state.AppliedDeliveryIds.Add(deliveryId);
        }

        var option = string.IsNullOrWhiteSpace(digits)
            ? null
            : node.Options.FirstOrDefault(candidate => string.Equals(candidate?.Digit, digits.Trim(), StringComparison.Ordinal));

        if (option?.Action is null)
        {
            return Retry(flow, state, node);
        }

        return Apply(flow, state, option.Action, node);
    }

    private static IvrStep Apply(IvrFlow flow, IvrFlowState state, IvrAction action, IvrNode node)
    {
        switch (action.Kind)
        {
            case IvrActionKind.SubMenu:
                var child = FindNode(flow, action.TargetId);

                if (child is null)
                {
                    return Retry(flow, state, node);
                }

                state.CurrentNodeId = child.NodeId;

                // Attempts belong to the menu the caller is on.
                state.Attempts = 0;

                return Prompt(child);

            case IvrActionKind.Repeat:
                state.Attempts = 0;

                return Prompt(node);

            case IvrActionKind.RouteToQueue:
                return new IvrStep(IvrStepKind.RouteToQueue, node.NodeId, null, null, action.TargetId);

            case IvrActionKind.RouteToAgent:
                return new IvrStep(IvrStepKind.RouteToAgent, node.NodeId, null, null, action.TargetId);

            case IvrActionKind.Voicemail:
                return new IvrStep(IvrStepKind.Voicemail, node.NodeId, null, null, action.TargetId);

            case IvrActionKind.ExternalTransfer:
                return new IvrStep(IvrStepKind.ExternalTransfer, node.NodeId, null, null, action.TargetId);

            default:
                return Retry(flow, state, node);
        }
    }

    private static IvrStep Retry(IvrFlow flow, IvrFlowState state, IvrNode node)
    {
        state.Attempts++;

        if (state.Attempts < Math.Max(1, flow.MaxRetries))
        {
            return Prompt(node);
        }

        // Out of retries. A caller who cannot work the menu still has to reach somebody, so the fallback is
        // taken rather than the prompt repeating until they hang up.
        return flow.FallbackAction is null
            ? IvrStep.Done
            : Apply(flow, state, flow.FallbackAction, node);
    }

    private static IvrStep Prompt(IvrNode node)
        => new(IvrStepKind.Prompt, node.NodeId, node.Prompt, node.PromptMediaId, null);

    private static IvrNode FindNode(IvrFlow flow, string nodeId)
        => string.IsNullOrEmpty(nodeId)
            ? null
            : flow.Nodes.FirstOrDefault(node => string.Equals(node?.NodeId, nodeId, StringComparison.Ordinal));
}
