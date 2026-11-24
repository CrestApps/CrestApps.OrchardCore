using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Learning.Core.Services;

/// <summary>
/// The name a learner sees for a lesson file. A stored media file is often renamed: Orchard Core's attached media
/// editor keeps a hash of the file, and files moved in by an import carry a GUID before the name. The original name is
/// kept beside the path, and is shown instead.
/// </summary>
public static partial class LearningFileNames
{
    /// <summary>
    /// Gets the name to show for a file: the text the author gave it, else the name the file had when it was uploaded,
    /// else the stored name without a leading GUID.
    /// </summary>
    /// <param name="path">The stored path of the file.</param>
    /// <param name="attachedName">The name the file had when it was uploaded, when the field kept it.</param>
    /// <param name="mediaText">The text the author gave the file, when there is one.</param>
    /// <returns>The name to show.</returns>
    public static string GetDisplayName(string path, string attachedName, string mediaText)
    {
        if (!string.IsNullOrWhiteSpace(mediaText))
        {
            return mediaText.Trim();
        }

        if (!string.IsNullOrWhiteSpace(attachedName))
        {
            return Path.GetFileName(attachedName.Trim());
        }

        var name = Path.GetFileName(path ?? string.Empty);
        var match = GuidPrefix().Match(name);

        return match.Success ? match.Groups["name"].Value : name;
    }

    [GeneratedRegex("^(?:[0-9a-fA-F]{32}|[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})[_-](?<name>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex GuidPrefix();
}
