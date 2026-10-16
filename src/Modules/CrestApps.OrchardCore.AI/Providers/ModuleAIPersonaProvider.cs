using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Services;
using CrestApps.Core.Templates.Parsing;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Extensions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Providers;

/// <summary>
/// Reads the persona files modules ship in their <c>Templates/Personas</c> folder. The file name, without its
/// extension, is the persona's technical name; the front matter holds its fields and the body its instructions.
/// </summary>
/// <remarks>
/// Module files do not change while the application runs, so they are read once.
/// </remarks>
internal sealed class ModuleAIPersonaProvider : IAIPersonaProvider
{
    internal const string PersonasDirectoryPath = AIPersonaFileSystemProvider.PersonasDirectoryPath;

    private const string _personasDirectorySubPath = PersonasDirectoryPath + "/";

    private readonly IExtensionManager _extensionManager;
    private readonly IApplicationContext _applicationContext;
    private readonly IEnumerable<ITemplateParser> _parsers;
    private readonly ILogger _logger;

    private IReadOnlyList<AIPersona> _personas;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleAIPersonaProvider"/> class.
    /// </summary>
    /// <param name="extensionManager">The extension manager used to enumerate installed modules.</param>
    /// <param name="applicationContext">The application context providing module information.</param>
    /// <param name="parsers">The template parsers, which read the persona files.</param>
    /// <param name="logger">The logger.</param>
    public ModuleAIPersonaProvider(
        IExtensionManager extensionManager,
        IApplicationContext applicationContext,
        IEnumerable<ITemplateParser> parsers,
        ILogger<ModuleAIPersonaProvider> logger)
    {
        _extensionManager = extensionManager;
        _applicationContext = applicationContext;
        _parsers = parsers;
        _logger = logger;
    }

    /// <summary>
    /// Gets the personas shipped by modules. Every call returns copies, so a caller cannot change what later
    /// callers read.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task<IReadOnlyList<AIPersona>> GetPersonasAsync(CancellationToken cancellationToken = default)
    {
        _personas ??= ReadPersonas();

        return Task.FromResult<IReadOnlyList<AIPersona>>(_personas.Select(persona => persona.Clone()).ToList());
    }

    private List<AIPersona> ReadPersonas()
    {
        var personas = new List<AIPersona>();
        var application = _applicationContext.Application;

        foreach (var extension in _extensionManager.GetExtensions())
        {
            var module = application.GetModule(extension.Id);

            if (string.IsNullOrEmpty(module.Name))
            {
                continue;
            }

            var personasRoot = module.Root + _personasDirectorySubPath;

            foreach (var assetPath in module.AssetPaths)
            {
                if (!assetPath.StartsWith(personasRoot, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var relativePath = assetPath[personasRoot.Length..];

                if (string.IsNullOrEmpty(relativePath))
                {
                    continue;
                }

                var parser = AIProfileTemplateParser.GetParserForExtension(_parsers, Path.GetExtension(relativePath));

                if (parser is null)
                {
                    continue;
                }

                try
                {
                    var fileInfo = module.GetFileInfo(assetPath[module.Root.Length..]);

                    if (!fileInfo.Exists)
                    {
                        continue;
                    }

                    using var stream = fileInfo.CreateReadStream();
                    using var reader = new StreamReader(stream);

                    var parseResult = parser.Parse(reader.ReadToEnd());

                    // Sub-folders become part of the name, the same way the content root's persona files are named.
                    var id = Path.ChangeExtension(relativePath, null)
                        .Replace('/', '.')
                        .Replace('\\', '.');

                    personas.Add(AIPersonaFileParser.Parse(id, parseResult));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse the AI persona file '{AssetPath}'.", assetPath);
                }
            }
        }

        return personas;
    }
}
