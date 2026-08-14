namespace CrestApps.OrchardCore.DncRegistry;

/// <summary>
/// Reports that a do-not-call registry was asked whether a number is listed and could not answer.
/// <para>
/// This is not the same as a registry answering "not listed", and the difference matters more than almost
/// any other distinction in this module. A registry that is unreachable, misconfigured, or rejecting requests
/// has told the platform nothing at all. Returning an empty result in that situation makes silence
/// indistinguishable from a clean answer, and a caller acting on it dials a number nobody ever screened. A
/// registry raises this instead, so the caller can decide — and the only defensible decision is not to call.
/// </para>
/// </summary>
public sealed class DoNotCallScreeningException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DoNotCallScreeningException"/> class.
    /// </summary>
    /// <param name="registryKey">The key of the registry that could not answer.</param>
    /// <param name="message">The message describing why the registry could not answer.</param>
    public DoNotCallScreeningException(string registryKey, string message)
        : base(message)
    {
        RegistryKey = registryKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DoNotCallScreeningException"/> class.
    /// </summary>
    /// <param name="registryKey">The key of the registry that could not answer.</param>
    /// <param name="message">The message describing why the registry could not answer.</param>
    /// <param name="innerException">The underlying failure.</param>
    public DoNotCallScreeningException(string registryKey, string message, Exception innerException)
        : base(message, innerException)
    {
        RegistryKey = registryKey;
    }

    /// <summary>
    /// Gets the key of the registry that could not answer.
    /// </summary>
    public string RegistryKey { get; }
}
