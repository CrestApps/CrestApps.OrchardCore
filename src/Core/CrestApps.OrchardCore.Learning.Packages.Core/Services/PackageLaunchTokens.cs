using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Services;

/// <summary>
/// One launch of a package by a learner: what a launch token is bound to.
/// </summary>
/// <param name="LaunchId">The launch; only the latest launch of a learner on a package may write.</param>
/// <param name="EnrollmentId">The enrollment.</param>
/// <param name="PackageItemId">The package course item.</param>
/// <param name="PackageFileId">The package file the launch runs.</param>
/// <param name="UserId">The learner.</param>
public sealed record PackageLaunch(string LaunchId, string EnrollmentId, string PackageItemId, string PackageFileId, string UserId)
{
    /// <summary>
    /// Gets the origin of the player page that launched the package, when there is one. On a package content host it
    /// is the only page that may frame the package and that the frame page talks to.
    /// </summary>
    public string PlayerOrigin { get; init; }
}

/// <summary>
/// Signed, expiring launch tokens, made with the tenant's ASP.NET Core Data Protection (encrypted and authenticated,
/// with a purpose of their own). The token sits in the path of every file the package frame loads, so relative links in
/// the package resolve under it, and it is the frame's only credential: it opens the files of one package for one launch
/// and the runtime data of that launch, never the learner's session. The expiry is inside the protected payload and is
/// checked against the site clock.
/// </summary>
public sealed class PackageLaunchTokens
{
    /// <summary>
    /// How long a launch token is valid.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);

    private const string Purpose = "CrestApps.OrchardCore.Learning.Packages.Launch";
    private const string Version = "v1";
    private const string VersionWithOrigin = "v2";

    private readonly IDataProtector _protector;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="PackageLaunchTokens"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The tenant's data protection.</param>
    /// <param name="clock">The clock.</param>
    public PackageLaunchTokens(IDataProtectionProvider dataProtectionProvider, IClock clock)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
        _clock = clock;
    }

    /// <summary>
    /// Makes a token for a launch.
    /// </summary>
    /// <param name="launch">The launch.</param>
    /// <returns>The token, safe in a URL path segment.</returns>
    public string Create(PackageLaunch launch)
    {
        ArgumentNullException.ThrowIfNull(launch);

        var expires = (_clock.UtcNow + Lifetime).Ticks.ToString(CultureInfo.InvariantCulture);

        if (string.IsNullOrEmpty(launch.PlayerOrigin))
        {
            return _protector.Protect(string.Join('|', Version, expires, launch.LaunchId, launch.EnrollmentId, launch.PackageItemId, launch.PackageFileId, launch.UserId));
        }

        if (!PackageContentHost.TryNormalizeOrigin(launch.PlayerOrigin, out var origin) || !string.Equals(origin, launch.PlayerOrigin, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The player origin is not an origin.", nameof(launch));
        }

        var payload = string.Join('|', VersionWithOrigin, expires, launch.LaunchId, launch.EnrollmentId, launch.PackageItemId, launch.PackageFileId, launch.UserId, launch.PlayerOrigin);

        return _protector.Protect(payload);
    }

    /// <summary>
    /// Reads a token.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The launch, or <see langword="null"/> when the token was altered, made for another purpose or tenant, or
    /// has expired.</returns>
    public PackageLaunch Read(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 2048)
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
            return null;
        }
        catch (FormatException)
        {
            return null;
        }

        var parts = payload.Split('|');

        var withOrigin = parts.Length == 8 && parts[0] == VersionWithOrigin;

        if (!(withOrigin || (parts.Length == 7 && parts[0] == Version)) || parts.Any(string.IsNullOrEmpty) ||
            !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var expires) ||
            expires <= _clock.UtcNow.Ticks)
        {
            return null;
        }

        return new PackageLaunch(parts[2], parts[3], parts[4], parts[5], parts[6])
        {
            PlayerOrigin = withOrigin ? parts[7] : null,
        };
    }
}
