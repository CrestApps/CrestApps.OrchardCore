using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.FileStorage.ProtectedFiles;

/// <summary>
/// Signed, time-limited links to the files of an area, opened only for people every policy of the area allows.
/// </summary>
/// <typeparam name="TArea">The marker type of the area.</typeparam>
public sealed class ProtectedFileLinks<TArea> : IProtectedFileLinks<TArea>
{
    private readonly ITimeLimitedDataProtector _protector;
    private readonly IProtectedFileStore<TArea> _store;
    private readonly IEnumerable<IProtectedFileAccessPolicy<TArea>> _policies;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtectedFileLinks{TArea}"/> class.
    /// </summary>
    public ProtectedFileLinks(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<ProtectedFileAreaOptions<TArea>> options,
        IProtectedFileStore<TArea> store,
        IEnumerable<IProtectedFileAccessPolicy<TArea>> policies)
    {
        _protector = dataProtectionProvider.CreateProtector("CrestApps.OrchardCore.ProtectedFiles." + options.Value.Folder).ToTimeLimitedDataProtector();
        _store = store;
        _policies = policies;
    }

    /// <inheritdoc/>
    public string CreateToken(string fileId, string resourceId, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileId);

        return _protector.Protect($"{fileId}|{resourceId}", lifetime);
    }

    /// <inheritdoc/>
    public async Task<(ProtectedFile File, Stream Content)?> OpenAsync(string token, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        string payload;

        try
        {
            payload = _protector.Unprotect(token);
        }
        catch (CryptographicException)
        {
            // An altered or expired token reads as no file.
            return null;
        }

        var separator = payload.IndexOf('|', StringComparison.Ordinal);
        var access = new ProtectedFileAccess
        {
            FileId = separator < 0 ? payload : payload[..separator],
            ResourceId = separator < 0 ? null : payload[(separator + 1)..],
            User = user,
        };

        var policies = _policies.ToArray();

        // An area nobody wrote a policy for serves nothing.
        if (policies.Length == 0)
        {
            return null;
        }

        foreach (var policy in policies)
        {
            if (!await policy.CanReadAsync(access, cancellationToken))
            {
                return null;
            }
        }

        var file = await _store.FindAsync(access.FileId, cancellationToken);

        if (file is null)
        {
            return null;
        }

        var stream = await _store.OpenAsync(access.FileId, cancellationToken);

        return stream is null ? null : (file, stream);
    }
}
