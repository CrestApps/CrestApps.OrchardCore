using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Learning.Core.Moodle;

/// <summary>
/// The pictures and files Moodle HTML refers to as <c>@@PLUGINFILE@@/path/name</c>, which the import copies to the media
/// library.
/// </summary>
public static partial class MoodleFiles
{
    /// <summary>
    /// The placeholder Moodle writes for the files of the text's file area.
    /// </summary>
    public const string PluginFile = "@@PLUGINFILE@@";

    /// <summary>
    /// Gets the path a file has in its file area, as the HTML names it once decoded, such as <c>/sub/Exit sign.png</c>.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns>The path.</returns>
    public static string PathOf(MoodleFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var folder = string.IsNullOrEmpty(file.FilePath) ? "/" : file.FilePath;

        if (!folder.StartsWith('/'))
        {
            folder = "/" + folder;
        }

        if (!folder.EndsWith('/'))
        {
            folder += "/";
        }

        return folder + file.FileName;
    }

    /// <summary>
    /// Points each <c>@@PLUGINFILE@@</c> link at the address of its imported file, and leaves out the pictures and media
    /// whose file was not imported; a link to a file that was not imported points nowhere.
    /// </summary>
    /// <param name="html">The HTML.</param>
    /// <param name="urls">The address of each imported file, by its path.</param>
    /// <returns>The HTML.</returns>
    public static string Rewrite(string html, IReadOnlyDictionary<string, string> urls)
    {
        if (string.IsNullOrEmpty(html) || !html.Contains(PluginFile, StringComparison.Ordinal))
        {
            return html;
        }

        urls ??= new Dictionary<string, string>(StringComparer.Ordinal);

        var linked = LinkPattern().Replace(html, match =>
        {
            string path;

            try
            {
                path = Uri.UnescapeDataString(match.Groups["path"].Value);
            }
            catch (UriFormatException)
            {
                return match.Value;
            }

            return urls.TryGetValue(path, out var url) ? url : match.Value;
        });

        var withoutMedia = MediaPattern().Replace(linked, string.Empty);

        return LinkPattern().Replace(withoutMedia, "#");
    }

    [GeneratedRegex(@"@@PLUGINFILE@@(?<path>/[^""'\s<>]*)")]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"<\s*(img|video|audio|source|track|iframe|object|embed)\b[^>]*@@PLUGINFILE@@[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex MediaPattern();
}
