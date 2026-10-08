namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Thrown when an aggregate is asked to move to a state it cannot reach from the state it is in.
/// <para>
/// The transition is refused rather than recorded. A projection built from a state the domain does not admit
/// is worse than a failed operation, because nothing downstream can tell it apart from a real one: a call
/// that reports itself as held without ever having been answered is indistinguishable, to a report, a wallboard,
/// or a supervisor, from a call that genuinely is.
/// </para>
/// </summary>
public sealed class InvalidStateTransitionException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidStateTransitionException"/> class.
    /// </summary>
    /// <param name="aggregateName">The name of the aggregate that refused the transition.</param>
    /// <param name="from">The state the aggregate is in.</param>
    /// <param name="to">The state the aggregate was asked to move to.</param>
    public InvalidStateTransitionException(string aggregateName, object from, object to)
        : base($"A {aggregateName} cannot move from '{from}' to '{to}'.")
    {
        AggregateName = aggregateName;
        From = from;
        To = to;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidStateTransitionException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the refused transition.</param>
    public InvalidStateTransitionException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidStateTransitionException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the refused transition.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public InvalidStateTransitionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Gets the name of the aggregate that refused the transition.
    /// </summary>
    public string AggregateName { get; }

    /// <summary>
    /// Gets the state the aggregate was in when the transition was refused.
    /// </summary>
    public object From { get; }

    /// <summary>
    /// Gets the state the aggregate was asked to move to.
    /// </summary>
    public object To { get; }
}
