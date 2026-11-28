using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Records;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Agent.RecordTools;

/// <summary>
/// Lists a page of the records of one registered record kind, optionally filtered by a text, with the fields the
/// kind's owner allow-listed. The caller needs the kind's read permission.
/// </summary>
internal sealed class ListRecordsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listRecords";

    private readonly AIRecordKindOptions _options;
    private readonly Lazy<JsonElement> _jsonSchema;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListRecordsTool"/> class.
    /// </summary>
    /// <param name="options">The registered record kinds.</param>
    public ListRecordsTool(IOptions<AIRecordKindOptions> options)
    {
        _options = options.Value;
        _jsonSchema = new Lazy<JsonElement>(() => AIRecordToolSupport.BuildSchema(_options, new JsonObject
        {
            ["query"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Optional text the records' names must contain.",
            },
            ["page"] = new JsonObject
            {
                ["type"] = "integer",
                ["description"] = "The page to return, starting at 1. Default 1.",
            },
            ["pageSize"] = new JsonObject
            {
                ["type"] = "integer",
                ["description"] = $"The number of records on a page, at most {AgentToolResults.MaxPageSize}. Default {AgentToolResults.DefaultPageSize}.",
            },
        }));
    }

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the records of one kind (a setup catalog such as tax categories), a page at a time, optionally filtered by a text in their names. Returns each record's id and main fields.";

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

        arguments.TryGetFirstString("query", out var query);

        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        return await kind.Accept(new ListVisitor(services, string.IsNullOrWhiteSpace(query) ? null : query.Trim(), page, pageSize, cancellationToken));
    }

    private sealed class ListVisitor : IAIRecordKindVisitor<Task<string>>
    {
        private readonly IServiceProvider _services;
        private readonly string _query;
        private readonly int _page;
        private readonly int _pageSize;
        private readonly CancellationToken _cancellationToken;

        public ListVisitor(
            IServiceProvider services,
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            _services = services;
            _query = query;
            _page = page;
            _pageSize = pageSize;
            _cancellationToken = cancellationToken;
        }

        public async Task<string> Visit<T>(AIRecordKind<T> kind)
            where T : CatalogItem, new()
        {
            var result = await kind.ListAsync(_services, new AIRecordQuery
            {
                Text = _query,
                Page = _page,
                PageSize = _pageSize,
            }, _cancellationToken);

            var scope = AIRecordScopeChanges.Get(_services);
            var items = (result?.Entries ?? Array.Empty<T>())
                .Select(record => scope.Apply(kind, record))
                .Where(record => record is not null)
                .Select(kind.Projection)
                .ToList();

            var total = result?.Count ?? 0;

            return AgentToolResults.Json(new
            {
                kind = kind.Name,
                items,
                total,
                page = _page,
                pageSize = _pageSize,
                truncated = total > _page * _pageSize ? true : (bool?)null,
                query = _query,
                note = total > _page * _pageSize
                    ? "More records match; ask for the next page or narrow the query."
                    : null,
            });
        }
    }
}
