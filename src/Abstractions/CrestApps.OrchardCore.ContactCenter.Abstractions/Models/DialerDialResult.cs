namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Represents the result of a dialer provider call operation.
/// </summary>
public sealed class DialerDialResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the provider accepted the dial request.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the provider call identifier returned by the provider.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the provider error code when the dial request failed.
    /// </summary>
    public string ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the provider error message when the dial request failed.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets provider-specific result metadata.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Creates a successful result for the specified provider call identifier.
    /// </summary>
    /// <param name="providerCallId">The provider call identifier.</param>
    /// <returns>A successful <see cref="DialerDialResult"/>.</returns>
    public static DialerDialResult Success(string providerCallId)
    {
        return new DialerDialResult
        {
            Succeeded = true,
            ProviderCallId = providerCallId,
        };
    }

    /// <summary>
    /// Creates a failed result with the specified error details.
    /// </summary>
    /// <param name="errorCode">The provider error code.</param>
    /// <param name="errorMessage">The provider error message.</param>
    /// <returns>A failed <see cref="DialerDialResult"/>.</returns>
    public static DialerDialResult Failure(string errorCode, string errorMessage)
    {
        return new DialerDialResult
        {
            Succeeded = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
        };
    }
}
