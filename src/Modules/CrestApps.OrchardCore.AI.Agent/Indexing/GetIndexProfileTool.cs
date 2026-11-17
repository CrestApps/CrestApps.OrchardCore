using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.Indexing;

/// <summary>
/// Returns one search index profile with its settings and the names of its indexed fields.
/// </summary>
public sealed class GetIndexProfileTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getIndexProfile";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "index": {
          "type": "string",
          "description": "The ID or the name of the index profile, from listIndexProfiles."
        }
      },
      "required": [
        "index"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns one index profile with all its settings (content types, culture, latest or published, analyzer, default search fields) and the names of the indexed fields. Use it before writing a search query or changing the profile. The analyzer, the default search fields and the field names are returned for Lucene and Elasticsearch indexes.";

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

        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, IndexingPermissions.ManageIndexes))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!arguments.TryGetFirstString("index", out var index))
        {
            return "The index argument is required: the ID or the name of an index profile.";
        }

        var indexProfile = await IndexProfileContent.FindAsync(services.GetRequiredService<IIndexProfileManager>(), index);

        if (indexProfile is null)
        {
            return $"There is no index profile with the ID or the name '{index}'. Use listIndexProfiles to get them.";
        }

        var localClock = services.GetRequiredService<ILocalClock>();

        return AgentToolResults.Json(new
        {
            indexProfile = await IndexProfileContent.DescribeAsync(indexProfile, localClock),
            timeZone = await AgentToolResults.GetTimeZoneIdAsync(localClock),
        });
    }
}
