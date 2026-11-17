using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement;
using OrchardCore.Secrets;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Agent.Settings;

/// <summary>
/// Replays a site settings page the way the Settings admin controller does: it builds the editor of a settings group
/// through the site display drivers, reads the editor view models the drivers filled, and posts changed values back
/// through the same drivers, so validation, secret handling, side effects and the tenant reload stay theirs.
/// </summary>
/// <remarks>
/// The editor view models are filled when the editor is built: <c>ShapeResult</c> runs a driver's initializer while
/// it builds and places the shape, before anything is rendered. Each view model is the shape itself (a proxy that
/// derives from the view model type), and its <c>Metadata.Prefix</c> is the form prefix the driver binds with.
/// </remarks>
internal static class SiteSettingsEditor
{
    /// <summary>
    /// The zone that holds the save button of every settings page. The controller ignores it when it decides whether
    /// a group has an editor.
    /// </summary>
    public const string ActionsZone = "Actions";

    /// <summary>
    /// The stereotype of the content types whose items are custom settings groups.
    /// </summary>
    public const string CustomSettingsStereotype = "CustomSettings";

    // The wrapper a settings driver adds to the page when saving it reloads the tenant.
    private const string ReloadWarningWrapper = "Settings_Wrapper__Reload";

    private const int MaxDepth = 4;

    private static readonly string[] _secretNameParts =
    [
        "password",
        "secret",
        "token",
        "apikey",
        "connectionstring",
        "privatekey",
    ];

    private static readonly JsonSerializerOptions _valueOptions = new()
    {
        Converters =
        {
            new JsonStringEnumConverter(),
        },
    };

    /// <summary>
    /// Whether a settings group is a custom settings content type, whose editor is a content item editor that this
    /// tool does not replay.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="groupId">The settings group ID.</param>
    /// <returns><see langword="true"/> when the group is a custom settings type.</returns>
    public static async Task<bool> IsCustomSettingsGroupAsync(IServiceProvider services, string groupId)
    {
        var contentDefinitionManager = services.GetService<IContentDefinitionManager>();

        if (contentDefinitionManager is null)
        {
            return false;
        }

        var typeDefinition = await contentDefinitionManager.GetTypeDefinitionAsync(groupId);

        return typeDefinition?.StereotypeEquals(CustomSettingsStereotype) == true;
    }

    /// <summary>
    /// Builds the editor of a settings group as the settings page does and reads the view models the drivers filled.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="httpContext">The request of the caller.</param>
    /// <param name="site">The site settings to build the editor from.</param>
    /// <param name="groupId">The settings group ID.</param>
    /// <returns>The editor, or <see langword="null"/> when the group has no editor the caller can see.</returns>
    public static async Task<SettingsEditor> BuildAsync(IServiceProvider services, HttpContext httpContext, ISite site, string groupId)
    {
        var displayManager = services.GetRequiredService<IDisplayManager<ISite>>();
        var updater = new FormValuesModelUpdater(services, httpContext, null);

        var shape = await displayManager.BuildEditorAsync(site, updater, false, groupId);

        return Capture(services, shape);
    }

    /// <summary>
    /// Reads the editor view models of a built or updated settings editor.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="shape">The root shape the display manager returned.</param>
    /// <returns>The editor, or <see langword="null"/> when the shape has no editor outside the actions zone.</returns>
    public static SettingsEditor Capture(IServiceProvider services, IShape shape)
    {
        // As the controller does: a group whose editor has nothing outside the actions zone is not found.
        if (shape is null || !shape.HasItemsInAnyZone(ActionsZone))
        {
            return null;
        }

        var metadataProvider = services.GetRequiredService<IModelMetadataProvider>();
        var editor = new SettingsEditor
        {
            ReloadsTenant = shape.Metadata.Wrappers.Contains(ReloadWarningWrapper),
        };

        foreach (var zone in shape.Properties)
        {
            if (string.Equals(zone.Key, ActionsZone, StringComparison.OrdinalIgnoreCase) || zone.Value is not IShape zoneShape)
            {
                continue;
            }

            foreach (var item in zoneShape.Items ?? [])
            {
                if (item is IShape child)
                {
                    Visit(metadataProvider, editor, child);
                }
            }
        }

        return editor;
    }

    /// <summary>
    /// Checks the changes the model sent against the fields of the editor and puts them in the editor's spelling.
    /// </summary>
    /// <param name="editor">The editor built from the current settings.</param>
    /// <param name="changes">The changes, keyed by section and then by field.</param>
    /// <param name="normalized">The changes with the section and field names of the editor.</param>
    /// <returns>The problems found, empty when the changes can be posted.</returns>
    public static List<string> ValidateChanges(SettingsEditor editor, JsonObject changes, out Dictionary<string, JsonObject> normalized)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(changes);

        var errors = new List<string>();

        normalized = new Dictionary<string, JsonObject>(StringComparer.Ordinal);

        foreach (var sectionChange in changes)
        {
            var section = editor.Sections.FirstOrDefault(s => string.Equals(s.Name, sectionChange.Key, StringComparison.OrdinalIgnoreCase));

            if (section is null)
            {
                errors.Add($"There is no section '{sectionChange.Key}' in this settings group. The sections are: {string.Join(", ", editor.Sections.Select(s => s.Name))}.");

                continue;
            }

            if (sectionChange.Value is not JsonObject fields)
            {
                errors.Add($"The changes of the section '{section.Name}' must be an object of field names and values.");

                continue;
            }

            var target = new JsonObject();

            ValidateFields(section.Name, section.Fields, fields, target, errors);

            normalized[section.Name] = target;
        }

        return errors;
    }

    /// <summary>
    /// Builds the form values a settings page post would send: every field of every section the editor was built
    /// with, with the changes merged over the current values. Every driver of the group binds its whole view model
    /// on save, so a field left out would be saved as its default.
    /// </summary>
    /// <param name="editor">The editor built from the current settings.</param>
    /// <param name="changes">The validated changes, keyed by section.</param>
    /// <returns>The form values.</returns>
    public static Dictionary<string, StringValues> BuildFormValues(SettingsEditor editor, IReadOnlyDictionary<string, JsonObject> changes)
    {
        ArgumentNullException.ThrowIfNull(editor);

        var values = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);

        foreach (var section in editor.Sections)
        {
            var merged = (JsonObject)section.Values.DeepClone();

            if (changes is not null && changes.TryGetValue(section.Name, out var sectionChanges))
            {
                Merge(merged, sectionChanges);
            }

            Flatten(section.Name, merged, values);
        }

        return values;
    }

    /// <summary>
    /// Describes the values of a section for the model: a secret is replaced by whether it is set, and the reference
    /// to a secret keeps only its source and name.
    /// </summary>
    /// <param name="section">The section.</param>
    /// <returns>The values without any secret.</returns>
    public static JsonObject DescribeValues(SettingsSection section)
    {
        ArgumentNullException.ThrowIfNull(section);

        return DescribeObject(section.Fields, section.Values);
    }

    /// <summary>
    /// Builds the JSON schema of the editable fields of a section, from the view model metadata.
    /// </summary>
    /// <param name="section">The section.</param>
    /// <returns>The schema.</returns>
    public static JsonObject BuildSchema(SettingsSection section)
    {
        ArgumentNullException.ThrowIfNull(section);

        return ObjectSchema(section.Fields);
    }

    /// <summary>
    /// Lists the fields whose described value differs between two editors of the same group.
    /// </summary>
    /// <param name="before">The editor built before the save.</param>
    /// <param name="after">The editor built from the saved settings.</param>
    /// <returns>The changed fields, as <c>section.field</c>.</returns>
    public static List<string> GetChangedFields(SettingsEditor before, SettingsEditor after)
    {
        var changed = new List<string>();

        if (before is null || after is null)
        {
            return changed;
        }

        foreach (var section in after.Sections)
        {
            var previous = before.Sections.FirstOrDefault(s => s.Name == section.Name);
            var previousValues = previous is null ? new JsonObject() : DescribeValues(previous);
            var currentValues = DescribeValues(section);

            foreach (var field in currentValues)
            {
                previousValues.TryGetPropertyValue(field.Key, out var previousValue);

                if (!JsonNode.DeepEquals(previousValue, field.Value))
                {
                    changed.Add($"{section.Name}.{field.Key}");
                }
            }
        }

        return changed;
    }

    private static void Visit(IModelMetadataProvider metadataProvider, SettingsEditor editor, IShape shape)
    {
        var modelType = GetViewModelType(shape);

        if (modelType is null)
        {
            // A shape without a view model: a grouping shape is walked, an editor shape is not readable here.
            if (shape.Items is { Count: > 0 })
            {
                foreach (var item in shape.Items)
                {
                    if (item is IShape child)
                    {
                        Visit(metadataProvider, editor, child);
                    }
                }
            }
            else if (!string.IsNullOrEmpty(shape.Metadata.Prefix))
            {
                editor.Unsupported.Add($"the editor '{shape.Metadata.Type}' has no view model");
            }

            return;
        }

        var prefix = shape.Metadata.Prefix;

        if (string.IsNullOrEmpty(prefix))
        {
            editor.Unsupported.Add($"the editor '{shape.Metadata.Type}' has no form prefix");

            return;
        }

        var section = editor.Sections.FirstOrDefault(s => s.Name == prefix);

        if (section is null)
        {
            section = new SettingsSection
            {
                Name = prefix,
            };

            editor.Sections.Add(section);
        }

        // The general group builds three shapes (Site, Resources, Cache) of one view model with one prefix.
        if (section.ModelTypes.Add(modelType))
        {
            var metadata = metadataProvider.GetMetadataForType(modelType);

            AddFields(section.Name, metadata, shape, section.Fields, section.Values, editor.Unsupported, 0);
        }

        foreach (var item in shape.Items ?? [])
        {
            if (item is IShape child)
            {
                Visit(metadataProvider, editor, child);
            }
        }
    }

    private static Type GetViewModelType(IShape shape)
    {
        var type = shape.GetType();

        // A strongly typed shape is a proxy type generated at run time that derives from the view model type.
        while (type.Assembly.IsDynamic && type.BaseType is not null)
        {
            type = type.BaseType;
        }

        return typeof(IShape).IsAssignableFrom(type)
            ? null
            : type;
    }

    private static void AddFields(string path, ModelMetadata metadata, object model, List<SettingsField> fields, JsonObject values, List<string> unsupported, int depth)
    {
        foreach (var property in metadata.Properties)
        {
            if (!property.IsBindingAllowed || property.IsReadOnly || string.IsNullOrEmpty(property.PropertyName))
            {
                continue;
            }

            if (fields.Any(field => string.Equals(field.Name, property.PropertyName, StringComparison.Ordinal)))
            {
                continue;
            }

            var value = model is null ? null : property.PropertyGetter(model);
            var fieldPath = $"{path}.{property.PropertyName}";

            if (!property.IsComplexType)
            {
                var kind = property.UnderlyingOrModelType == typeof(string) && IsSecretName(property.PropertyName)
                    ? SettingsFieldKind.Secret
                    : SettingsFieldKind.Value;

                fields.Add(new SettingsField(property.PropertyName, kind, property));
                values[property.PropertyName] = ToNode(value, property.ModelType);

                continue;
            }

            if (property.ModelType == typeof(SecretInputViewModel))
            {
                var secret = value as SecretInputViewModel;

                fields.Add(new SettingsField(property.PropertyName, SettingsFieldKind.SecretReference, property));
                values[property.PropertyName] = new JsonObject
                {
                    [nameof(SecretInputViewModel.Source)] = (secret?.Source ?? SecretInputSource.Value).ToString(),
                    [nameof(SecretInputViewModel.SecretName)] = secret?.SecretName,
                    [nameof(SecretInputViewModel.Value)] = secret?.Value,
                    [nameof(SecretInputViewModel.HasValue)] = secret?.HasValue ?? false,
                };

                continue;
            }

            if (property.IsEnumerableType)
            {
                var element = property.ElementMetadata;

                // Option lists the editor shows (time zones, cultures...) are not posted back by the page.
                if (element?.ModelType == typeof(SelectListItem) || element?.ModelType == typeof(SelectListGroup))
                {
                    continue;
                }

                if (element is not null && !element.IsComplexType)
                {
                    fields.Add(new SettingsField(property.PropertyName, SettingsFieldKind.List, property));
                    values[property.PropertyName] = ToArray(value as IEnumerable, element.ModelType);

                    continue;
                }

                unsupported.Add($"the field '{fieldPath}' is a list of objects");

                continue;
            }

            if (depth >= MaxDepth || property.ModelType == typeof(object))
            {
                unsupported.Add($"the field '{fieldPath}' cannot be read here");

                continue;
            }

            var children = new List<SettingsField>();
            var childValues = new JsonObject();

            AddFields(fieldPath, property, value, children, childValues, unsupported, depth + 1);

            fields.Add(new SettingsField(property.PropertyName, SettingsFieldKind.Object, property)
            {
                Children = children,
            });
            values[property.PropertyName] = value is null ? null : childValues;
        }
    }

    private static bool IsSecretName(string name)
    {
        if (name.EndsWith("SecretName", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return _secretNameParts.Any(part => name.Contains(part, StringComparison.OrdinalIgnoreCase));
    }

    private static JsonNode ToNode(object value, Type type)
    {
        if (value is null)
        {
            return null;
        }

        return JsonSerializer.SerializeToNode(value, type, _valueOptions);
    }

    private static JsonArray ToArray(IEnumerable values, Type elementType)
    {
        if (values is null)
        {
            return null;
        }

        var array = new JsonArray();

        foreach (var value in values)
        {
            array.Add(ToNode(value, elementType));
        }

        return array;
    }

    private static void ValidateFields(string path, List<SettingsField> fields, JsonObject changes, JsonObject target, List<string> errors)
    {
        foreach (var change in changes)
        {
            var field = fields.FirstOrDefault(f => string.Equals(f.Name, change.Key, StringComparison.OrdinalIgnoreCase));
            var fieldPath = $"{path}.{change.Key}";

            if (field is null)
            {
                errors.Add($"'{fieldPath}' is not a field of this settings page. Use getSiteSettings to see the fields.");

                continue;
            }

            switch (field.Kind)
            {
                case SettingsFieldKind.Secret:
                    errors.Add($"'{path}.{field.Name}' is a secret; it is never accepted in the chat. A person enters it on the settings page, or the setting can name a secret of the Secrets module where the page offers that.");
                    break;

                case SettingsFieldKind.SecretReference:
                    if (change.Value is not JsonObject reference)
                    {
                        errors.Add($"'{path}.{field.Name}' takes an object with Source (Value or Secret) and SecretName.");
                        break;
                    }

                    var referenceTarget = new JsonObject();

                    foreach (var part in reference)
                    {
                        if (string.Equals(part.Key, nameof(SecretInputViewModel.Source), StringComparison.OrdinalIgnoreCase))
                        {
                            var source = part.Value is JsonValue sourceValue && sourceValue.TryGetValue<string>(out var text) ? text : null;

                            if (!Enum.TryParse<SecretInputSource>(source, true, out var parsed))
                            {
                                errors.Add($"'{path}.{field.Name}.Source' must be Value or Secret.");
                                continue;
                            }

                            referenceTarget[nameof(SecretInputViewModel.Source)] = parsed.ToString();
                        }
                        else if (string.Equals(part.Key, nameof(SecretInputViewModel.SecretName), StringComparison.OrdinalIgnoreCase))
                        {
                            referenceTarget[nameof(SecretInputViewModel.SecretName)] = part.Value?.DeepClone();
                        }
                        else
                        {
                            errors.Add($"'{path}.{field.Name}.{part.Key}' cannot be set here: a secret value is never accepted in the chat. Set Source to Secret and SecretName to the name of a secret, or let a person enter the value on the settings page.");
                        }
                    }

                    target[field.Name] = referenceTarget;
                    break;

                case SettingsFieldKind.List:
                    if (change.Value is not null and not JsonArray)
                    {
                        errors.Add($"'{path}.{field.Name}' takes a list.");
                        break;
                    }

                    target[field.Name] = change.Value?.DeepClone();
                    break;

                case SettingsFieldKind.Object:
                    if (change.Value is not JsonObject nested)
                    {
                        errors.Add($"'{path}.{field.Name}' takes an object.");
                        break;
                    }

                    var nestedTarget = new JsonObject();

                    ValidateFields($"{path}.{field.Name}", field.Children, nested, nestedTarget, errors);

                    target[field.Name] = nestedTarget;
                    break;

                default:
                    if (change.Value is JsonObject or JsonArray)
                    {
                        errors.Add($"'{path}.{field.Name}' takes a single value.");
                        break;
                    }

                    target[field.Name] = change.Value?.DeepClone();
                    break;
            }
        }
    }

    private static void Merge(JsonObject target, JsonObject changes)
    {
        foreach (var change in changes)
        {
            if (change.Value is JsonObject nested && target[change.Key] is JsonObject existing)
            {
                Merge(existing, nested);

                continue;
            }

            target[change.Key] = change.Value?.DeepClone();
        }
    }

    private static void Flatten(string key, JsonNode node, Dictionary<string, StringValues> values)
    {
        switch (node)
        {
            case null:
                return;

            case JsonObject jsonObject:
                foreach (var property in jsonObject)
                {
                    // The page never posts whether a secret is set; the view model ignores it anyway.
                    if (property.Key == nameof(SecretInputViewModel.HasValue))
                    {
                        continue;
                    }

                    Flatten($"{key}.{property.Key}", property.Value, values);
                }

                return;

            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    Flatten($"{key}[{i.ToString(CultureInfo.InvariantCulture)}]", array[i], values);
                }

                return;

            case JsonValue value:
                values[key] = value.GetValueKind() switch
                {
                    JsonValueKind.String => value.GetValue<string>(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => value.ToJsonString(),
                };

                return;
        }
    }

    private static JsonObject DescribeObject(List<SettingsField> fields, JsonObject values)
    {
        var result = new JsonObject();

        foreach (var field in fields)
        {
            values.TryGetPropertyValue(field.Name, out var value);

            result[field.Name] = field.Kind switch
            {
                SettingsFieldKind.Secret => new JsonObject
                {
                    ["hasValue"] = value is JsonValue secret && secret.TryGetValue<string>(out var text) && !string.IsNullOrEmpty(text),
                },
                SettingsFieldKind.SecretReference => value is JsonObject reference
                    ? new JsonObject
                    {
                        [nameof(SecretInputViewModel.Source)] = reference[nameof(SecretInputViewModel.Source)]?.DeepClone(),
                        [nameof(SecretInputViewModel.SecretName)] = reference[nameof(SecretInputViewModel.SecretName)]?.DeepClone(),
                        [nameof(SecretInputViewModel.HasValue)] = reference[nameof(SecretInputViewModel.HasValue)]?.DeepClone(),
                    }
                    : null,
                SettingsFieldKind.Object => value is JsonObject nested
                    ? DescribeObject(field.Children, nested)
                    : null,
                _ => value?.DeepClone(),
            };
        }

        return result;
    }

    private static JsonObject ObjectSchema(List<SettingsField> fields)
    {
        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var field in fields)
        {
            properties[field.Name] = FieldSchema(field);

            if (field.Kind is SettingsFieldKind.Value or SettingsFieldKind.List &&
                field.Metadata.ValidatorMetadata.OfType<RequiredAttribute>().Any())
            {
                required.Add(field.Name);
            }
        }

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["additionalProperties"] = false,
        };

        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        return schema;
    }

    private static JsonObject FieldSchema(SettingsField field)
    {
        JsonObject schema;

        switch (field.Kind)
        {
            case SettingsFieldKind.Secret:
                schema = new JsonObject
                {
                    ["type"] = "object",
                    ["readOnly"] = true,
                    ["description"] = "A secret. Only whether it is set is shown, and a value is never accepted here; a person enters it on the settings page.",
                };

                return schema;

            case SettingsFieldKind.SecretReference:
                schema = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        [nameof(SecretInputViewModel.Source)] = new JsonObject
                        {
                            ["type"] = "string",
                            ["enum"] = new JsonArray(nameof(SecretInputSource.Value), nameof(SecretInputSource.Secret)),
                            ["description"] = "Where the credential is kept: Value (entered on the settings page and kept protected with the settings) or Secret (a secret of the Secrets module, named in SecretName).",
                        },
                        [nameof(SecretInputViewModel.SecretName)] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "The name of the secret of the Secrets module to use when Source is Secret.",
                        },
                        [nameof(SecretInputViewModel.HasValue)] = new JsonObject
                        {
                            ["type"] = "boolean",
                            ["readOnly"] = true,
                            ["description"] = "Whether a protected value is kept with the settings. It cannot be changed here.",
                        },
                    },
                    ["additionalProperties"] = false,
                    ["description"] = "A credential. Its value is never shown or accepted here; only where it is kept and the secret name can change.",
                };

                return schema;

            case SettingsFieldKind.List:
                schema = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = ValueSchema(field.Metadata.ElementMetadata),
                };

                break;

            case SettingsFieldKind.Object:
                schema = ObjectSchema(field.Children);

                break;

            default:
                schema = ValueSchema(field.Metadata);

                break;
        }

        var description = GetDescription(field.Metadata);

        if (!string.IsNullOrEmpty(description))
        {
            schema["description"] = description;
        }

        return schema;
    }

    private static JsonObject ValueSchema(ModelMetadata metadata)
    {
        var type = metadata?.UnderlyingOrModelType ?? typeof(string);
        var schema = new JsonObject();

        if (type.IsEnum)
        {
            schema["type"] = "string";
            schema["enum"] = new JsonArray([.. Enum.GetNames(type).Select(name => (JsonNode)name)]);
        }
        else if (type == typeof(bool))
        {
            schema["type"] = "boolean";
        }
        else if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) ||
            type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte))
        {
            schema["type"] = "integer";
        }
        else if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
        {
            schema["type"] = "number";
        }
        else
        {
            schema["type"] = "string";
        }

        return schema;
    }

    private static string GetDescription(ModelMetadata metadata)
    {
        if (!string.IsNullOrWhiteSpace(metadata.Description))
        {
            return metadata.Description;
        }

        var displayName = metadata.DisplayName;

        return !string.IsNullOrWhiteSpace(displayName) && !string.Equals(displayName, metadata.PropertyName, StringComparison.Ordinal)
            ? displayName
            : null;
    }
}

/// <summary>
/// The editor of one settings group, read from the view models its display drivers filled.
/// </summary>
internal sealed class SettingsEditor
{
    /// <summary>
    /// Gets the sections of the editor, one per form prefix (one per settings class, in practice).
    /// </summary>
    public List<SettingsSection> Sections { get; } = [];

    /// <summary>
    /// Gets the reasons the editor cannot be posted back from the chat, empty when it can.
    /// </summary>
    public List<string> Unsupported { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the page warns that saving it reloads the tenant.
    /// </summary>
    public bool ReloadsTenant { get; set; }
}

/// <summary>
/// The fields of one form prefix of a settings editor and the values the drivers filled them with.
/// </summary>
internal sealed class SettingsSection
{
    /// <summary>
    /// Gets or sets the form prefix, such as <c>ISite.AdminSettings</c>.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets the editable fields.
    /// </summary>
    public List<SettingsField> Fields { get; } = [];

    /// <summary>
    /// Gets the values of the fields, secrets included. They never leave the tool; see <see cref="SiteSettingsEditor.DescribeValues"/>.
    /// </summary>
    public JsonObject Values { get; } = new();

    /// <summary>
    /// Gets the view model types read for the prefix.
    /// </summary>
    public HashSet<Type> ModelTypes { get; } = [];
}

/// <summary>
/// One editable field of a settings section.
/// </summary>
internal sealed class SettingsField
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsField"/> class.
    /// </summary>
    /// <param name="name">The property name of the view model.</param>
    /// <param name="kind">How the field is read and written.</param>
    /// <param name="metadata">The model metadata of the property.</param>
    public SettingsField(string name, SettingsFieldKind kind, ModelMetadata metadata)
    {
        Name = name;
        Kind = kind;
        Metadata = metadata;
    }

    /// <summary>
    /// Gets the property name of the view model.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets how the field is read and written.
    /// </summary>
    public SettingsFieldKind Kind { get; }

    /// <summary>
    /// Gets the model metadata of the property.
    /// </summary>
    public ModelMetadata Metadata { get; }

    /// <summary>
    /// Gets or sets the fields of a nested object.
    /// </summary>
    public List<SettingsField> Children { get; set; } = [];
}

/// <summary>
/// How a settings field is read and written.
/// </summary>
internal enum SettingsFieldKind
{
    /// <summary>
    /// A single value: text, number, true or false, or one of a list of names.
    /// </summary>
    Value,

    /// <summary>
    /// A text that holds a credential: only whether it is set is shown, and it is never written.
    /// </summary>
    Secret,

    /// <summary>
    /// A credential edited with the secret input: its source and secret name can change, its value cannot.
    /// </summary>
    SecretReference,

    /// <summary>
    /// A list of single values.
    /// </summary>
    List,

    /// <summary>
    /// A nested object of fields.
    /// </summary>
    Object,
}
