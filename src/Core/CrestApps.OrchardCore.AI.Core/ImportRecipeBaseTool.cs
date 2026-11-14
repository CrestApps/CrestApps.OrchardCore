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

    protected static ValueTask<object> ProcessRecipeAsync(IServiceProvider services, string json, ILogger logger, CancellationToken cancellationToken)
        => ProcessRecipeAsync(services, json, logger, allowedSteps: null, cancellationToken);

    /// <summary>
    /// Validates and imports a recipe that may contain only the named steps.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="json">The recipe.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="allowedSteps">
    /// The names of the steps the recipe may contain, or <see langword="null"/> for any step. A tool that needs less than
    /// <c>ManageRecipes</c> names its own steps: a recipe runs every step it carries, so without the list the tool's
    /// permission would import a Roles, Users or settings step too.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    protected static async ValueTask<object> ProcessRecipeAsync(IServiceProvider services, string json, ILogger logger, IReadOnlyCollection<string> allowedSteps, CancellationToken cancellationToken)
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

        if (allowedSteps is not null && !HasOnlySteps(data, allowedSteps))
        {
            logger.LogWarning("AI tool recipe import refused: the recipe has a step the tool may not run.");
            RecordImport(AIModuleTelemetry.ModeInline, AIModuleTelemetry.OutcomeInvalid);

            return $"This tool can only import a recipe whose steps are: {string.Join(", ", allowedSteps)}.";
        }

        // Orchard's recipe executor evaluates a value shaped like [js: ...] or [file: ...] anywhere in the recipe (each
        // step and the variables) before a step runs, and those scripts can read the shell configuration and create,
        // change or delete content. The step list alone would let a narrower permission run them.
        if (allowedSteps is not null && HasDirective(data))
        {
            logger.LogWarning("AI tool recipe import refused: the recipe has a script or file directive.");
            RecordImport(AIModuleTelemetry.ModeInline, AIModuleTelemetry.OutcomeInvalid);

            return "This tool can't import a recipe with a script or file directive such as [js: ...] or [file: ...]. Write every value as plain text.";
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

    private static bool HasOnlySteps(JsonObject recipe, IReadOnlyCollection<string> allowedSteps)
    {
        if (recipe["steps"] is not JsonArray steps)
        {
            return true;
        }

        foreach (var step in steps)
        {
            var name = (step as JsonObject)?["name"] is JsonValue value && value.TryGetValue<string>(out var text)
                ? text
                : null;

            if (name is null || !allowedSteps.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasDirective(JsonNode node)
        => node switch
        {
            JsonObject properties => properties.Any(property => HasDirective(property.Value)),
            JsonArray items => items.Any(HasDirective),
            JsonValue value => value.TryGetValue<string>(out var text) && IsDirective(text),
            _ => false,
        };

    // The test of the pinned Orchard Core's RecipeExecutor.EvaluateJsonTreeAsync: a value that starts with '[', ends with
    // ']' and has only letters between the '[' and its first ':' is evaluated as a script.
    private static bool IsDirective(string text)
    {
        if (text is null || !text.StartsWith('[') || !text.EndsWith(']'))
        {
            return false;
        }

        var colon = text.IndexOf(':');

        return colon > 0 && text.AsSpan(1, colon - 1).ToString().All(char.IsLetter);
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
