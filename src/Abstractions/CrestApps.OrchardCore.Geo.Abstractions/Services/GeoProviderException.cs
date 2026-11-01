namespace CrestApps.OrchardCore.Geo.Services;

/// <summary>
/// A provider refused or couldn't answer: no key, a refused key, a quota, or an answer it can't give.
/// </summary>
public sealed class GeoProviderException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeoProviderException"/> class.
    /// </summary>
    public GeoProviderException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GeoProviderException"/> class.
    /// </summary>
    /// <param name="message">What went wrong.</param>
    public GeoProviderException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GeoProviderException"/> class.
    /// </summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The error behind it.</param>
    public GeoProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
