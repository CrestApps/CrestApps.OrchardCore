namespace CrestApps.Core.Security;

/// <summary>
/// Thrown when a change to the current user's profile could not be committed.
/// </summary>
/// <remarks>
/// Its own type because callers must not report success after a failed persist: a soft-phone
/// credential that was never stored looks identical to one that was, until a call fails to connect.
/// </remarks>
public class UserPersistenceException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserPersistenceException"/> class.
    /// </summary>
    public UserPersistenceException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserPersistenceException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public UserPersistenceException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserPersistenceException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public UserPersistenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
