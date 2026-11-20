using Microsoft.Extensions.Options;
using OrchardCore.Media;

namespace CrestApps.OrchardCore.Learning.Services;

/// <summary>
/// The media limits Learning needs for lesson files and videos, read from the <c>CrestApps:Learning:Media</c>
/// configuration section. Orchard Core Media allows 30 MB a file and no OpenDocument spreadsheets by default, which is
/// too little for training videos and office handouts.
/// </summary>
public sealed class LearningMediaOptions
{
    /// <summary>
    /// The upload limit Learning asks for when none is configured: 2 GB.
    /// </summary>
    public const long DefaultMaxFileSize = 2L * 1024 * 1024 * 1024;

    /// <summary>
    /// The file types Learning adds when none are configured: OpenDocument and other office files, plain text, and
    /// video captions.
    /// </summary>
    public static readonly string[] DefaultAdditionalFileExtensions =
    [
        ".ods",
        ".odp",
        ".odg",
        ".odt",
        ".rtf",
        ".txt",
        ".pps",
        ".ppsx",
        ".vsdx",
        ".vtt",
        ".srt",
    ];

    /// <summary>
    /// File types that never become uploadable through this option: programs, scripts, pages a browser runs, server
    /// pages and macro-enabled office files.
    /// </summary>
    internal static readonly HashSet<string> NeverAllowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".com", ".bat", ".cmd", ".msi", ".ps1", ".psm1", ".sh", ".vbs", ".vbe", ".js", ".mjs", ".jse",
        ".wsf", ".wsh", ".hta", ".scr", ".jar", ".svg", ".htm", ".html", ".xhtml", ".shtml", ".css", ".xml", ".xsl",
        ".asp", ".aspx", ".ashx", ".asmx", ".axd", ".cshtml", ".vbhtml", ".php", ".jsp", ".config", ".cer",
        ".docm", ".dotm", ".xlsm", ".xltm", ".xlam", ".pptm", ".potm", ".ppam", ".ppsm",
    };

    /// <summary>
    /// Gets or sets the largest media file, in bytes, that Learning makes sure Orchard Core Media takes. Media's own
    /// limit is kept when it is larger. Zero leaves Media's limit alone.
    /// </summary>
    public long MaxFileSize { get; set; } = DefaultMaxFileSize;

    /// <summary>
    /// Gets or sets the file types added to the types Orchard Core Media takes, or <see langword="null"/> for
    /// <see cref="DefaultAdditionalFileExtensions"/>. Programs, scripts, web pages and macro-enabled office files are
    /// never added.
    /// </summary>
    public string[] AdditionalFileExtensions { get; set; }
}

/// <summary>
/// Raises Orchard Core Media's upload limit to Learning's and adds Learning's file types, after Media read its own
/// configuration. Media's limit applies to every media upload of the tenant, the media library included.
/// </summary>
internal sealed class LearningMediaOptionsSetup : IPostConfigureOptions<MediaOptions>
{
    private readonly LearningMediaOptions _options;

    public LearningMediaOptionsSetup(IOptions<LearningMediaOptions> options)
    {
        _options = options.Value;
    }

    public void PostConfigure(string name, MediaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (_options.MaxFileSize > options.MaxFileSize)
        {
            options.MaxFileSize = _options.MaxFileSize;
        }

        var allowed = options.AllowedFileExtensions is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(options.AllowedFileExtensions, StringComparer.OrdinalIgnoreCase);

        foreach (var extension in _options.AdditionalFileExtensions ?? LearningMediaOptions.DefaultAdditionalFileExtensions)
        {
            var normalized = Normalize(extension);

            if (normalized is not null && !LearningMediaOptions.NeverAllowed.Contains(normalized))
            {
                allowed.Add(normalized);
            }
        }

        options.AllowedFileExtensions = allowed;
    }

    private static string Normalize(string extension)
    {
        var trimmed = extension?.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed == ".")
        {
            return null;
        }

        return trimmed.StartsWith('.') ? trimmed : "." + trimmed;
    }
}
