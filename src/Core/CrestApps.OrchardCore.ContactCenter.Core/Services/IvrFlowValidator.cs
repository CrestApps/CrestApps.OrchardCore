using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Checks that an IVR flow can actually be run before it is saved, so a caller never reaches a menu that names a
/// missing sub-menu, a key that does nothing, or a root that does not exist. The state machine copes with those
/// at run time by retrying or giving up, which is the wrong moment to find out.
/// </summary>
public static class IvrFlowValidator
{
    private const string TelephoneKeys = "0123456789*#";

    /// <summary>
    /// Validates the flow.
    /// </summary>
    /// <param name="flow">The flow.</param>
    /// <returns>Every problem found, or an empty list when the flow is runnable.</returns>
    public static IReadOnlyList<IvrFlowValidationError> Validate(IvrFlow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);

        var errors = new List<IvrFlowValidationError>();
        var nodeIds = new HashSet<string>(StringComparer.Ordinal);

        if (flow.MaxRetries < 1)
        {
            errors.Add(new IvrFlowValidationError(IvrFlowValidationErrorKind.MaxRetriesInvalid));
        }

        foreach (var node in flow.Nodes)
        {
            if (node is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(node.NodeId))
            {
                errors.Add(new IvrFlowValidationError(IvrFlowValidationErrorKind.NodeIdMissing));
            }
            else if (!nodeIds.Add(node.NodeId))
            {
                errors.Add(new IvrFlowValidationError(IvrFlowValidationErrorKind.NodeIdDuplicate, node.NodeId));
            }
        }

        if (string.IsNullOrWhiteSpace(flow.RootNodeId))
        {
            errors.Add(new IvrFlowValidationError(IvrFlowValidationErrorKind.RootNodeMissing));
        }
        else if (!nodeIds.Contains(flow.RootNodeId))
        {
            errors.Add(new IvrFlowValidationError(IvrFlowValidationErrorKind.RootNodeNotFound, targetId: flow.RootNodeId));
        }

        foreach (var node in flow.Nodes)
        {
            if (node is null || string.IsNullOrWhiteSpace(node.NodeId))
            {
                continue;
            }

            ValidateNode(node, nodeIds, errors);
        }

        if (flow.FallbackAction is not null)
        {
            ValidateAction(flow.FallbackAction, nodeIds, nodeId: null, digit: null, errors);
        }

        return errors;
    }

    private static void ValidateNode(IvrNode node, HashSet<string> nodeIds, List<IvrFlowValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(node.Prompt) && string.IsNullOrWhiteSpace(node.PromptMediaId))
        {
            errors.Add(new IvrFlowValidationError(IvrFlowValidationErrorKind.NodePromptMissing, node.NodeId));
        }

        var options = node.Options.Where(option => option is not null).ToArray();

        if (options.Length == 0)
        {
            errors.Add(new IvrFlowValidationError(IvrFlowValidationErrorKind.NodeHasNoOptions, node.NodeId));

            return;
        }

        var digits = new HashSet<string>(StringComparer.Ordinal);

        foreach (var option in options)
        {
            var digit = option.Digit?.Trim();

            if (string.IsNullOrEmpty(digit) || digit.Length != 1 || !TelephoneKeys.Contains(digit[0]))
            {
                errors.Add(new IvrFlowValidationError(IvrFlowValidationErrorKind.OptionDigitInvalid, node.NodeId, option.Digit));
            }
            else if (!digits.Add(digit))
            {
                errors.Add(new IvrFlowValidationError(IvrFlowValidationErrorKind.OptionDigitDuplicate, node.NodeId, digit));
            }

            if (option.Action is null)
            {
                errors.Add(new IvrFlowValidationError(IvrFlowValidationErrorKind.OptionActionMissing, node.NodeId, digit));

                continue;
            }

            ValidateAction(option.Action, nodeIds, node.NodeId, digit, errors);
        }
    }

    private static void ValidateAction(IvrAction action, HashSet<string> nodeIds, string nodeId, string digit, List<IvrFlowValidationError> errors)
    {
        switch (action.Kind)
        {
            case IvrActionKind.SubMenu:
                if (string.IsNullOrWhiteSpace(action.TargetId))
                {
                    errors.Add(new IvrFlowValidationError(IvrFlowValidationErrorKind.ActionTargetMissing, nodeId, digit));
                }
                else if (!nodeIds.Contains(action.TargetId))
                {
                    errors.Add(new IvrFlowValidationError(IvrFlowValidationErrorKind.SubMenuNotFound, nodeId, digit, action.TargetId));
                }

                break;

            case IvrActionKind.RouteToQueue:
            case IvrActionKind.RouteToAgent:
            case IvrActionKind.ExternalTransfer:
                if (string.IsNullOrWhiteSpace(action.TargetId))
                {
                    errors.Add(new IvrFlowValidationError(IvrFlowValidationErrorKind.ActionTargetMissing, nodeId, digit));
                }

                break;

            default:
                // Voicemail and Repeat need no destination.
                break;
        }
    }
}
