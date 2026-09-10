namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Represents a failure to acquire the per-channel create-serialization lock within the configured bounded
/// window while creating an Asterisk channel-tenant binding. This is a distinct, ambiguous outcome from the
/// <see langword="false"/> "lost the create race" result: the outcome of the create is unknown because the
/// stripe was held by another in-flight create (including an unrelated colliding channel) that did not
/// complete in time, so the caller must route to its reconcile path rather than assume another attempt owns
/// the channel.
/// </summary>
internal sealed class AsteriskChannelBindingCreateTimeoutException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskChannelBindingCreateTimeoutException"/> class.
    /// </summary>
    public AsteriskChannelBindingCreateTimeoutException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskChannelBindingCreateTimeoutException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public AsteriskChannelBindingCreateTimeoutException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskChannelBindingCreateTimeoutException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public AsteriskChannelBindingCreateTimeoutException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskChannelBindingCreateTimeoutException"/> class for
    /// a specific channel and acquisition window.
    /// </summary>
    /// <param name="channelId">The channel whose create-serialization lock could not be acquired.</param>
    /// <param name="timeout">The bounded window that elapsed before the lock could be acquired.</param>
    public AsteriskChannelBindingCreateTimeoutException(
        string channelId,
        TimeSpan timeout)
        : base($"Timed out after {timeout} acquiring the create-serialization lock for channel '{channelId}'. The create outcome is unknown; route to reconciliation instead of assuming another attempt owns the channel.")
    {
        ChannelId = channelId;
        Timeout = timeout;
    }

    /// <summary>
    /// Gets the channel whose create-serialization lock could not be acquired within the bounded window.
    /// </summary>
    public string ChannelId { get; }

    /// <summary>
    /// Gets the bounded window that elapsed before the create-serialization lock could be acquired.
    /// </summary>
    public TimeSpan Timeout { get; }
}
