using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Records;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Agent.RecordTools;

/// <summary>
/// Deletes records of a registered record kind by their IDs, through the kind's store as its admin page does. A record
/// the kind's owner says is still in use is not deleted. The caller needs the kind's write permission, and the tool
/// asks for approval.
/// </summary>
internal sealed class DeleteRecordTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "deleteRecord";

    private readonly AIRecordKindOptions _options;
    private readonly Lazy<JsonElement> _jsonSchema;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteRecordTool"/> class.
    /// </summary>
    /// <param name="options">The registered record kinds.</param>
    public DeleteRecordTool(IOptions<AIRecordKindOptions> options)
    {
        _options = options.Value;
        _jsonSchema = new Lazy<JsonElement>(() => AIRecordToolSupport.BuildSchema(_options, new JsonObject
        {
            ["ids"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "string",
                },
                ["description"] = $"The ids of the records to delete, from listRecords; at most {AIRecordToolSupport.MaxDeleteCount}.",
            },
        }, "ids"));
    }

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Deletes records of a kind by their ids. A record that is still in use is not deleted, and the reason is returned. This cannot be undone.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema.Value;

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;
        var (kind, refusal) = await AIRecordToolSupport.ResolveKindAsync(services, _options, arguments, AIRecordAccess.Delete);

        if (kind is null)
        {
            return refusal;
        }

        if (!AgentToolArguments.TryReadStrings(arguments, "ids", out var ids) || ids is null || ids.Count == 0)
        {
            return $"Nothing was deleted: give the ids of the records to delete. Use listRecords with the kind '{kind.Name}' to find them.";
        }

        if (ids.Count > AIRecordToolSupport.MaxDeleteCount)
        {
            return $"Nothing was deleted: give at most {AIRecordToolSupport.MaxDeleteCount} ids at a time.";
        }

        return await kind.Accept(new DeleteVisitor(services, ids, cancellationToken));
    }

    private sealed class DeleteVisitor : IAIRecordKindVisitor<Task<string>>
    {
        private readonly IServiceProvider _services;
        private readonly List<string> _ids;
        private readonly CancellationToken _cancellationToken;

        public DeleteVisitor(
            IServiceProvider services,
            List<string> ids,
            CancellationToken cancellationToken)
        {
            _services = services;
            _ids = ids;
            _cancellationToken = cancellationToken;
        }

        public async Task<string> Visit<T>(AIRecordKind<T> kind)
            where T : CatalogItem, new()
        {
            var scope = AIRecordScopeChanges.Get(_services);
            var deleted = new List<object>();
            var notFound = new List<string>();
            var refused = new List<object>();

            foreach (var id in _ids)
            {
                var record = await scope.FindAsync(_services, kind, id, _cancellationToken);

                if (record is null)
                {
                    notFound.Add(id);

                    continue;
                }

                if (kind.InUseAsync is not null)
                {
                    var inUse = await kind.InUseAsync(_services, record, _cancellationToken);

                    if (!string.IsNullOrEmpty(inUse))
                    {
                        refused.Add(new
                        {
                            id,
                            reason = inUse,
                        });

                        continue;
                    }
                }

                if (!await kind.DeleteAsync(_services, record, _cancellationToken))
                {
                    refused.Add(new
                    {
                        id,
                        reason = "The record could not be deleted.",
                    });

                    continue;
                }

                scope.Deleted(kind, record.ItemId ?? id);
                deleted.Add(new
                {
                    id,
                    record = kind.Projection(record),
                });
            }

            return AgentToolResults.Json(new
            {
                kind = kind.Name,
                deleted,
                notFound = notFound.Count > 0 ? notFound : null,
                notDeleted = refused.Count > 0 ? refused : null,
                note = deleted.Count == 0
                    ? "Nothing was deleted."
                    : null,
            });
        }
    }
}
