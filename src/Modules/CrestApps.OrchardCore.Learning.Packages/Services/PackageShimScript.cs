using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;

namespace CrestApps.OrchardCore.Learning.Packages.Services;

/// <summary>
/// The markup put at the start of every HTML page of a package: the learner's runtime data and the launch id, then the
/// run-time API script of the package's standard (<c>Runtime/scorm12-api.js</c>, which defines <c>window.API</c>, or
/// <c>Runtime/scorm2004-api.js</c>, which defines <c>window.API_1484_11</c>; the module build embeds both).
/// </summary>
public static class PackageShimScript
{
    /// <summary>
    /// The message protocol of the SCORM 1.2 frame script.
    /// </summary>
    public const string Scorm12Protocol = "crestapps-scorm12";

    /// <summary>
    /// The message protocol of the SCORM 2004 frame script.
    /// </summary>
    public const string Scorm2004Protocol = "crestapps-scorm2004";

    private static readonly Lazy<string> _script = new(() => ReadScript("scorm12-api.js"));
    private static readonly Lazy<string> _script2004 = new(() => ReadScript("scorm2004-api.js"));

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        // The default encoder escapes <, >, & and every character outside ASCII, so the data cannot end the script
        // element and the inserted markup stays ASCII.
        PropertyNamingPolicy = null,
    };

    /// <summary>
    /// Gets the SCORM 1.2 run-time API script.
    /// </summary>
    public static string Script => _script.Value;

    /// <summary>
    /// Gets the SCORM 2004 run-time API script.
    /// </summary>
    public static string Scorm2004Script => _script2004.Value;

    /// <summary>
    /// Gets the message protocol the player expects from the frame of a package of a standard.
    /// </summary>
    /// <param name="standard">The standard of the package.</param>
    /// <returns>The protocol.</returns>
    public static string ProtocolFor(string standard)
        => PackagesConstants.Standards.IsScorm2004(standard) ? Scorm2004Protocol : Scorm12Protocol;

    /// <summary>
    /// Builds the markup for a page of a launch, for the standard of the runtime state.
    /// </summary>
    /// <param name="launch">The launch.</param>
    /// <param name="state">The runtime state of the launch, or <see langword="null"/>.</param>
    /// <param name="playerOrigin">The origin of the player page, the only one the package frame posts to directly.</param>
    /// <returns>The markup.</returns>
    public static string Build(PackageLaunch launch, PackageRuntimeState state, string playerOrigin)
        => Build(launch, state, playerOrigin, state?.Standard);

    /// <summary>
    /// Builds the markup for a page of a launch.
    /// </summary>
    /// <param name="launch">The launch.</param>
    /// <param name="state">The runtime state of the launch, or <see langword="null"/>.</param>
    /// <param name="playerOrigin">The origin of the player page, the only one the package frame posts to directly.</param>
    /// <param name="standard">The standard of the package, which picks the API script.</param>
    /// <returns>The markup.</returns>
    public static string Build(PackageLaunch launch, PackageRuntimeState state, string playerOrigin, string standard)
        => Build(launch, state, playerOrigin, standard, bridge: false);

    /// <summary>
    /// Builds the markup for the frame page of a launch on the package content host. The page is the only one that
    /// gets the API there: the package's pages run in a child frame on the same origin and find it by walking up their
    /// parents. The script takes relayed messages only from that origin.
    /// </summary>
    /// <param name="launch">The launch.</param>
    /// <param name="state">The runtime state of the launch, or <see langword="null"/>.</param>
    /// <param name="playerOrigin">The origin of the player page, the only one the page posts to.</param>
    /// <param name="standard">The standard of the package, which picks the API script.</param>
    /// <returns>The markup.</returns>
    public static string BuildBridge(PackageLaunch launch, PackageRuntimeState state, string playerOrigin, string standard)
        => Build(launch, state, playerOrigin, standard, bridge: true);

    private static string Build(PackageLaunch launch, PackageRuntimeState state, string playerOrigin, string standard, bool bridge)
    {
        ArgumentNullException.ThrowIfNull(launch);

        if (PackagesConstants.Standards.IsScorm2004(standard))
        {
            var cmi = state?.Cmi2004 ?? new Scorm2004Cmi
            {
                Edition = standard == PackagesConstants.Standards.Scorm2004FourthEdition ? 4 : 3,
            };

            return Markup("CrestAppsScorm2004Launch", JsonSerializer.Serialize(new LaunchData<Scorm2004Cmi>(launch.LaunchId, playerOrigin, cmi, bridge), _jsonOptions), Scorm2004Script);
        }

        return Markup("CrestAppsScorm12Launch", JsonSerializer.Serialize(new LaunchData<Scorm12Cmi>(launch.LaunchId, playerOrigin, state?.Cmi ?? new Scorm12Cmi(), bridge), _jsonOptions), Script);
    }

    private static string Markup(string variable, string data, string script)
        => new StringBuilder()
            .Append("<script>window.")
            .Append(variable)
            .Append('=')
            .Append(data)
            .Append(";</script><script>")
            .Append(script)
            .Append("</script>")
            .ToString();

    private static string ReadScript(string fileName)
    {
        // The module build embeds every file of the module; the script is found by its file name.
        var assembly = typeof(PackageShimScript).Assembly;
        var name = assembly.GetManifestResourceNames().FirstOrDefault(candidate => candidate.EndsWith(fileName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"The run-time script {fileName} is not embedded in the module.");

        using var stream = assembly.GetManifestResourceStream(name);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        return reader.ReadToEnd();
    }

    private sealed record LaunchData<TCmi>(
        [property: JsonPropertyName("launchId")] string LaunchId,
        [property: JsonPropertyName("playerOrigin")] string PlayerOrigin,
        [property: JsonPropertyName("cmi")] TCmi Cmi,
        [property: JsonPropertyName("bridge"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Bridge);
}
