namespace CrestApps.OrchardCore.TenantHierarchy.Core.Guards;

/// <summary>
/// Marks the asynchronous flow of a broker call. Only the broker sets it, and only the broker's own counterpart
/// tenant becomes reachable while it is set, so no other feature can borrow the broker's rights.
/// </summary>
internal static class BrokerCallContext
{
    private static readonly AsyncLocal<string> _targetTenantName = new();

    /// <summary>
    /// Gets the name of the tenant the current broker call may reach, or <see langword="null"/> outside a broker call.
    /// </summary>
    public static string TargetTenantName => _targetTenantName.Value;

    /// <summary>
    /// Runs a synchronous operation as a broker call that may reach the given tenant and returns its result.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="targetTenantName">The name of the tenant the operation may reach.</param>
    /// <param name="operation">The operation.</param>
    public static T Run<T>(string targetTenantName, Func<T> operation)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetTenantName);
        ArgumentNullException.ThrowIfNull(operation);

        var previous = _targetTenantName.Value;
        _targetTenantName.Value = targetTenantName;

        try
        {
            return operation();
        }
        finally
        {
            _targetTenantName.Value = previous;
        }
    }

    /// <summary>
    /// Runs an operation as a broker call that may reach the given tenant. The marker is restored when the operation ends.
    /// </summary>
    /// <param name="targetTenantName">The name of the tenant the operation may reach.</param>
    /// <param name="operation">The operation.</param>
    public static async Task RunAsync(string targetTenantName, Func<Task> operation)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetTenantName);
        ArgumentNullException.ThrowIfNull(operation);

        var previous = _targetTenantName.Value;
        _targetTenantName.Value = targetTenantName;

        try
        {
            await operation();
        }
        finally
        {
            _targetTenantName.Value = previous;
        }
    }

    /// <summary>
    /// Runs an operation as a broker call that may reach the given tenant and returns its result.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="targetTenantName">The name of the tenant the operation may reach.</param>
    /// <param name="operation">The operation.</param>
    public static async Task<T> RunAsync<T>(string targetTenantName, Func<Task<T>> operation)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetTenantName);
        ArgumentNullException.ThrowIfNull(operation);

        var previous = _targetTenantName.Value;
        _targetTenantName.Value = targetTenantName;

        try
        {
            return await operation();
        }
        finally
        {
            _targetTenantName.Value = previous;
        }
    }
}
