using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Recipes;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Json.Schema;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OrchardCore.Deployment;
using OrchardCore.Json;
using OrchardCore.Recipes.Services;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Agent.Workflows;

public sealed class CreateOrUpdateWorkflowTool : ImportRecipeBaseTool
{
    public const string TheName = "createOrUpdateWorkflow";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IActivityLibrary _activityLibrary;

    public CreateOrUpdateWorkflowTool(
        IEnumerable<IDeploymentTargetHandler> deploymentTargetHandlers,
        IEnumerable<IRecipeStepHandler> handlers,
        IOptions<DocumentJsonSerializerOptions> options,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IActivityLibrary activityLibrary)
        : base(deploymentTargetHandlers, handlers, options.Value)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _activityLibrary = activityLibrary;
    }

    public override string Name => TheName;

    public override string Description => "Creates or updates a workflow types.";

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, OrchardCorePermissions.ManageWorkflows))
        {
            return "You do not have permission to manage workflows.";
        }

        if (!arguments.TryGetFirstString("recipe", out var recipe))
        {
            return MissingArgument();
        }

        return await ProcessRecipeAsync(recipe, cancellationToken);
    }

    protected override ValueTask<bool> BuildingRecipeSchemaAsync(JsonSchemaBuilder builder)
    {
        var activities = _activityLibrary.ListActivities();

        // Partition activities into "events" and "tasks" (adjust the predicates as needed).
        var events = activities.Where(x => x is IEvent).ToArray();
        var tasks = activities.Where(x => x is ITask).ToArray();

        // Convert names to JsonNode[] for Enum(...)
        var eventNameNodes = events
            .Select(a => JsonValue.Create(a.Name)!)   // JsonValue.Create returns JsonNode?
            .ToArray();

        var taskNameNodes = tasks
            .Select(a => JsonValue.Create(a.Name)!)
            .ToArray();

        builder
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Const("WorkflowType")
                ),
                ("data", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(
                        new JsonSchemaBuilder()
                            .Type(SchemaValueType.Object)
                            .Properties(
                                ("Name", new JsonSchemaBuilder()
                                    .Type(SchemaValueType.String)
                                ),

                                // Activities array
                                ("Activities", new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Array)
                                    .Items(
                                        // Activity object schema (we place the conditional on this object)
                                        new JsonSchemaBuilder()
                                            .Type(SchemaValueType.Object)
                                            .Properties(
                                                // base Name property (type only). The conditional below will add enum constraints.
                                                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),

                                                // IsStart property. If missing, the `if` won't match (treated as false via else).
                                                ("IsStart", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),

                                                // X and Y with descriptions
                                                ("X", new JsonSchemaBuilder()
                                                    .Type(SchemaValueType.Number)
                                                    .Description("The horizontal position (in pixels) where the workflow activity node appears on the UI.")
                                                ),
                                                ("Y", new JsonSchemaBuilder()
                                                    .Type(SchemaValueType.Number)
                                                    .Description("The vertical position (in pixels) where the workflow activity node appears on the UI.")
                                                ),

                                                // Properties object (arbitrary)
                                                ("Properties", new JsonSchemaBuilder()
                                                    .Type(SchemaValueType.Object)
                                                    .AdditionalProperties(true)
                                                )
                                            )
                                            .Required("Name", "Properties")
                                            .AdditionalProperties(true)

                                            // Conditional: if IsStart === true then restrict Name enum to event names,
                                            // otherwise (else) restrict Name enum to task names.
                                            .If(
                                                new JsonSchemaBuilder()
                                                    .Properties(("IsStart", new JsonSchemaBuilder().Const(true)))
                                                    .Required("IsStart")
                                            )
                                            .Then(
                                                new JsonSchemaBuilder()
                                                    .Properties(("Name", new JsonSchemaBuilder().Enum(eventNameNodes)))
                                            )
                                            .Else(
                                                new JsonSchemaBuilder()
                                                    .Properties(("Name", new JsonSchemaBuilder().Enum(taskNameNodes)))
                                            )
                                    )
                                ),

                                ("Transitions", new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Array)
                                    .Items(
                                        new JsonSchemaBuilder()
                                            .Type(SchemaValueType.Object)
                                            .AdditionalProperties(true)
                                    )
                                )
                            )
                            .Required("Name", "Activities", "Transitions")
                            .AdditionalProperties(true)
                    )
                )
            )
            .Required("name", "data")
            .AdditionalProperties(true);

        return ValueTask.FromResult(true);
    }

}
