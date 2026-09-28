namespace CrestApps.OrchardCore.Telephony.Core.Services;

/// <summary>
/// Default <see cref="ITelephonyExtensionResolver"/> backed by the extension registry.
/// </summary>
public sealed class TelephonyExtensionResolver : ITelephonyExtensionResolver
{
    private readonly ITelephonyExtensionStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyExtensionResolver"/> class.
    /// </summary>
    /// <param name="store">The extension store.</param>
    public TelephonyExtensionResolver(ITelephonyExtensionStore store)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<ExtensionResolution> ResolveAsync(string number, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return ExtensionResolution.NotFound(number);
        }

        var extension = await _store.FindByNumberAsync(number, cancellationToken);

        if (extension is null || string.IsNullOrWhiteSpace(extension.UserId))
        {
            return ExtensionResolution.NotFound(number);
        }

        return new ExtensionResolution
        {
            Found = true,
            Number = number.Trim(),
            UserId = extension.UserId,
            UserName = extension.UserName,
            DisplayName = string.IsNullOrWhiteSpace(extension.DisplayName) ? extension.UserName : extension.DisplayName,
        };
    }
}
