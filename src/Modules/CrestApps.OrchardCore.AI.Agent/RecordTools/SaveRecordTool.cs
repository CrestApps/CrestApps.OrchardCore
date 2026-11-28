using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Agent.Editors;
using CrestApps.OrchardCore.AI.Records;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.AI.Agent.RecordTools;

/// <summary>
/// Creates a record of a registered record kind, or changes one, the way its admin page saves it: the record's editor
/// is built through its display drivers, the values are merged over the fields the editor shows and posted back
/// through the same drivers, so their binding, normalization and validation run, then the record's own rules are
/// checked and only then is it saved. With <c>dryRun</c> nothing is saved. A validation error returns the kind's field
/// schema. The caller needs the kind's write permission.
/// </summary>
internal sealed class SaveRecordTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveRecord";

    private const string Retry = "Nothing was saved. Fix the fields named in the errors, using schema, and try again.";

    private const string UnchangedNote = "The fields in unchanged kept their value: the editor sets some fields only when a record is created, and may normalize others.";

    private readonly AIRecordKindOptions _options;
    private readonly Lazy<JsonElement> _jsonSchema;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveRecordTool"/> class.
    /// </summary>
    /// <param name="options">The registered record kinds.</param>
    public SaveRecordTool(IOptions<AIRecordKindOptions> options)
    {
        _options = options.Value;
        _jsonSchema = new Lazy<JsonElement>(() => AIRecordToolSupport.BuildSchema(_options, new JsonObject
        {
            ["id"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "The id of the record to change, from listRecords. Leave it out to create a record.",
            },
            ["values"] = new JsonObject
            {
                ["type"] = "object",
                ["description"] = "The fields to set, keyed by the field names of the record's editor, for example { \"Name\": \"Food\", \"Code\": \"FOOD\" }. When changing a record, send only the fields to change; the others keep their values. Call saveRecord with empty values to get the field schema. Secrets are never accepted.",
            },
            ["dryRun"] = new JsonObject
            {
                ["type"] = "boolean",
                ["description"] = "When true, the values are checked as a save would check them, and nothing is saved. Default false.",
            },
        }, "values"));
    }

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a record of a kind, or changes one when an id is given, through the same editor and rules as its admin page. Returns the saved record, or the errors and the field schema. With dryRun true it only checks the values.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema.Value;

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
        var (kind, refusal) = await AIRecordToolSupport.ResolveKindAsync(services, _options, arguments, AIRecordAccess.Save);

        if (kind is null)
        {
            return refusal;
        }

        if (!AgentToolArguments.TryReadJsonObject(arguments, "values", out var values))
        {
            return "Nothing was saved: values must be an object of field names and values.";
        }

        arguments.TryGetFirstString("id", out var id);

        var dryRun = arguments.GetFirstValueOrDefault("dryRun", false);

        return await kind.Accept(new SaveVisitor(services, string.IsNullOrWhiteSpace(id) ? null : id.Trim(), values ?? new JsonObject(), dryRun, cancellationToken));
    }

    private sealed class SaveVisitor : IAIRecordKindVisitor<Task<string>>
    {
        private readonly IServiceProvider _services;
        private readonly string _id;
        private readonly JsonObject _values;
        private readonly bool _dryRun;
        private readonly CancellationToken _cancellationToken;

        public SaveVisitor(
            IServiceProvider services,
            string id,
            JsonObject values,
            bool dryRun,
            CancellationToken cancellationToken)
        {
            _services = services;
            _id = id;
            _values = values;
            _dryRun = dryRun;
            _cancellationToken = cancellationToken;
        }

        public async Task<string> Visit<T>(AIRecordKind<T> kind)
            where T : CatalogItem, new()
        {
            var displayManager = _services.GetService<IDisplayManager<T>>();

            if (displayManager is null)
            {
                return $"Nothing was saved: records of the kind '{kind.Name}' have no editor that can be used here. A person changes them on the admin page.";
            }

            var scope = AIRecordScopeChanges.Get(_services);
            var isNew = _id is null;
            T record;

            if (isNew)
            {
                record = await kind.NewAsync(_services, _cancellationToken);
            }
            else
            {
                record = await scope.FindAsync(_services, kind, _id, _cancellationToken);

                if (record is null)
                {
                    return $"Nothing was saved: no record of the kind '{kind.Name}' with the id '{_id}' was found. Use listRecords to find it, or leave the id out to create a record.";
                }
            }

            var httpContext = FormValuesModelUpdater.GetHttpContext(_services, AgentToolAuthorization.GetCaller(_services));
            var editor = await AIRecordEditor.BuildAsync(_services, httpContext, kind, displayManager, record, isNew);

            if (editor is null || editor.Sections.Count == 0)
            {
                return $"Nothing was saved: the editor of the kind '{kind.Name}' shows no fields that can be set here. A person changes these records on the admin page.";
            }

            if (editor.Unsupported.Count > 0)
            {
                return $"Nothing was saved: records of the kind '{kind.Name}' cannot be changed from the chat because {string.Join("; ", editor.Unsupported)}. A person can change them on the admin page.";
            }

            var schema = AIRecordEditor.BuildValuesSchema(editor);

            if (_values.Count == 0)
            {
                return AgentToolResults.Json(new
                {
                    saved = false,
                    kind = kind.Name,
                    id = _id,
                    schema,
                    note = "Nothing was saved: give values, the fields to set, in the shape of schema.",
                });
            }

            var problems = EditorReplay.ValidateChanges(editor, AIRecordEditor.NormalizeValues(editor, _values), out var normalized, "the schema in this result");

            if (problems.Count > 0)
            {
                return Invalid(kind, problems.Select(problem => new AIRecordFieldError(null, problem)).ToList(), schema);
            }

            var formValues = EditorReplay.BuildFormValues(editor, normalized);

            // The post is first replayed on a copy, so a dry run or a refused save never changes the record the store
            // handed out.
            var candidate = kind.Copy(record);
            var check = await AIRecordEditor.ReplayAsync(_services, httpContext, kind, displayManager, candidate, isNew, formValues, _cancellationToken);

            if (!check.Updater.ModelState.IsValid)
            {
                return Invalid(kind, AIRecordEditor.GetErrors(check.Updater.ModelState, editor), schema);
            }

            var (changed, unchanged) = AIRecordEditor.CompareFields(editor, check.Editor, normalized);
            var unchangedNote = unchanged.Count > 0 ? UnchangedNote : null;

            if (_dryRun)
            {
                return AgentToolResults.Json(new
                {
                    saved = false,
                    dryRun = true,
                    valid = true,
                    kind = kind.Name,
                    id = _id,
                    changed,
                    unchanged = unchanged.Count > 0 ? unchanged : null,
                    record = kind.Projection(candidate),
                    note = unchangedNote is null
                        ? "The values are valid. Nothing was saved; call saveRecord again without dryRun to save them."
                        : "The values are valid. Nothing was saved; call saveRecord again without dryRun to save them. " + unchangedNote,
                });
            }

            // As the admin page does: the post is replayed on the record the store handed out, and that record is saved.
            // A new record is not in a store yet, so the checked copy is the one saved.
            if (!isNew)
            {
                var replay = await AIRecordEditor.ReplayAsync(_services, httpContext, kind, displayManager, record, isNew, formValues, _cancellationToken);

                if (!replay.Updater.ModelState.IsValid)
                {
                    return Invalid(kind, AIRecordEditor.GetErrors(replay.Updater.ModelState, editor), schema);
                }

                (changed, unchanged) = AIRecordEditor.CompareFields(editor, replay.Editor, normalized);
            }
            else
            {
                record = candidate;
            }

            await kind.SaveAsync(_services, record, isNew, _cancellationToken);
            scope.Saved(kind, record);

            return AgentToolResults.Json(new
            {
                saved = true,
                created = isNew,
                kind = kind.Name,
                id = record.ItemId,
                changed,
                unchanged = unchanged.Count > 0 ? unchanged : null,
                record = kind.Projection(record),
                note = unchanged.Count > 0 ? UnchangedNote : null,
            });
        }

        private string Invalid(AIRecordKind kind, List<AIRecordFieldError> errors, JsonObject schema)
            => AgentToolResults.Json(new
            {
                saved = false,
                kind = kind.Name,
                id = _id,
                errors,
                schema,
                note = Retry,
            });
    }
}
