using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.Hosting;

/// <summary>
/// The default <see cref="IDetachedWorkExecutor"/>, which runs the operation on a scope created from
/// the root service provider so it survives the request that started it.
/// </summary>
public sealed class ServiceProviderDetachedWorkExecutor : IDetachedWorkExecutor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceProviderDetachedWorkExecutor"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to create the scope.</param>
    /// <param name="logger">The logger.</param>
    public ServiceProviderDetachedWorkExecutor(IServiceProvider serviceProvider, ILogger<ServiceProviderDetachedWorkExecutor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public void Run(Func<IServiceProvider, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        // Deliberately not awaited. Nothing is left to observe the returned task, so the failure path
        // has to be handled here or it would be lost entirely.
        _ = Task.Run(async () =>
        {
            await using var scope = _serviceProvider.CreateAsyncScope();

            try
            {
                await operation(scope.ServiceProvider);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Detached work failed.");
            }
        });
    }
}
