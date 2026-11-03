namespace CrestApps.OrchardCore.DataConnectors;

/// <summary>
/// A resource of a connection that a data set can read, such as a table, an index or a file.
/// </summary>
public sealed class DataConnectorResource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataConnectorResource"/> class.
    /// </summary>
    public DataConnectorResource()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DataConnectorResource"/> class.
    /// </summary>
    /// <param name="name">The name a data set stores to read the resource.</param>
    /// <param name="displayName">The name shown to users.</param>
    /// <param name="kind">The kind of resource, such as <c>Table</c>.</param>
    public DataConnectorResource(string name, string displayName, string kind = null)
    {
        Name = name;
        DisplayName = displayName;
        Kind = kind;
    }

    /// <summary>
    /// Gets or sets the name a data set stores to read the resource, such as <c>dbo.Customers</c>.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the name shown to users.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the kind of resource, such as <c>Table</c>, <c>View</c> or <c>Index</c>.
    /// </summary>
    public string Kind { get; set; }
}

/// <summary>
/// The result of checking a connection.
/// </summary>
public sealed class DataConnectorTestResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the external system answered.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets a message safe to show, such as the server version or why the check failed.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="message">The message to show.</param>
    /// <returns>The result.</returns>
    public static DataConnectorTestResult Success(string message = null)
        => new() { Succeeded = true, Message = message };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="message">Why the check failed, safe to show.</param>
    /// <returns>The result.</returns>
    public static DataConnectorTestResult Failure(string message)
        => new() { Succeeded = false, Message = message };
}

/// <summary>
/// An error of an external system, with a message that is safe to show and to log: it never holds a secret.
/// </summary>
public sealed class DataConnectorException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataConnectorException"/> class.
    /// </summary>
    public DataConnectorException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DataConnectorException"/> class.
    /// </summary>
    /// <param name="message">The message, safe to show.</param>
    public DataConnectorException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DataConnectorException"/> class.
    /// </summary>
    /// <param name="message">The message, safe to show.</param>
    /// <param name="innerException">The error of the external system.</param>
    public DataConnectorException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Gets or sets a value indicating whether trying again later may succeed, as after a timeout or a throttled call.
    /// </summary>
    public bool IsTransient { get; set; }
}
