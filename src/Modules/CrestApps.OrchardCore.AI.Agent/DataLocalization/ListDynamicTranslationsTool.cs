using System.Globalization;
using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Localization.Data;

namespace CrestApps.OrchardCore.AI.Agent.DataLocalization;

/// <summary>
/// Lists the translatable dynamic strings with the translation one culture has for each, and the translation
/// progress of every supported culture, as the Dynamic Translations page and its Statistics page show them.
/// </summary>
public sealed class ListDynamicTranslationsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listDynamicTranslations";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "culture": {
          "type": "string",
          "description": "The culture whose translations to list, one of the site's supported cultures, such as fr-FR. Leave it out to get only the progress of every culture."
        },
        "context": {
          "type": "string",
          "description": "Text the context of the string must start with, such as 'Content Types' or 'Permissions'. Leave it out to keep every context."
        },
        "search": {
          "type": "string",
          "description": "Text the string or its translation must contain."
        },
        "untranslatedOnly": {
          "type": "boolean",
          "description": "Whether to keep only the strings the culture has no translation for. The default is false."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1. The default is 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of strings on a page. The default is 20 and the most is 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the translatable dynamic strings for one culture with their current translation, filterable by context, search text and \"untranslated only\", and returns translation progress per culture. Use it to find what is missing before translating.";

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

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, DataLocalizationPermissions.ViewDynamicTranslations))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        // Reading now would load the translations from the document as it was before the save, and the site would keep
        // showing them after the save commits.
        if (DynamicTranslationScopeChanges.Get(services).HasChanges)
        {
            return "Translations were saved earlier in this reply and are stored when it ends. List them again in the next reply; the strings just saved are already translated.";
        }

        var resourceManager = services.GetService<DataResourceManager>();

        if (resourceManager is null)
        {
            return "Dynamic translations are not available: the Data Localization feature does not register its services on this site.";
        }

        arguments.TryGetFirstString("culture", out var culture);
        arguments.TryGetFirstString("context", out var context);
        arguments.TryGetFirstString("search", out var search);
        context = context?.Trim();
        search = search?.Trim();

        var untranslatedOnly = arguments.GetFirstValueOrDefault("untranslatedOnly", false);
        var supportedCultures = await DynamicTranslationToolSupport.GetSupportedCulturesAsync(services);
        string cultureName = null;

        if (!string.IsNullOrWhiteSpace(culture))
        {
            cultureName = DynamicTranslationToolSupport.FindCulture(supportedCultures, culture);

            if (cultureName is null)
            {
                return $"The culture '{culture.Trim()}' is not one of the site's supported cultures ({string.Join(", ", supportedCultures)}). Add it under Settings > Localization > Cultures first.";
            }
        }

        var descriptors = await DynamicTranslationToolSupport.GetDescriptorsAsync(services);

        var progress = supportedCultures
            .Select(name =>
            {
                var cultureInfo = CultureInfo.GetCultureInfo(name);
                var translated = descriptors.Count(descriptor => DynamicTranslationToolSupport.GetTranslation(resourceManager, descriptor, cultureInfo) is not null);

                return new
                {
                    culture = name,
                    displayName = DynamicTranslationToolSupport.GetDisplayName(name),
                    translated,
                    total = descriptors.Count,
                    percent = descriptors.Count == 0 ? 100 : Math.Round(translated * 100.0 / descriptors.Count, 1),
                };
            })
            .ToList();

        if (cultureName is null)
        {
            return AgentToolResults.Json(new
            {
                progress,
                link = AgentToolLinks.GetAdminPath(services, "OrchardCore.DataLocalization", "Admin", "Statistics"),
                note = "Give a culture to list its strings and translations.",
            });
        }

        var selectedCulture = CultureInfo.GetCultureInfo(cultureName);

        var strings = descriptors
            .Where(descriptor => string.IsNullOrEmpty(context) || descriptor.Context.StartsWith(context, StringComparison.OrdinalIgnoreCase))
            .Select(descriptor => (Descriptor: descriptor, Translation: DynamicTranslationToolSupport.GetTranslation(resourceManager, descriptor, selectedCulture)))
            .Where(entry => !untranslatedOnly || entry.Translation is null)
            .Where(entry => string.IsNullOrEmpty(search)
                || entry.Descriptor.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || entry.Translation?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
            .OrderBy(entry => entry.Descriptor.Context, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Descriptor.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        return AgentToolResults.Json(new
        {
            culture = cultureName,
            items = strings
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(entry => new
                {
                    context = entry.Descriptor.Context,
                    key = entry.Descriptor.Name,
                    translation = entry.Translation,
                    translated = entry.Translation is not null,
                })
                .ToList(),
            total = strings.Count,
            page,
            pageSize,
            truncated = strings.Count > page * pageSize
                ? true
                : (bool?)null,
            progress,
            link = AgentToolLinks.GetAdminPath(services, "OrchardCore.DataLocalization", "Admin", "Index", new() { ["culture"] = cultureName }),
            note = "To translate, send the culture and each string's context, key and translation to saveDynamicTranslations.",
        });
    }
}
