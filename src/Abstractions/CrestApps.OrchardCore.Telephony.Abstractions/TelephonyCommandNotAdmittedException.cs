namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Thrown by <see cref="ITelephonyCommandExecutor"/> when a telephony provider mutation is refused because
/// the host is shutting down. The provider is never contacted, so the command is guaranteed not to have
/// been applied and the caller may safely treat it as a definite non-application (rather than an
/// indeterminate outcome).
/// </summary>
/// <remarks>
/// Derives from <see cref="OperationCanceledException"/> so callers that already treat cancellation as an
/// indeterminate outcome continue to fail safe, while callers that want the more precise
/// "definitely not applied" signal can catch this type first.
/// </remarks>
public sealed class TelephonyCommandNotAdmittedException : OperationCanceledException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyCommandNotAdmittedException"/> class.
    /// </summary>
    public TelephonyCommandNotAdmittedException()
        : base("The telephony command was refused because the application is stopping.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyCommandNotAdmittedException"/> class with the
    /// cancellation token that triggered the refusal.
    /// </summary>
    /// <param name="cancellationToken">The shutdown token that caused the command to be refused.</param>
    public TelephonyCommandNotAdmittedException(CancellationToken cancellationToken)
        : base("The telephony command was refused because the application is stopping.", cancellationToken)
    {
    }
}
