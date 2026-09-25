using CrestApps.OrchardCore.Telephony.Core.Models;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// The extensions of the phone system, each with the name of the person it rings.
/// </summary>
internal interface ITelephonyExtensionDirectory
{
    /// <summary>
    /// Lists every extension that rings somebody, by number, with the name to show for them.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<TelephonyExtensionDirectoryEntry>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the name to show for a user: how a caller placing an extension call is shown to the person it rings.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The user's display name, or <see langword="null"/> when the user is not found.</returns>
    Task<string> GetUserNameAsync(string userId, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="ITelephonyExtensionDirectory"/>
internal sealed class TelephonyExtensionDirectory : ITelephonyExtensionDirectory
{
    private readonly ITelephonyExtensionStore _store;
    private readonly ITelephonyUserDisplayNames _userDisplayNames;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyExtensionDirectory"/> class.
    /// </summary>
    /// <param name="store">The extension registry.</param>
    /// <param name="userDisplayNames">The users' display names.</param>
    public TelephonyExtensionDirectory(ITelephonyExtensionStore store, ITelephonyUserDisplayNames userDisplayNames)
    {
        _store = store;
        _userDisplayNames = userDisplayNames;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TelephonyExtensionDirectoryEntry>> ListAsync(CancellationToken cancellationToken = default)
    {
        var extensions = (await _store.GetAllAsync(cancellationToken))
            .Where(extension => !string.IsNullOrWhiteSpace(extension?.Number) && !string.IsNullOrWhiteSpace(extension.UserId))
            .ToArray();

        // Every user in one query, not one per extension.
        var names = await _userDisplayNames.GetAsync(extensions.Select(extension => extension.UserId), cancellationToken);

        return extensions
            .Select(extension => new TelephonyExtensionDirectoryEntry
            {
                Extension = extension.Number.Trim(),
                DisplayName = Describe(extension, names),
                UserName = extension.UserName,
            })
            .OrderBy(entry => entry.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(entry => entry.Extension, StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<string> GetUserNameAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var names = await _userDisplayNames.GetAsync([userId], cancellationToken);

        return names.TryGetValue(userId, out var name) ? name : null;
    }

    internal static string Describe(TelephonyExtension extension, IReadOnlyDictionary<string, string> names)
        => TelephonyExtensionNames.Choose(
            extension.DisplayName,
            extension.UserName,
            !string.IsNullOrEmpty(extension.UserId) && names.TryGetValue(extension.UserId, out var name) ? name : null,
            extension.Number);
}

/// <summary>
/// Resolves extensions through the registry, naming the person each one rings as the site names its users -- so the
/// ringing call, the caller's in-call view and the call's history all show "Jane Doe" rather than a username.
/// </summary>
internal sealed class DisplayNameTelephonyExtensionResolver : ITelephonyExtensionResolver
{
    private readonly ITelephonyExtensionStore _store;
    private readonly ITelephonyUserDisplayNames _userDisplayNames;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisplayNameTelephonyExtensionResolver"/> class.
    /// </summary>
    /// <param name="store">The extension registry.</param>
    /// <param name="userDisplayNames">The users' display names.</param>
    public DisplayNameTelephonyExtensionResolver(ITelephonyExtensionStore store, ITelephonyUserDisplayNames userDisplayNames)
    {
        _store = store;
        _userDisplayNames = userDisplayNames;
    }

    /// <inheritdoc/>
    public async Task<ExtensionResolution> ResolveAsync(string number, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return ExtensionResolution.NotFound(number);
        }

        var extension = await _store.FindByNumberAsync(number.Trim(), cancellationToken);

        if (extension is null || string.IsNullOrWhiteSpace(extension.UserId))
        {
            return ExtensionResolution.NotFound(number);
        }

        var names = await _userDisplayNames.GetAsync([extension.UserId], cancellationToken);

        return new ExtensionResolution
        {
            Found = true,
            Number = number.Trim(),
            UserId = extension.UserId,
            UserName = extension.UserName,
            DisplayName = TelephonyExtensionDirectory.Describe(extension, names),
        };
    }
}
