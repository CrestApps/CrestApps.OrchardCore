using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Executes telephony provider mutations with the configured server-owned deadline.
/// </summary>
public sealed class DefaultTelephonyCommandExecutor : ITelephonyCommandExecutor
{
    private readonly TelephonyCommandOptions _options;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTelephonyCommandExecutor"/> class.
    /// </summary>
    /// <param name="options">The telephony command options.</param>
    /// <param name="hostApplicationLifetime">The host application lifetime.</param>
    public DefaultTelephonyCommandExecutor(
        IOptions<TelephonyCommandOptions> options,
        IHostApplicationLifetime hostApplicationLifetime)
    {
        _options = options.Value;
        _hostApplicationLifetime = hostApplicationLifetime;

        if (_options.Timeout < TimeSpan.FromSeconds(TelephonyCommandOptions.MinimumTimeoutSeconds) ||
            _options.Timeout > TimeSpan.FromSeconds(TelephonyCommandOptions.MaximumTimeoutSeconds))
        {
            throw new OptionsValidationException(
                nameof(TelephonyCommandOptions),
                typeof(TelephonyCommandOptions),
                ["Telephony command timeout must be between one second and two minutes."]);
        }
    }

    /// <inheritdoc/>
    public async Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        using var timeoutSource = new CancellationTokenSource(_options.Timeout);
        using var executionSource = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutSource.Token,
            _hostApplicationLifetime.ApplicationStopping);
        Task<TResult> operationTask = null;

        try
        {
            operationTask = operation(executionSource.Token);
            var result = await operationTask.WaitAsync(executionSource.Token);

            if (timeoutSource.IsCancellationRequested)
            {
                throw CreateTimeoutException();
            }

            if (_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested)
            {
                throw new OperationCanceledException(
                    "The telephony command was interrupted because the application is stopping.",
                    _hostApplicationLifetime.ApplicationStopping);
            }

            return result;
        }
        catch (OperationCanceledException exception) when (timeoutSource.IsCancellationRequested)
        {
            ObserveLateFault(operationTask);

            throw CreateTimeoutException(exception);
        }
        catch (OperationCanceledException exception)
            when (_hostApplicationLifetime.ApplicationStopping.IsCancellationRequested)
        {
            ObserveLateFault(operationTask);

            throw new OperationCanceledException(
                "The telephony command was interrupted because the application is stopping.",
                exception,
                _hostApplicationLifetime.ApplicationStopping);
        }
    }

    private TimeoutException CreateTimeoutException(Exception innerException = null)
    {
        return new TimeoutException(
            $"The telephony command exceeded the server-owned timeout of {_options.Timeout}.",
            innerException);
    }

    private static void ObserveLateFault(Task operationTask)
    {
        if (operationTask is null)
        {
            return;
        }

        _ = operationTask.ContinueWith(
            static completedTask => _ = completedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
