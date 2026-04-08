using System.Reflection;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Templates.Parsing;

namespace CrestApps.Core.AI.Services;

/// <summary>
/// Discovers profile templates from embedded resources in an assembly.
/// </summary>
public sealed class EmbeddedResourceAIProfileTemplateProvider : IAIProfileTemplateProvider
{
    private const string ProfilesResourceSegment = ".Templates.Profiles.";
    private const string OrchardCoreProfilesResourceSegment = ".Templates>Profiles>";

    private readonly Assembly _assembly;
    private readonly IEnumerable<ITemplateParser> _parsers;

    public EmbeddedResourceAIProfileTemplateProvider(Assembly assembly, IEnumerable<ITemplateParser> parsers)
    {
        _assembly = assembly;
        _parsers = parsers;
    }

    public Task<IReadOnlyList<AIProfileTemplate>> GetTemplatesAsync()
    {
        var templates = new List<AIProfileTemplate>();

        foreach (var resourceName in _assembly.GetManifestResourceNames())
        {
            var profilesIndex = resourceName.IndexOf(ProfilesResourceSegment, StringComparison.OrdinalIgnoreCase);
            var segmentLength = ProfilesResourceSegment.Length;

            if (profilesIndex < 0)
            {
                profilesIndex = resourceName.IndexOf(OrchardCoreProfilesResourceSegment, StringComparison.OrdinalIgnoreCase);
                segmentLength = OrchardCoreProfilesResourceSegment.Length;
            }

            if (profilesIndex < 0)
            {
                continue;
            }

            var extension = GetExtension(resourceName);
            var parser = AIProfileTemplateParser.GetParserForExtension(_parsers, extension);

            if (parser is null)
            {
                continue;
            }

            using var stream = _assembly.GetManifestResourceStream(resourceName);

            if (stream is null)
            {
                continue;
            }

            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();
            var parseResult = parser.Parse(content);
            var relativePath = resourceName[(profilesIndex + segmentLength)..];
            var id = extension is not null && relativePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
                ? relativePath[..^extension.Length]
                : relativePath;

            templates.Add(AIProfileTemplateParser.Parse(id, parseResult));
        }

        return Task.FromResult<IReadOnlyList<AIProfileTemplate>>(templates);
    }

    private static string GetExtension(string resourceName)
    {
        var lastDot = resourceName.LastIndexOf('.');

        if (lastDot <= 0)
        {
            return null;
        }

        return resourceName[lastDot..];
    }
}
