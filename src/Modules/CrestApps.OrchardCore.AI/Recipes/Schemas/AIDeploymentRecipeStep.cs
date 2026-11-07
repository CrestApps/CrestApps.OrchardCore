using CrestApps.Core.AI;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.Recipes.Schemas;

/// <summary>
/// Schema for the "AIDeployment" recipe step — creates or updates AI model deployments.
/// </summary>
public sealed class AIDeploymentRecipeStep : IRecipeStep
{
    private readonly AIOptions _aiOptions;
    private readonly AIDeploymentCapabilityOptions _capabilityOptions;
    private JsonSchema _cached;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDeploymentRecipeStep"/> class.
    /// </summary>
    /// <param name="aiOptions">The AI options.</param>
    /// <param name="capabilityOptions">The registered model feature and parameter definitions.</param>
    public AIDeploymentRecipeStep(
        IOptions<AIOptions> aiOptions,
        IOptions<AIDeploymentCapabilityOptions> capabilityOptions)
    {
        _aiOptions = aiOptions.Value;
        _capabilityOptions = capabilityOptions.Value;
    }

    public string Name => "AIDeployment";

    /// <summary>
    /// Retrieves the schema async.
    /// </summary>
    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= CreateSchema();

        return ValueTask.FromResult(_cached);
    }

    private JsonSchema CreateSchema()
    {
        var azureAuthenticationTypeSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Enum("Default", "ManagedIdentity", "ApiKey")
            .Description("Azure authentication type. Supported values are Default, ManagedIdentity, or ApiKey.");

        // The purpose was replaced by model capabilities. A recipe that still names one is read through the
        // framework's read-time projection, so it keeps importing; new recipes declare AIDeploymentMetadata
        // under Properties instead.
        var deploymentPurposeSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Enum("Chat", "Utility", "Embedding", "Image", "SpeechToText", "TextToSpeech", "Vision")
            .Description("Legacy deployment purpose identifier, projected onto model capabilities on import.");

        var modelCapabilitiesSchema = BuildModelCapabilitiesSchema();
        var pricingSchema = BuildPricingSchema();

        var containedConnectionPropertiesSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Endpoint", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Contained-connection endpoint. Used by AzureSpeech deployments.")),
                ("AuthenticationType", azureAuthenticationTypeSchema),
                ("ApiKey", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Contained-connection API key. Required when AuthenticationType is ApiKey.")),
                ("IdentityId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional client ID of a user-assigned managed identity. Used when AuthenticationType is ManagedIdentity.")),
                ("AIDeploymentMetadata", modelCapabilitiesSchema),
                ("AIDeploymentPricing", pricingSchema))
            .AdditionalProperties(true)
            .Description("Properties stored directly on the deployment, such as contained provider connection settings and the declared model capabilities (AIDeploymentMetadata).");

        var clientNameSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description("Client name for the registered AI provider.");
        var clientNames = _aiOptions.CompletionClients.Values
            .Select(static entry => entry.ClientName)
            .Concat(_aiOptions.Deployments.Keys)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (clientNames.Length > 0)
        {
            clientNameSchema = clientNameSchema.WithSuggestions(clientNames);
        }

        var deploymentSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Deployment name as specified by the vendor.")),
                ("ModelName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Optional vendor model name. Defaults to Name when omitted.")),
                ("ClientName", clientNameSchema),
                ("ConnectionName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Connection name used to configure the provider.")),
                ("Endpoint", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Contained-connection endpoint alias for recipe imports. Supported by AzureSpeech deployments.")),
                ("AuthenticationType", azureAuthenticationTypeSchema.Description("Contained-connection authentication type alias for recipe imports. Supported by AzureSpeech deployments.")),
                ("ApiKey", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Contained-connection API key alias for recipe imports. Supported by AzureSpeech deployments.")),
                ("IdentityId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Contained-connection managed identity client ID alias for recipe imports. Supported by AzureSpeech deployments.")),
                ("Properties", containedConnectionPropertiesSchema.Description("Contained provider connection properties stored directly on the deployment.")),
                ("Pricing", new JsonSchemaBuilder().AnyOf(
                    pricingSchema,
                    new JsonSchemaBuilder().Type(SchemaValueType.Null).Description("Removes the deployment's prices.")).Description("What the deployment costs with its provider, used to estimate the cost of its usage in the AI usage reports. Leave it out to keep the deployment's current prices.")),
                ("Purpose", new JsonSchemaBuilder().AnyOf(
                    deploymentPurposeSchema.Description("Legacy. Declare the model's capabilities under Properties.AIDeploymentMetadata instead. A deployment that names neither is imported as a text chat deployment."),
                    new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(
                        deploymentPurposeSchema).MinItems(1).UniqueItems(true).Description("Legacy deployment purposes, projected onto model capabilities on import."))))
            .Required("Name")
            .AdditionalProperties(true);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const("AIDeployment").Description("Recipe step discriminator. Must be 'AIDeployment'.")),
                ("Deployments", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(deploymentSchema)
                    .MinItems(1)
                    .Description("The AI deployments to create or update.")))
            .Required("name", "Deployments")
            .AdditionalProperties(true)
            .Build();
    }

    /// <summary>
    /// Builds the schema for a deployment's pricing: a currency and the prices, each an amount for a quantity of one
    /// billed unit.
    /// </summary>
    private static JsonSchemaBuilder BuildPricingSchema()
    {
        var meterNames = AIUsageMeters.All.Select(static meter => meter.Name).ToArray();

        var priceSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Meter", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum(meterNames)
                    .Description("The billed unit. Cached, cache-write, audio, and image tokens are taken out of the input and output tokens when they have their own price.")),
                ("Amount", new JsonSchemaBuilder().Type(SchemaValueType.Number).Minimum(0).Description("What Per units cost, in the pricing's currency.")),
                ("Per", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Description("How many of the meter's base units Amount pays for, such as 1000000 for a price per million tokens, or 60 for a price per minute of audio (AudioSeconds).")),
                ("CountName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("For the AdditionalCount meter: the name of the count the provider reports, as shown in the usage report.")),
                ("AboveInputTokens", new JsonSchemaBuilder().Type(SchemaValueType.Integer | SchemaValueType.Null).Minimum(1).Description("Long-context price: applies instead of the meter's regular price to requests with more input tokens than this.")),
                ("Increment", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Description("Rounds each request's quantity up to a multiple of this many base units, such as 15 for audio billed in 15-second increments. 0 bills the exact quantity.")))
            .Required("Meter", "Amount", "Per")
            .AdditionalProperties(false);

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Currency", new JsonSchemaBuilder().Type(SchemaValueType.String).Pattern("^[A-Za-z]{3}$").Description("The ISO currency code the prices are in, such as USD. Defaults to USD.")),
                ("Prices", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(priceSchema).Description("The prices, one per billed unit. Several prices for the same meter add up.")))
            .AdditionalProperties(false)
            .Description("What the deployment costs with its provider.");
    }

    /// <summary>
    /// Builds the schema for the <c>CascadedRealtimeMetadata</c> property, which names the three deployments
    /// a cascaded realtime deployment chains together to answer speech with speech.
    /// </summary>
    private static JsonSchemaBuilder BuildCascadedRealtimeSchema()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("SpeechToTextDeploymentName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Technical name of the deployment that transcribes the user's speech. Must expose a realtime client.")),
                ("ChatDeploymentName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Technical name of the chat deployment that generates the reply.")),
                ("TextToSpeechDeploymentName", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Technical name of the deployment that speaks the reply.")))
            .Required("SpeechToTextDeploymentName", "ChatDeploymentName", "TextToSpeechDeploymentName")
            .AdditionalProperties(false)
            .Description("Set on a deployment created under the CascadedRealtime client to chain a speech-to-text, a chat, and a text-to-speech deployment into one realtime deployment.");
    }

    /// <summary>
    /// Builds the schema for the model capabilities metadata stored on a deployment under the
    /// <c>AIDeploymentMetadata</c> property. Declares the trained features the deployment supports and the
    /// per-parameter narrowing (allowed values, default, and numeric range) it exposes to profiles,
    /// profile templates, and chat interactions.
    /// </summary>
    private JsonSchemaBuilder BuildModelCapabilitiesSchema()
    {
        var featureNames = _capabilityOptions.Features.Keys
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var parameterNames = _capabilityOptions.Parameters.Keys
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var featureItemSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Description("Technical name of a registered model feature the underlying model was trained with, for example textGeneration, toolCalling, reasoning, imageInput, audioInput, or realtime.");

        if (featureNames.Length > 0)
        {
            featureItemSchema = featureItemSchema.WithSuggestions(featureNames);
        }

        // The per-parameter narrowing an operator can declare on the deployment. Every member is optional
        // and narrows the globally registered parameter descriptor.
        var parameterMetadataSchema = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("AllowedValues", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                    .Description("Subset of the registered allowed values this deployment supports. Omit or leave empty to support every registered value.")),
                ("DefaultValue", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("Value applied when an operator does not select one.")),
                ("Minimum", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("Inclusive minimum accepted value for numeric parameters.")),
                ("Maximum", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("Inclusive maximum accepted value for numeric parameters.")),
                ("Step", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("Increment applied by numeric editors.")))
            .AdditionalProperties(true)
            .Description("Per-deployment narrowing of a registered model parameter.");

        var parametersSchemaBuilder = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(parameterMetadataSchema)
            .Description("Supported model parameters keyed by their registered technical name (for example reasoningEffort). A parameter that is not present is not exposed by the deployment.");

        if (parameterNames.Length > 0)
        {
            parametersSchemaBuilder = parametersSchemaBuilder.Properties(
                parameterNames.Select(name => (name, parameterMetadataSchema)).ToArray());
        }

        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Features", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(featureItemSchema)
                    .UniqueItems(true)
                    .Description("Technical names of the trained model features this deployment supports. Only declared features are offered in editors and sent to the provider.")),
                ("Parameters", parametersSchemaBuilder))
            .AdditionalProperties(true)
            .Description("Declared model capabilities for the deployment: the trained features it supports and the configurable model parameters it exposes.");
    }
}
