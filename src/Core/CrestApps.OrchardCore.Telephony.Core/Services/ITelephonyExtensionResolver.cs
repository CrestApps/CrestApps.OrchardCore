namespace CrestApps.OrchardCore.Telephony.Core.Services;

/// <summary>
/// Resolves a dialed internal extension number to the on-platform user it rings. This is the provider-neutral
/// half of extension calling: it turns a dialed number into a target user id, which a provider then maps to its
/// own live endpoint.
/// </summary>
public interface ITelephonyExtensionResolver
{
    /// <summary>
    /// Resolves a dialed extension number to its target user.
    /// </summary>
    /// <param name="number">The dialed extension number.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resolution result. <see cref="ExtensionResolution.Found"/> is <see langword="false"/> when
    /// no enabled extension owns the number.</returns>
    Task<ExtensionResolution> ResolveAsync(string number, CancellationToken cancellationToken = default);
}

/// <summary>
/// The outcome of resolving a dialed extension number.
/// </summary>
public sealed class ExtensionResolution
{
    /// <summary>
    /// Gets a value indicating whether an enabled extension was found for the dialed number.
    /// </summary>
    public bool Found { get; init; }

    /// <summary>
    /// Gets the dialed number that was resolved.
    /// </summary>
    public string Number { get; init; }

    /// <summary>
    /// Gets the identifier of the user the extension rings.
    /// </summary>
    public string UserId { get; init; }

    /// <summary>
    /// Gets the user name of the user the extension rings.
    /// </summary>
    public string UserName { get; init; }

    /// <summary>
    /// Gets the display name shown to the caller.
    /// </summary>
    public string DisplayName { get; init; }

    /// <summary>
    /// Creates a not-found resolution for the given number.
    /// </summary>
    /// <param name="number">The dialed number.</param>
    /// <returns>A not-found resolution.</returns>
    public static ExtensionResolution NotFound(string number)
        => new() { Found = false, Number = number };
}
