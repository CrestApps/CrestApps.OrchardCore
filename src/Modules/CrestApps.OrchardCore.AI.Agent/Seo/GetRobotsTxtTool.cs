using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using OrchardCore.Modules.FileProviders;
using OrchardCore.Seo;

namespace CrestApps.OrchardCore.AI.Agent.Seo;

/// <summary>
/// Returns the robots.txt content the site serves: the physical file when the site has one, otherwise what the robots
/// providers build from the settings, in the order the site writes them.
/// </summary>
public sealed class GetRobotsTxtTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getRobotsTxt";

    private const int MaxLength = 20_000;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {},
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns the robots.txt content the site currently serves, and says whether it comes from the robots settings or from a physical robots.txt file that overrides them. Use it to check the effect of the robots settings before or after changing them.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, SeoConstants.ManageSeoSettings))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        // The site serves a robots.txt file of its static files as it is, and the settings then have no effect.
        var file = services.GetService<IStaticFileProvider>()?.GetFileInfo(SeoConstants.RobotsFileName);
        var fromFile = file?.Exists == true;
        var content = fromFile
            ? await ReadAsync(file, cancellationToken)
            : await BuildAsync(services);

        var truncated = content.Length > MaxLength;

        return AgentToolResults.Json(new
        {
            source = fromFile
                ? "file"
                : "settings",
            content = truncated
                ? content.Substring(0, MaxLength)
                : content,
            truncated = truncated
                ? true
                : (bool?)null,
            note = fromFile
                ? "A physical robots.txt file is in the static files of the site. It is served as it is, and the robots settings have no effect until the file is removed."
                : "The content is built from the robots settings (Settings, Search, Search Engine Optimization, Robots) and from what other features add, such as the sitemaps.",
        });
    }

    private static async Task<string> ReadAsync(IFileInfo file, CancellationToken cancellationToken)
    {
        await using var stream = file.CreateReadStream();
        using var reader = new StreamReader(stream);

        // One character more than the limit shows that the file is longer.
        var buffer = new char[MaxLength + 1];
        var length = await reader.ReadBlockAsync(buffer.AsMemory(), cancellationToken);

        return new string(buffer, 0, length);
    }

    private static async Task<string> BuildAsync(IServiceProvider services)
    {
        var content = new StringBuilder();

        // The order of the site: the provider registered last writes first.
        foreach (var provider in services.GetServices<IRobotsProvider>().Reverse())
        {
            var item = (await provider.GetContentAsync())?.Trim();

            if (!string.IsNullOrEmpty(item))
            {
                content.AppendLine(item);
            }
        }

        return content.ToString();
    }
}
