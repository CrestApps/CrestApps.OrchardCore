namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Thrown when a persisted Contact Center domain event cannot be brought to the schema version the running
/// code understands. The exception is deliberate: the alternative is to hand a caller a payload written to a
/// shape it does not know, which deserializes without error and produces defaults where the missing data was.
/// </summary>
public sealed class InteractionEventUpcastException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionEventUpcastException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public InteractionEventUpcastException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InteractionEventUpcastException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public InteractionEventUpcastException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
