namespace CrestApps.OrchardCore.Learning.Packages.Models;

/// <summary>
/// The site settings of the package content host (Settings › Learning packages).
/// </summary>
public sealed class LearningPackageContentHostSettings
{
    /// <summary>
    /// Gets or sets the base URL of the package content host, such as <c>https://packages.example.com</c>: an origin of
    /// its own that serves only package files. Empty for strict mode, in which package files are served from the
    /// site's own host on an opaque origin.
    /// </summary>
    public string BaseUrl { get; set; }
}
