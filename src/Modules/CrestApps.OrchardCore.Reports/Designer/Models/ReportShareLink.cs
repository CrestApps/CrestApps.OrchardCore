using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.Reports.Designer.Models;

/// <summary>
/// A link that opens one designed report without an account or report permission. Only a hash of the link's secret
/// token is stored, so the full link is shown once, when it is created; a stolen database cannot be turned back into
/// working links.
/// </summary>
public sealed class ReportShareLink : CatalogItem, ICloneable<ReportShareLink>
{
    /// <summary>
    /// Gets or sets the identifier of the report the link opens.
    /// </summary>
    public string ReportId { get; set; }

    /// <summary>
    /// Gets or sets a note that tells people who manage the report what the link is for.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the secret token, as lower-case hexadecimal.
    /// </summary>
    public string TokenHash { get; set; }

    /// <summary>
    /// Gets or sets the first characters of the token, shown so people can tell links apart.
    /// </summary>
    public string TokenHint { get; set; }

    /// <summary>
    /// Gets or sets the UTC time after which the link stops working, or <see langword="null"/> for a link that does
    /// not expire.
    /// </summary>
    public DateTime? ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the link was revoked, or <see langword="null"/> while it is active.
    /// </summary>
    public DateTime? RevokedUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the link also allows exporting the report.
    /// </summary>
    public bool AllowExport { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the link works only for people who are signed in.
    /// </summary>
    public bool RequireSignIn { get; set; }

    /// <summary>
    /// Gets or sets the user name of the person who created the link.
    /// </summary>
    public string CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the link was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Determines whether the link still works at a point in time.
    /// </summary>
    /// <param name="utcNow">The current UTC time.</param>
    /// <returns><see langword="true"/> when the link is neither revoked nor expired.</returns>
    public bool IsActive(DateTime utcNow)
    {
        return RevokedUtc is null && (ExpiresUtc is null || ExpiresUtc.Value > utcNow);
    }

    /// <summary>
    /// Creates a copy of the link.
    /// </summary>
    /// <returns>The copy.</returns>
    public ReportShareLink Clone()
    {
        return (ReportShareLink)MemberwiseClone();
    }
}
