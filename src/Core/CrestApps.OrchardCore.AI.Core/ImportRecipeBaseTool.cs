using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core.Telemetry;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Json.Schema;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Represents the import recipe base tool.
/// </summary>
public abstract class ImportRecipeBaseTool : AIFunction
{
    private const string RecipeSchemaInstruction = $"If the 'getOrchardCoreRecipeJsonSchema' tool is available, call it first to fetch the current Orchard Core recipe schema before building the recipe JSON.";

    protected readonly static JsonSerializerOptions RecipeSerializerOptions = new(JOptions.Default)
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    $$"""
    {
      "type": "object",
      "properties": {
        "recipe": {
          "type": "string",
          "description": "A JSON string representing an Orchard Core recipe to import. {{RecipeSchemaInstruction}}"
        }
      },
      "required": [
        "recipe"
      ],
      "additionalProperties": false
    }
    """);

    /// <summary>
    /// Orchard Core's permission to run recipes. A recipe can change anything on the site.
    /// </summary>
    protected static readonly Permission ManageRecipesPermission = new("ManageRecipes");

    public override JsonElement JsonSchema => _jsonSchema;

    /// <summary>
    /// Gets the permission the caller must hold for the tool to import its recipe. It runs for whoever drives the chat, a
    /// public visitor included, so it never imports with the system's authority alone.
    /// </summary>
    protected virtual Permission RequiredPermission => ManageRecipesPermission;

    /// <summary>
    /// Whether the caller holds <see cref="RequiredPermission"/>.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    protected Task<bool> IsCallerAuthorizedAsync(IServiceProvider services)
        => AIToolCallerAuthorization.IsAuthorizedAsync(services, RequiredPermission);

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        ArgumentNullException.ThrowIfNull(arguments.Services);

        if (!await IsCallerAuthorizedAsync(arguments.Services))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var logger = arguments.Services.GetRequiredService<ILogger<ImportRecipeBaseTool>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", Name);
        }

        if (!arguments.TryGetFirstString("recipe", out var recipe))
        {
            logger.LogWarning("AI tool '{ToolName}' missing required argument 'recipe'.", Name);
            RecordImport(AIModuleTelemetry.ModeInline, AIModuleTelemetry.OutcomeInvalid);

            return MissingArgument();
        }

        return await ProcessRecipeAsync(arguments.Services, recipe, logger, cancellationToken);
    }

    protected static string MissingArgument(string name = "recipe")
    {
        return $"Unable to find a '{name}' argument in the arguments parameter.";
    }

    protected static async ValueTask<object> ProcessRecipeAsync(IServiceProvider services, string json, ILogger logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        var recipeExecutionService = services.GetRequiredService<RecipeExecutionService>();
        var recipeSchemaService = services.GetRequiredService<RecipeSchemaService>();
        var data = JsonSerializer.Deserialize<JsonObject>(json, RecipeSerializerOptions);
        var rootSchema = await recipeSchemaService.GetRecipeSchemaAsync(cancellationToken);

        if (data is null)
        {
            logger.LogWarning("AI tool recipe import failed: recipe payload could not be deserialized.");
            RecordImport(AIModuleTelemetry.ModeInline, AIModuleTelemetry.OutcomeInvalid);

            return "Invalid recipe format. The recipe payload could not be deserialized.";
        }

        var result = rootSchema.Evaluate(JsonSerializer.SerializeToElement(data, RecipeSerializerOptions), new EvaluationOptions()
        {
            OutputFormat = OutputFormat.List,
        });

        if (!result.IsValid)
        {
            logger.LogWarning("AI tool recipe import failed: invalid recipe format.");
            RecordImport(AIModuleTelemetry.ModeInline, AIModuleTelemetry.OutcomeInvalid);

            var schemaStructure = JsonSerializer.Serialize(rootSchema);

            return
            $"""
            Invalid recipe format. The recipe must match the expected schema shown below.
            {RecipeSchemaInstruction}
            Please generate a valid recipe and try again:
            {schemaStructure}
            """;
        }

        if (ShellScope.Current is null)
        {
            if (await ExecuteAndRecordAsync(recipeExecutionService, data, AIModuleTelemetry.ModeInline))
            {
                logger.LogInformation("AI tool recipe import completed successfully.");

                return "Recipe was successfully imported";
            }

            logger.LogWarning("AI tool recipe import failed: error occurred during execution.");

            return "Error occurred while trying to import the recipe.";
        }

        var serializedRecipe = JsonSerializer.Serialize(data, RecipeSerializerOptions);

        // Counted when it runs, with how it really ended: the tool has already answered by then.
        DeferredWork.Add("ai_recipe_import", scope => ExecuteDeferredRecipeAsync(scope, serializedRecipe));

        logger.LogInformation("AI tool recipe import scheduled for deferred execution.");

        return "Recipe was successfully imported";
    }

    private static async Task ExecuteDeferredRecipeAsync(ShellScope scope, string serializedRecipe)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(serializedRecipe);

        var recipe = JsonSerializer.Deserialize<JsonObject>(serializedRecipe, RecipeSerializerOptions);
        if (recipe is null)
        {
            return;
        }

        var recipeExecutionService = scope.ServiceProvider.GetRequiredService<RecipeExecutionService>();

        if (!await ExecuteAndRecordAsync(recipeExecutionService, recipe, AIModuleTelemetry.ModeDeferred))
        {
            AICoreLog.DeferredRecipeImportFailed(scope.ServiceProvider.GetRequiredService<ILogger<ImportRecipeBaseTool>>());
        }
    }

    private static async Task<bool> ExecuteAndRecordAsync(RecipeExecutionService recipeExecutionService, JsonObject recipe, string mode)
    {
        bool succeeded;

        try
        {
            succeeded = await recipeExecutionService.ExecuteRecipeAsync(recipe);
        }
        catch (Exception ex)
        {
            RecordImport(mode, CrestAppsOrchardCoreTelemetry.GetOutcome(ex));

            throw;
        }

        RecordImport(mode, succeeded ? CrestAppsOrchardCoreTelemetry.OutcomeOk : CrestAppsOrchardCoreTelemetry.OutcomeError);

        return succeeded;
    }

    private static void RecordImport(string mode, string outcome)
    {
        AIModuleTelemetry.Add(AIModuleTelemetry.RecipeImports, new TagList
        {
            { AIModuleTelemetry.ModeTag, mode },
            { CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome },
        });
    }
}
