namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Executes a telephony provider mutation with a bounded server-owned cancellation token.
/// </summary>
public interface ITelephonyCommandExecutor
{
    /// <summary>
    /// Executes a telephony provider mutation independently of caller or connection cancellation.
    /// </summary>
    /// <typeparam name="TResult">The operation result type.</typeparam>
    /// <param name="operation">The operation that receives the bounded server-owned cancellation token.</param>
    /// <returns>The operation result.</returns>
    /// <exception cref="TimeoutException">Thrown when the operation is not confirmed before the server-owned deadline expires.</exception>
    /// <exception cref="TelephonyCommandNotAdmittedException">Thrown when the host is shutting down before the operation is dispatched; the provider is never contacted, so the command is guaranteed not to have been applied.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is interrupted after dispatch because the host is shutting down; the provider outcome is indeterminate.</exception>
    Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> operation);

    /// <summary>
    /// Executes a telephony provider mutation independently of caller or connection cancellation, and names it for
    /// the <c>crestapps.telephony.command.duration</c> metric and span.
    /// </summary>
    /// <typeparam name="TResult">The operation result type.</typeparam>
    /// <param name="providerName">The technical name of the provider the command goes to, or <see langword="null"/>
    /// when it is not known here.</param>
    /// <param name="command">The command, from a bounded set such as <c>Dial</c> or <c>Hold</c>; never an id.</param>
    /// <param name="operation">The operation that receives the bounded server-owned cancellation token.</param>
    /// <returns>The operation result.</returns>
    /// <exception cref="TimeoutException">Thrown when the operation is not confirmed before the server-owned deadline expires.</exception>
    /// <exception cref="TelephonyCommandNotAdmittedException">Thrown when the host is shutting down before the operation is dispatched.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is interrupted after dispatch because the host is shutting down.</exception>
    Task<TResult> ExecuteAsync<TResult>(string providerName, string command, Func<CancellationToken, Task<TResult>> operation)
        => ExecuteAsync(operation);
}
