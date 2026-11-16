using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;

namespace CrestApps.OrchardCore.Learning.Packages.Services;

/// <summary>
/// The markup put at the start of every HTML page of a package: the learner's runtime data and the launch id, then the
/// SCORM 1.2 run-time API script (<c>Runtime/scorm12-api.js</c>, which the module build embeds).
/// </summary>
public static class PackageShimScript
{
    private static readonly Lazy<string> _script = new(ReadScript);

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        // The default encoder escapes <, >, & and every character outside ASCII, so the data cannot end the script
        // element and the inserted markup stays ASCII.
        PropertyNamingPolicy = null,
    };

    /// <summary>
    /// Gets the run-time API script.
    /// </summary>
    public static string Script => _script.Value;

    /// <summary>
    /// Builds the markup for a page of a launch.
    /// </summary>
    /// <param name="launch">The launch.</param>
    /// <param name="state">The runtime state of the launch, or <see langword="null"/>.</param>
    /// <param name="playerOrigin">The origin of the player page, the only one the package frame posts to directly.</param>
    /// <returns>The markup.</returns>
    public static string Build(PackageLaunch launch, PackageRuntimeState state, string playerOrigin)
    {
        ArgumentNullException.ThrowIfNull(launch);

        var data = JsonSerializer.Serialize(new LaunchData(launch.LaunchId, playerOrigin, state?.Cmi ?? new Scorm12Cmi()), _jsonOptions);

        return new StringBuilder()
            .Append("<script>window.CrestAppsScorm12Launch=")
            .Append(data)
            .Append(";</script><script>")
            .Append(Script)
            .Append("</script>")
            .ToString();
    }

    private static string ReadScript()
    {
        // The module build embeds every file of the module; the script is found by its file name.
        var assembly = typeof(PackageShimScript).Assembly;
        var name = assembly.GetManifestResourceNames().FirstOrDefault(candidate => candidate.EndsWith("scorm12-api.js", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("The SCORM 1.2 run-time script is not embedded in the module.");

        using var stream = assembly.GetManifestResourceStream(name);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        return reader.ReadToEnd();
    }

    private sealed record LaunchData(
        [property: JsonPropertyName("launchId")] string LaunchId,
        [property: JsonPropertyName("playerOrigin")] string PlayerOrigin,
        [property: JsonPropertyName("cmi")] Scorm12Cmi Cmi);
}
