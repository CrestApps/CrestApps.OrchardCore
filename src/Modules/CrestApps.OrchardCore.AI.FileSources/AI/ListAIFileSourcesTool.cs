using System.Text.Json;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.FileSources.AI;

/// <summary>
/// Lists the file sources, as the File Sources page does, or returns one by ID. Passwords, keys and passphrases are
/// reported only as stored or not.
/// </summary>
public sealed class ListAIFileSourcesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listAIFileSources";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of one file source to return. Leave it out to list file sources."
        },
        "search": {
          "type": "string",
          "description": "Text the file source's name contains."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, from 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "How many file sources a page holds, at most 50. Defaults to 20."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the file sources that feed File AI data sources, with connector, data source, enabled state, schedule, connector settings and the last run, with optional name search or one file source by id. Also returns the connectors enabled on this site. Passwords and keys are never returned, only whether one is stored.";

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

        if (!await AIModuleTools.IsAuthorizedAsync(services, FileSourcePermissions.ManageFileSources))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var manager = services.GetRequiredService<ISourceCatalogManager<FileSource>>();
        var id = AIModuleTools.ReadString(arguments, "id");

        if (id is not null)
        {
            var fileSource = await manager.FindByIdAsync(id, cancellationToken);

            if (fileSource is null)
            {
                return $"No file source has the ID '{id}'. Call {TheName} without an id to see the file sources.";
            }

            return AIModuleTools.Json(await FileSourceToolSupport.DescribeAsync(services, fileSource, cancellationToken));
        }

        var search = AIModuleTools.ReadString(arguments, "search");
        var (page, pageSize) = AIModuleTools.ReadPaging(arguments);

        // Every record of the catalog, as the File Sources page lists them; one whose connector is gone is still listed.
        var matches = (await manager.GetAllAsync(cancellationToken))
            .Where(fileSource => search is null || fileSource.DisplayText?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
            .OrderBy(fileSource => fileSource.DisplayText, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var items = new List<object>();

        foreach (var fileSource in matches.Skip((page - 1) * pageSize).Take(pageSize))
        {
            items.Add(await FileSourceToolSupport.DescribeAsync(services, fileSource, cancellationToken));
        }

        return AIModuleTools.Json(new
        {
            items,
            total = matches.Count,
            page,
            pageSize,
            truncated = page * pageSize < matches.Count,
            connectors = FileSourceToolSupport.GetConnectors(services).Select(connector => new
            {
                name = connector.Name,
                displayName = connector.DisplayName.Value,
                description = connector.Description.Value,
            }),
        });
    }
}
