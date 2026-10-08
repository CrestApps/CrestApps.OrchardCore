using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;

/// <summary>
/// Works out which scripts the browser harness has to load from the same sources the real widget uses: the module's
/// resource manifest (what the page registers) and its <c>Assets.json</c> (what each served bundle is built from).
/// <para>
/// The harness used to carry its own hand-written copy of that list. When the soft phone grew helper files and new
/// dependencies the copy did not, and the browser suite kept exercising a phone that no longer existed. Deriving the
/// list here means a new dependency is either served, or named in <see cref="OmittedDependencies"/> with the reason,
/// or the harness refuses to start.
/// </para>
/// </summary>
public static partial class SoftPhoneAssets
{
    /// <summary>
    /// The script resource the soft phone widget registers (see <c>SoftPhoneWidgetPresenter.RegisterResources</c>).
    /// </summary>
    public const string WidgetScriptResource = "telephony-soft-phone";

    /// <summary>
    /// The URL prefix the module's own resources are served from.
    /// </summary>
    public const string ModuleUrlPrefix = "/CrestApps.OrchardCore.Telephony/";

    /// <summary>
    /// The URL the harness serves the OrchardCore SignalR client from.
    /// </summary>
    public const string SignalRUrl = "/signalr.js";

    private const string ModuleRelativePath = "src/Modules/CrestApps.OrchardCore.Telephony";

    private const string ManifestConfigurationTypeName =
        "CrestApps.OrchardCore.Telephony.Services.ResourceManagementOptionsConfiguration, CrestApps.OrchardCore.Telephony";

    /// <summary>
    /// Dependencies the widget declares that the harness deliberately does not load, with the reason. Each one is
    /// checked to still be a real dependency, so the list cannot outlive what it excuses.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> OmittedDependencies = new Dictionary<string, string>
    {
        // The phone formats numbers itself and only uses intl-tel-input, when present, to enhance the keypad field.
        // Loading it would make the harness validate every number against a country, which the extension and
        // short-code dials these tests place are not.
        ["intl-tel-input"] = "Optional enhancement of the number field; the soft phone falls back to its own formatter.",
    };

    private static readonly Lazy<string> _moduleDirectory = new(FindModuleDirectory);

    /// <summary>
    /// Gets the Telephony module's source directory.
    /// </summary>
    public static string ModuleDirectory => _moduleDirectory.Value;

    /// <summary>
    /// The script resources the real page loads for the widget, dependencies first, as the resource manager orders
    /// them.
    /// </summary>
    public static IReadOnlyList<string> ResolveWidgetScriptResources()
    {
        var scripts = GetManifest().GetResources("script");
        var ordered = new List<string>();

        Visit(WidgetScriptResource, scripts, ordered, []);

        return ordered;
    }

    /// <summary>
    /// The script URLs the harness page loads: every widget script resource except the documented omissions.
    /// </summary>
    public static IReadOnlyList<string> ResolveHarnessScriptUrls()
    {
        var scripts = GetManifest().GetResources("script");
        var urls = new List<string>();

        foreach (var name in ResolveWidgetScriptResources())
        {
            if (OmittedDependencies.ContainsKey(name))
            {
                continue;
            }

            if (name == "signalr")
            {
                urls.Add(SignalRUrl);

                continue;
            }

            if (!scripts.TryGetValue(name, out var definitions) || definitions.Count == 0)
            {
                throw new InvalidOperationException(
                    $"The soft phone widget now depends on the script resource '{name}', which the browser harness does not serve. " +
                    $"Serve it from {nameof(SoftPhoneTestServer)}, or add it to {nameof(OmittedDependencies)} with the reason it can be left out.");
            }

            urls.Add(ToServedUrl(definitions[0].UrlDebug ?? definitions[0].Url));
        }

        return urls;
    }

    /// <summary>
    /// Maps a URL the harness serves under <see cref="ModuleUrlPrefix"/> to the module's <c>wwwroot</c> file.
    /// </summary>
    public static string ResolveModuleFile(string relativePath)
    {
        var root = Path.GetFullPath(Path.Combine(ModuleDirectory, "wwwroot"));
        var path = Path.GetFullPath(Path.Combine(root, relativePath));

        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"'{relativePath}' is outside the module's wwwroot.");
        }

        return path;
    }

    /// <summary>
    /// Every name each helper file of a served bundle exports on its shared namespace, read from the module's
    /// <c>Assets.json</c> inputs, keyed by the input file. A bundle that is missing one of these was not rebuilt after
    /// the helper was added or changed.
    /// </summary>
    public static IReadOnlyList<HelperExport> ReadHelperExports(string bundleOutput)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(ModuleDirectory, "Assets.json")));
        var group = document.RootElement.EnumerateArray()
            .FirstOrDefault(element => element.GetProperty("output").GetString() == bundleOutput);

        if (group.ValueKind == JsonValueKind.Undefined)
        {
            throw new InvalidOperationException($"Assets.json has no asset group for '{bundleOutput}'.");
        }

        var exports = new List<HelperExport>();

        foreach (var input in group.GetProperty("inputs").EnumerateArray().Select(element => element.GetString()))
        {
            var source = File.ReadAllText(Path.Combine(ModuleDirectory, input));

            foreach (Match alias in NamespaceAliasPattern().Matches(source))
            {
                var variable = alias.Groups["alias"].Value;
                var ns = alias.Groups["namespace"].Value;
                var exportPattern = new Regex(@"^\s*" + Regex.Escape(variable) + @"\.(?<name>[A-Za-z_$][\w$]*)\s*=(?!=)", RegexOptions.Multiline);

                foreach (Match export in exportPattern.Matches(source))
                {
                    exports.Add(new HelperExport(input, ns, export.Groups["name"].Value));
                }
            }
        }

        return exports;
    }

    private static void Visit(
        string name,
        IDictionary<string, IList<ResourceDefinition>> scripts,
        List<string> ordered,
        HashSet<string> visiting)
    {
        if (ordered.Contains(name) || !visiting.Add(name))
        {
            return;
        }

        if (scripts.TryGetValue(name, out var definitions) && definitions.Count > 0)
        {
            foreach (var dependency in definitions[0].Dependencies ?? [])
            {
                Visit(dependency, scripts, ordered, visiting);
            }
        }

        ordered.Add(name);
    }

    private static string ToServedUrl(string url)
    {
        var path = url.TrimStart('~');

        if (!path.StartsWith(ModuleUrlPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"'{url}' is not one of the Telephony module's own resources.");
        }

        return path;
    }

    private static ResourceManifest GetManifest()
    {
        // The module keeps its manifest configuration internal; it is loaded by name rather than widening the module's
        // public surface for a test.
        var type = Type.GetType(ManifestConfigurationTypeName, throwOnError: true);
        var configuration = (IConfigureOptions<ResourceManagementOptions>)Activator.CreateInstance(type, nonPublic: true);
        var options = new ResourceManagementOptions();
        configuration.Configure(options);

        return options.ResourceManifests.Single();
    }

    private static string FindModuleDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, ModuleRelativePath);

            if (File.Exists(Path.Combine(candidate, "Assets.json")))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not find {ModuleRelativePath} above {AppContext.BaseDirectory}. The browser harness serves the module's built scripts from the repository.");
    }

    // var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};
    [GeneratedRegex(@"var\s+(?<alias>[A-Za-z_$][\w$]*)\s*=\s*root\.(?<namespace>[A-Za-z_$][\w$]*)\s*=\s*root\.\k<namespace>\s*\|\|\s*\{\s*\}")]
    private static partial Regex NamespaceAliasPattern();
}

/// <summary>
/// A name a helper file exports on a shared browser namespace.
/// </summary>
/// <param name="Input">The <c>Assets.json</c> input that defines it.</param>
/// <param name="Namespace">The global namespace it is attached to.</param>
/// <param name="Name">The exported name.</param>
public sealed record HelperExport(string Input, string Namespace, string Name);
