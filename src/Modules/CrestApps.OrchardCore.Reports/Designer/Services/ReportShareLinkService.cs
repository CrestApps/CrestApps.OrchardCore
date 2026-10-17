using System.Security.Cryptography;
using System.Text;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Reports.Designer.Models;
using Microsoft.AspNetCore.WebUtilities;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// Creates, finds, and revokes the share links of designed reports. A link's token carries 256 random bits and only
/// its SHA-256 hash is stored.
/// </summary>
public sealed class ReportShareLinkService
{
    private const int TokenBytes = 32;
    private const int HintLength = 6;

    private readonly ICatalog<ReportShareLink> _links;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportShareLinkService"/> class.
    /// </summary>
    /// <param name="links">The share link catalog.</param>
    /// <param name="clock">The clock.</param>
    public ReportShareLinkService(
        ICatalog<ReportShareLink> links,
        IClock clock)
    {
        _links = links;
        _clock = clock;
    }

    /// <summary>
    /// Creates a share link.
    /// </summary>
    /// <param name="reportId">The report identifier.</param>
    /// <param name="name">A note that describes what the link is for.</param>
    /// <param name="expiresUtc">When the link stops working, or <see langword="null"/> for never.</param>
    /// <param name="allowExport">Whether the link also allows exporting.</param>
    /// <param name="requireSignIn">Whether the link works only for signed-in people.</param>
    /// <param name="createdBy">The user name of the creator.</param>
    /// <returns>The stored link and its secret token, which is not stored and cannot be shown again.</returns>
    public async Task<(ReportShareLink Link, string Token)> CreateAsync(
        string reportId,
        string name,
        DateTime? expiresUtc,
        bool allowExport,
        bool requireSignIn,
        string createdBy)
    {
        ArgumentException.ThrowIfNullOrEmpty(reportId);

        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));
        var link = new ReportShareLink
        {
            ReportId = reportId,
            Name = name?.Trim(),
            TokenHash = Hash(token),
            TokenHint = token[..HintLength],
            ExpiresUtc = expiresUtc.HasValue ? DateTime.SpecifyKind(expiresUtc.Value, DateTimeKind.Utc) : null,
            AllowExport = allowExport,
            RequireSignIn = requireSignIn,
            CreatedBy = createdBy,
            CreatedUtc = _clock.UtcNow,
        };

        await _links.CreateAsync(link);

        return (link, token);
    }

    /// <summary>
    /// Finds the active link that a token opens.
    /// </summary>
    /// <param name="token">The token from the link.</param>
    /// <returns>The link, or <see langword="null"/> when the token is unknown, revoked, or expired.</returns>
    public async Task<ReportShareLink> FindActiveAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128)
        {
            return null;
        }

        var hash = Hash(token.Trim());
        var now = _clock.UtcNow;

        foreach (var link in await _links.GetAllAsync())
        {
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(link.TokenHash ?? string.Empty), Encoding.ASCII.GetBytes(hash)))
            {
                return link.IsActive(now) ? link : null;
            }
        }

        return null;
    }

    /// <summary>
    /// Lists the links of a report, newest first.
    /// </summary>
    /// <param name="reportId">The report identifier.</param>
    /// <returns>The links.</returns>
    public async Task<IReadOnlyList<ReportShareLink>> ListAsync(string reportId)
    {
        return (await _links.GetAllAsync())
            .Where(link => string.Equals(link.ReportId, reportId, StringComparison.Ordinal))
            .OrderByDescending(link => link.CreatedUtc)
            .ToArray();
    }

    /// <summary>
    /// Revokes a link of a report.
    /// </summary>
    /// <param name="reportId">The report identifier.</param>
    /// <param name="linkId">The link identifier.</param>
    /// <returns><see langword="true"/> when the link was found and revoked.</returns>
    public async Task<bool> RevokeAsync(string reportId, string linkId)
    {
        if (string.IsNullOrEmpty(linkId))
        {
            return false;
        }

        var link = await _links.FindByIdAsync(linkId);

        if (link is null || !string.Equals(link.ReportId, reportId, StringComparison.Ordinal))
        {
            return false;
        }

        link.RevokedUtc ??= _clock.UtcNow;
        await _links.UpdateAsync(link);

        return true;
    }

    /// <summary>
    /// Deletes every link of a report.
    /// </summary>
    /// <param name="reportId">The report identifier.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task DeleteAllAsync(string reportId)
    {
        foreach (var link in await ListAsync(reportId))
        {
            await _links.DeleteAsync(link);
        }
    }

    /// <summary>
    /// Hashes a token the way links are stored.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The lower-case hexadecimal SHA-256 hash.</returns>
    public static string Hash(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
