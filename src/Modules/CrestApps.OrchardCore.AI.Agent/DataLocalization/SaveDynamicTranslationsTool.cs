using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using OrchardCore.Localization.Data;

namespace CrestApps.OrchardCore.AI.Agent.DataLocalization;

/// <summary>
/// Adds or changes dynamic translations of one culture through the Translations recipe step of the Data Localization
/// module, which merges them into the culture's saved translations, so strings not sent keep theirs. The caller needs
/// the permission the Dynamic Translations page checks before it saves a culture.
/// </summary>
public sealed class SaveDynamicTranslationsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveDynamicTranslations";

    private const string StepName = "Translations";

    private const int MaxTranslations = 200;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "culture": {
          "type": "string",
          "description": "The culture of the translations, one of the site's supported cultures, such as fr-FR."
        },
        "translations": {
          "type": "array",
          "minItems": 1,
          "maxItems": 200,
          "description": "The translations to add or change, each for one string from listDynamicTranslations.",
          "items": {
            "type": "object",
            "properties": {
              "context": {
                "type": "string",
                "description": "The context of the string, exactly as listDynamicTranslations returns it."
              },
              "key": {
                "type": "string",
                "description": "The string to translate, exactly as listDynamicTranslations returns it."
              },
              "value": {
                "type": "string",
                "description": "The translation. It cannot be empty."
              }
            },
            "required": [
              "context",
              "key",
              "value"
            ],
            "additionalProperties": false
          }
        }
      },
      "required": [
        "culture",
        "translations"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Adds or changes translations of dynamic strings for one culture; strings not sent keep their current translation. The culture must be one of the site's supported cultures.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

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

        if (AgentToolAuthorization.GetCaller(services) is null)
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        arguments.TryGetFirstString("culture", out var culture);

        var supportedCultures = await DynamicTranslationToolSupport.GetSupportedCulturesAsync(services);
        var cultureName = DynamicTranslationToolSupport.FindCulture(supportedCultures, culture);

        // As the Dynamic Translations page does before saving a culture: managing all translations, or the culture's own
        // permission. A caller without the first one learns nothing about the cultures before the check.
        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, DataLocalizationPermissions.ManageTranslations))
        {
            if (cultureName is null ||
                !await AIToolCallerAuthorization.IsAuthorizedAsync(services, DataLocalizationPermissions.CreateCulturePermission(cultureName, DynamicTranslationToolSupport.GetDisplayName(cultureName))))
            {
                return AIToolCallerAuthorization.NotAllowedMessage;
            }
        }

        if (cultureName is null)
        {
            return $"Nothing was saved: the culture '{culture?.Trim()}' is not one of the site's supported cultures ({string.Join(", ", supportedCultures)}). Add it under Settings > Localization > Cultures first.";
        }

        if (!AgentToolArguments.TryReadJsonArray(arguments, "translations", out var given) || given is null || given.Count == 0)
        {
            return "Nothing was saved: give the translations, each with the context and key from listDynamicTranslations and a value.";
        }

        if (given.Count > MaxTranslations)
        {
            return $"Nothing was saved: give at most {MaxTranslations} translations at a time.";
        }

        var known = (await DynamicTranslationToolSupport.GetDescriptorsAsync(services))
            .Select(descriptor => (descriptor.Context, descriptor.Name))
            .ToHashSet();

        var translations = new List<(string Context, string Key, string Value)>();
        var problems = new List<string>();

        foreach (var item in given)
        {
            var context = ReadString(item, "context");
            var key = ReadString(item, "key");
            var value = ReadString(item, "value")?.Trim();

            if (string.IsNullOrEmpty(context) || string.IsNullOrEmpty(key))
            {
                problems.Add("every translation needs the context and the key of a string from listDynamicTranslations");
            }
            else if (string.IsNullOrEmpty(value))
            {
                problems.Add($"the translation of '{key}' ({context}) is empty");
            }
            else if (!known.Contains((context, key)))
            {
                problems.Add($"'{key}' ({context}) is not a translatable string of this site; copy the context and key from listDynamicTranslations");
            }
            else
            {
                translations.Add((context, key, value));
            }
        }

        if (problems.Count > 0)
        {
            return "Nothing was saved: " + string.Join("; ", problems.Distinct()) + ".";
        }

        var step = new JsonObject
        {
            ["name"] = StepName,
            ["translations"] = new JsonArray(translations
                .Select(translation => (JsonNode)new JsonObject
                {
                    ["culture"] = cultureName,
                    ["context"] = translation.Context,
                    ["key"] = translation.Key,
                    ["value"] = translation.Value,
                })
                .ToArray()),
        };

        var errors = await RecipeStepRunner.RunAsync(services, StepName, step);

        if (errors.Count > 0)
        {
            return "The translations were not saved: " + string.Join(" ", errors);
        }

        return AgentToolResults.Json(new
        {
            culture = cultureName,
            saved = translations
                .Select(translation => new
                {
                    context = translation.Context,
                    key = translation.Key,
                    value = translation.Value,
                })
                .ToList(),
            link = AgentToolLinks.GetAdminPath(services, "OrchardCore.DataLocalization", "Admin", "Index", new() { ["culture"] = cultureName }),
            note = "Strings that were not sent keep their translation.",
        });
    }

    private static string ReadString(JsonNode item, string property)
        => item is JsonObject jsonObject && jsonObject[property] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;
}
