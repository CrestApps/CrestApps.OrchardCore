using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Records;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Agent.RecordTools;

/// <summary>
/// Reads one record of a registered record kind by its ID, with the fields the kind's owner allow-listed. The caller
/// needs the kind's read permission.
/// </summary>
internal sealed class GetRecordTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getRecord";

    private readonly AIRecordKindOptions _options;
    private readonly Lazy<JsonElement> _jsonSchema;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetRecordTool"/> class.
    /// </summary>
    /// <param name="options">The registered record kinds.</param>
    public GetRecordTool(IOptions<AIRecordKindOptions> options)
    {
        _options = options.Value;
        _jsonSchema = new Lazy<JsonElement>(() => AIRecordToolSupport.BuildSchema(_options, new JsonObject
        {
            ["id"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "The id of the record, from listRecords.",
            },
        }, "id"));
    }

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Gets one record of a kind by its id, with its main fields. Use listRecords to find the id.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema.Value;

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;
        var (kind, refusal) = await AIRecordToolSupport.ResolveKindAsync(services, _options, arguments, AIRecordAccess.Read);

        if (kind is null)
        {
            return refusal;
        }

        if (!arguments.TryGetFirstString("id", out var id) || string.IsNullOrWhiteSpace(id))
        {
            return $"Give the id of the record to read. Use listRecords with the kind '{kind.Name}' to find it.";
        }

        return await kind.Accept(new GetVisitor(services, id.Trim(), cancellationToken));
    }

    private sealed class GetVisitor : IAIRecordKindVisitor<Task<string>>
    {
        private readonly IServiceProvider _services;
        private readonly string _id;
        private readonly CancellationToken _cancellationToken;

        public GetVisitor(
            IServiceProvider services,
            string id,
            CancellationToken cancellationToken)
        {
            _services = services;
            _id = id;
            _cancellationToken = cancellationToken;
        }

        public async Task<string> Visit<T>(AIRecordKind<T> kind)
            where T : CatalogItem, new()
        {
            var record = await AIRecordScopeChanges.Get(_services).FindAsync(_services, kind, _id, _cancellationToken);

            if (record is null)
            {
                return $"No record of the kind '{kind.Name}' with the id '{_id}' was found. Use listRecords with the kind '{kind.Name}' to find it.";
            }

            return AgentToolResults.Json(new
            {
                kind = kind.Name,
                id = record.ItemId,
                record = kind.Projection(record),
                note = kind.CanWrite
                    ? "To change it, call saveRecord with this kind, the id and the fields to change in values."
                    : kind.SaveToolName is not null
                        ? $"To change it, call {kind.SaveToolName}: its arguments carry these field names."
                        : null,
            });
        }
    }
}
