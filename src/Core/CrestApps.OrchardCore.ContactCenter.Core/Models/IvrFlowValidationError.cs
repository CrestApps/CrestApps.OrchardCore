namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// One reason an IVR flow cannot be run as written. The kind says what is wrong and the node, digit, and target
/// say where, so the screen or import that asked can word it for its own audience.
/// </summary>
public sealed class IvrFlowValidationError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IvrFlowValidationError"/> class.
    /// </summary>
    /// <param name="kind">What is wrong.</param>
    /// <param name="nodeId">The menu the problem is on, when it is on one.</param>
    /// <param name="digit">The key the problem is on, when it is on one.</param>
    /// <param name="targetId">The identifier the problem refers to, when it refers to one.</param>
    public IvrFlowValidationError(IvrFlowValidationErrorKind kind, string nodeId = null, string digit = null, string targetId = null)
    {
        Kind = kind;
        NodeId = nodeId;
        Digit = digit;
        TargetId = targetId;
    }

    /// <summary>
    /// Gets what is wrong.
    /// </summary>
    public IvrFlowValidationErrorKind Kind { get; }

    /// <summary>
    /// Gets the menu the problem is on, when it is on one.
    /// </summary>
    public string NodeId { get; }

    /// <summary>
    /// Gets the key the problem is on, when it is on one.
    /// </summary>
    public string Digit { get; }

    /// <summary>
    /// Gets the identifier the problem refers to, when it refers to one.
    /// </summary>
    public string TargetId { get; }
}
