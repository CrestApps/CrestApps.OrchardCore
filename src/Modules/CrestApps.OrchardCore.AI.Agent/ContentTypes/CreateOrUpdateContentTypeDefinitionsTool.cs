using CrestApps.OrchardCore.AI.Agent.Recipes;
using CrestApps.OrchardCore.AI.Agent.Services;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Json.Schema;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OrchardCore.Deployment;
using OrchardCore.Json;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Agent.ContentTypes;

public sealed class CreateOrUpdateContentTypeDefinitionsTool : ImportRecipeBaseTool
{
    public const string TheName = "applyContentTypeDefinitionFromRecipe";

    private readonly ContentMetadataService _contentMetadataService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public CreateOrUpdateContentTypeDefinitionsTool(
        IEnumerable<IDeploymentTargetHandler> deploymentTargetHandlers,
        IEnumerable<IRecipeStepHandler> handlers,
        IOptions<DocumentJsonSerializerOptions> options,
        ContentMetadataService contentMetadataService,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
        : base(deploymentTargetHandlers, handlers, options.Value)
    {
        _contentMetadataService = contentMetadataService;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    public override string Name => TheName;

    public override string Description => "Creates or updates a content type or part definition based on the provided JSON recipe.";

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, OrchardCorePermissions.EditContentTypes))
        {
            return "You do not have permission to edit content types or parts.";
        }

        if (!arguments.TryGetFirstString("recipe", out var recipe))
        {
            return MissingArgument();
        }

        return await ProcessRecipeAsync(recipe, cancellationToken);
    }

    protected override async ValueTask<bool> BuildingRecipeSchemaAsync(JsonSchemaBuilder builder)
    {
        var parts = await _contentMetadataService.GetPartsAsync();
        var fields = await _contentMetadataService.GetFieldsAsync();

        builder
            .Type(SchemaValueType.Object) // Root object: step
            .Properties(
                // $.name
                ("name", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Const("ContentDefinition") // $.name must always equal "ContentDefinition"
                ),

                // $.ContentTypes
                ("ContentTypes", new JsonSchemaBuilder()
                    .Description(
                        """
                        List of content types.
                        If fields need to be added to a content type, they must be included as part of a content part
                        whose PartName matches the content type's Name exactly.
                        These parts must also appear in the ContentPartFieldDefinitionRecords collection inside ContentParts.
                        Note that Fields cannot be treated as Parts, so they cannot be used directly in PartName.
                        Fields must only be attached to a Part either by creating a new reusable part or by placing the field on a private part named exactly as the content type.
                        """
                    )
                    .Type(SchemaValueType.Array)
                    .Items(
                        new JsonSchemaBuilder()
                            .Type(SchemaValueType.Object)
                            .Properties(
                                // $.ContentTypes[].Name
                                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),

                                // $.ContentTypes[].DisplayName
                                ("DisplayName", new JsonSchemaBuilder().Type(SchemaValueType.String)),

                                // $.ContentTypes[].Settings
                                ("Settings", new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Object)
                                    .AdditionalProperties(true)
                                ),

                                // $.ContentTypes[].ContentTypePartDefinitionRecords
                                ("ContentTypePartDefinitionRecords", new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Array)
                                    .Items(
                                        new JsonSchemaBuilder()
                                            .Type(SchemaValueType.Object)
                                            .Properties(
                                                // $.ContentTypes[].ContentTypePartDefinitionRecords[].PartName
                                                ("PartName", new JsonSchemaBuilder()
                                                    .Type(SchemaValueType.String)
                                                    .AnyOf(
                                                        new JsonSchemaBuilder().Enum(parts.Select(part => part.Name)), // Suggest known part names
                                                        new JsonSchemaBuilder()
                                                            .Type(SchemaValueType.String)
                                                            .Pattern(@"^(?!.*Field$).+") // Disallow names ending with 'Field'
                                                    )
                                                ),

                                                // $.ContentTypes[].ContentTypePartDefinitionRecords[].Name
                                                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),

                                                // $.ContentTypes[].ContentTypePartDefinitionRecords[].Settings
                                                ("Settings", new JsonSchemaBuilder()
                                                    .Type(SchemaValueType.Object)
                                                    .AdditionalProperties(true)
                                                )
                                            )
                                            .Required("PartName", "Name", "Settings")
                                            .AdditionalProperties(true)
                                    )
                                )
                            )
                            .Required("Name", "DisplayName", "Settings", "ContentTypePartDefinitionRecords")
                            .AdditionalProperties(true)
                    )
                ),

                // $.ContentParts (optional array of content part definitions)
                ("ContentParts", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(
                        new JsonSchemaBuilder()
                            .Type(SchemaValueType.Object)
                            .Properties(
                                // $.ContentParts[].Name
                                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),

                                // $.ContentParts[].Settings
                                ("Settings", new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Object)
                                    .AdditionalProperties(true)
                                ),

                                // $.ContentParts[].ContentPartFieldDefinitionRecords
                                ("ContentPartFieldDefinitionRecords", new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Array)
                                    .Items(
                                        new JsonSchemaBuilder()
                                            .Type(SchemaValueType.Object)
                                            .Properties(
                                                // $.ContentParts[].ContentPartFieldDefinitionRecords[].FieldName
                                                ("FieldName", new JsonSchemaBuilder()
                                                    .Type(SchemaValueType.String)
                                                    .Enum(fields.Select(field => field.Name))
                                                ),

                                                // $.ContentParts[].ContentPartFieldDefinitionRecords[].Name
                                                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),

                                                // $.ContentParts[].ContentPartFieldDefinitionRecords[].Settings
                                                ("Settings", new JsonSchemaBuilder()
                                                    .Type(SchemaValueType.Object)
                                                    .AdditionalProperties(true)
                                                )
                                            )
                                            .Required("FieldName", "Name", "Settings")
                                            .AdditionalProperties(true)
                                    )
                                )
                            )
                            .Required("Name", "Settings", "ContentPartFieldDefinitionRecords")
                            .AdditionalProperties(true)
                    )
                )
            )
            .Required("name", "ContentTypes") // $.name and $.ContentTypes are mandatory; $.ContentParts is optional
            .AdditionalProperties(true);

        return true;
    }
}
