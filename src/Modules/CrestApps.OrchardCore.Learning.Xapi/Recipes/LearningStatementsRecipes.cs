using System.Security.Claims;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Learning.Xapi.Core.Services;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Learning.Xapi.Recipes;

/// <summary>
/// Imports statements as learner records. A statement keeps its id, <c>stored</c> and <c>authority</c>: statements
/// cannot change, so they move as they are. A statement already on the site is skipped when it is the same and
/// reported when it differs.
/// </summary>
internal sealed class LearningStatementsRecipeStep : NamedRecipeStepHandler
{
    private readonly XapiStatementService _statements;

    internal readonly IStringLocalizer S;

    public LearningStatementsRecipeStep(XapiStatementService statements, IStringLocalizer<LearningStatementsRecipeStep> stringLocalizer)
        : base(XapiConstants.StatementsRecipeStep)
    {
        _statements = statements;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (context.Step["Statements"] is not JsonArray rows)
        {
            return;
        }

        var items = new List<XapiImportedStatement>();

        foreach (var row in rows.OfType<JsonObject>())
        {
            if (row["Statement"] is not JsonObject statement)
            {
                context.Errors.Add(S["A row of the statements step has no statement; it was skipped."]);

                continue;
            }

            items.Add(new XapiImportedStatement
            {
                Statement = statement,
                Voided = row["Voided"] is JsonValue voided && voided.TryGetValue<bool>(out var flag) && flag,
                ClientId = row["ClientId"]?.GetValue<string>(),
                AuthorityKey = row["AuthorityKey"]?.GetValue<string>(),
            });
        }

        var result = await _statements.ImportAsync(items);

        foreach (var error in result.Errors)
        {
            context.Errors.Add(S["A statement was not imported: {0}", error]);
        }
    }
}

/// <summary>
/// Exports the statements of the Learning Record Store as learner records: the step is opt-in and needs the Export
/// learner records permission. The content of attachments stays in the site's protected file area.
/// </summary>
public sealed class LearningStatementsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<LearningStatementsDeploymentStep>("Learning");
    private static readonly LocalizationSource _title = LocalizationSource.Create<LearningStatementsDeploymentStep>("Learning statements (learner records)");

    /// <inheritdoc />
    public override string Name => XapiConstants.StatementsRecipeStep;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

/// <summary>
/// Writes the statements step, 500 statements a page, oldest first. It writes nothing for a person without the Export
/// learner records permission, and tells the export observers what it wrote.
/// </summary>
internal sealed class LearningStatementsDeploymentSource : DeploymentSourceBase<LearningStatementsDeploymentStep>
{
    private const int PageSize = 500;

    private readonly XapiStatementStore _store;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<ILearnerRecordExportObserver> _exportObservers;
    private readonly ILogger _logger;

    public LearningStatementsDeploymentSource(
        XapiStatementStore store,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IEnumerable<ILearnerRecordExportObserver> exportObservers,
        ILogger<LearningStatementsDeploymentSource> logger)
    {
        _store = store;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _exportObservers = exportObservers;
        _logger = logger;
    }

    protected override async Task ProcessAsync(LearningStatementsDeploymentStep step, DeploymentPlanResult result)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        // A plan can run without a request, such as a scheduled remote deployment; then the plan's own check has passed.
        if (user?.Identity?.IsAuthenticated == true && !await _authorizationService.AuthorizeAsync(user, LearningPermissions.ExportLearnerRecords))
        {
            _logger.LogWarning("Learning statements were not exported: the user lacks the Export learner records permission.");

            return;
        }

        var rows = new JsonArray();

        for (var skip = 0; ; skip += PageSize)
        {
            var page = await _store.PageForExportAsync(skip, PageSize);

            foreach (var record in page)
            {
                rows.Add(new JsonObject
                {
                    ["Statement"] = JsonNode.Parse(record.Statement),
                    ["Voided"] = record.Voided,
                    ["ClientId"] = record.ClientId,
                    ["AuthorityKey"] = record.AuthorityKey,
                });
            }

            if (page.Count < PageSize)
            {
                break;
            }
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["Statements"] = rows,
        });

        foreach (var observer in _exportObservers)
        {
            await observer.ExportedAsync(step.Name, rows.Count, user?.FindFirstValue(ClaimTypes.NameIdentifier), null);
        }
    }
}

internal sealed class LearningStatementsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, LearningStatementsDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(LearningStatementsDeploymentStep step, BuildDisplayContext context)
        => CombineAsync(
            View("LearningStatementsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("LearningStatementsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(LearningStatementsDeploymentStep step, BuildEditorContext context)
        => View("LearningStatementsDeploymentStep_Fields_Edit", step).Location("Content");
}

internal sealed class LearningStatementsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => XapiConstants.StatementsRecipeStep;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(XapiConstants.StatementsRecipeStep).Description("Recipe step discriminator. Must be 'LearningStatements'.")),
                ("Statements", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Statement", new JsonSchemaBuilder().Type(SchemaValueType.Object).Description("The xAPI 1.0.3 statement as stored, with its id, stored, authority, version and timestamp. It is validated, and it is not changed.")),
                        ("Voided", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether a voiding statement voided it.")),
                        ("ClientId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The OpenID client id that stored it, if a client did.")),
                        ("AuthorityKey", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The key of the credential that stored it, which read-own access compares.")))
                    .Required("Statement")).Description("The statements to add. A statement already on the site is skipped when it is the same, and reported when it differs.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}

internal sealed class LearningStatementsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(LearningStatementsDeploymentStep);

    protected override string DisplayText => "Learning statements (learner records)";

    protected override string Description => "Exports the xAPI statements of the Learning Record Store as they were stored, 500 a page. The content of attachments stays in the protected store. Needs the Export learner records permission.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
