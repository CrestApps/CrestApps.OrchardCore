namespace CrestApps.OrchardCore.Subscriptions.Core.Exceptions;

/// <summary>
/// Represents a failure caused by required payment or subscription data not being available yet.
/// </summary>
public class DataNotFoundException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataNotFoundException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the missing data.</param>
    public DataNotFoundException(string message)
        : base(message) { }
}
