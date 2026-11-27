using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using OrchardCore.DisplayManagement;
using OrchardCore.Secrets;

namespace CrestApps.OrchardCore.AI.Agent.Editors;

/// <summary>
/// Replays an admin editor the way its controller does: it reads the editor view models the display drivers filled
/// when the editor was built, checks the changes the model sent against them, and builds the form values a post of the
/// page would send, so the drivers bind, normalize and validate them on <c>UpdateEditorAsync</c> exactly as on a form
/// post. Used by the site settings tools and the generic record tools.
/// </summary>
/// <remarks>
/// The editor view models are filled when the editor is built: <c>ShapeResult</c> runs a driver's initializer while
/// it builds and places the shape, before anything is rendered. Each view model is the shape itself (a proxy that
/// derives from the view model type), and its <c>Metadata.Prefix</c> is the form prefix the driver binds with.
/// </remarks>
internal static class EditorReplay
{
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
    /// Reads the editor view models of a built or updated editor into <paramref name="editor"/>.
    /// </summary>
    /// <typeparam name="TEditor">The type of the editor model.</typeparam>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="shape">The root shape the display manager returned.</param>
    /// <param name="editor">The editor model to fill.</param>
    /// <param name="options">The zones to skip and the fields to treat specially; <see langword="null"/> for none.</param>
    /// <returns>The editor, or <see langword="null"/> when the shape has nothing in any zone that is not skipped.</returns>
    public static TEditor Capture<TEditor>(IServiceProvider services, IShape shape, TEditor editor, EditorCaptureOptions options = null)
        where TEditor : EditorModel
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(editor);

        options ??= EditorCaptureOptions.Default;

        // As the controllers do: an editor with nothing outside the skipped zones is not found.
        if (shape is null || !shape.HasItemsInAnyZone(options.IgnoredZones))
        {
            return null;
        }

        var metadataProvider = services.GetRequiredService<IModelMetadataProvider>();

        foreach (var zone in shape.Properties)
        {
            if (options.IgnoredZones.Any(ignored => string.Equals(zone.Key, ignored, StringComparison.OrdinalIgnoreCase)) || zone.Value is not IShape zoneShape)
            {
                continue;
            }

            foreach (var item in zoneShape.Items ?? [])
            {
                if (item is IShape child)
                {
                    Visit(metadataProvider, editor, child, options);
                }
            }
        }

        return editor;
    }

    /// <summary>
    /// Checks the changes the model sent against the fields of the editor and puts them in the editor's spelling.
    /// </summary>
    /// <param name="editor">The editor built from the current values.</param>
    /// <param name="changes">The changes, keyed by section and then by field.</param>
    /// <param name="normalized">The changes with the section and field names of the editor.</param>
    /// <param name="readTool">The tool that shows the fields, named in the errors, such as <c>getSiteSettings</c>.</param>
    /// <returns>The problems found, empty when the changes can be posted.</returns>
    public static List<string> ValidateChanges(EditorModel editor, JsonObject changes, out Dictionary<string, JsonObject> normalized, string readTool)
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
                errors.Add($"There is no section '{sectionChange.Key}' in this editor. The sections are: {string.Join(", ", editor.Sections.Select(s => s.Name))}.");

                continue;
            }

            if (sectionChange.Value is not JsonObject fields)
            {
                errors.Add($"The changes of the section '{section.Name}' must be an object of field names and values.");

                continue;
            }

            var target = new JsonObject();

            ValidateFields(section.Name, section.Fields, fields, target, errors, readTool);

            normalized[section.Name] = target;
        }

        return errors;
    }

    /// <summary>
    /// Builds the form values a post of the page would send: every field of every section the editor was built with,
    /// with the changes merged over the current values. Every driver binds its whole view model on save, so a field
    /// left out would be saved as its default.
    /// </summary>
    /// <param name="editor">The editor built from the current values.</param>
    /// <param name="changes">The validated changes, keyed by section.</param>
    /// <returns>The form values.</returns>
    public static Dictionary<string, StringValues> BuildFormValues(EditorModel editor, IReadOnlyDictionary<string, JsonObject> changes)
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
    /// Describes the values of a section for the model: a secret is replaced by whether it is set, the reference to a
    /// secret keeps only its source and name, and a fixed field is left out.
    /// </summary>
    /// <param name="section">The section.</param>
    /// <returns>The values without any secret.</returns>
    public static JsonObject DescribeValues(EditorSection section)
    {
        ArgumentNullException.ThrowIfNull(section);

        return DescribeObject(section.Fields, section.Values);
    }

    /// <summary>
    /// Builds the JSON schema of the editable fields of a section, from the view model metadata.
    /// </summary>
    /// <param name="section">The section.</param>
    /// <returns>The schema.</returns>
    public static JsonObject BuildSchema(EditorSection section)
    {
        ArgumentNullException.ThrowIfNull(section);

        return ObjectSchema(section.Fields);
    }

    /// <summary>
    /// Lists the fields whose described value differs between two editors of the same page.
    /// </summary>
    /// <param name="before">The editor built before the save.</param>
    /// <param name="after">The editor built from the saved values.</param>
    /// <returns>The changed fields, as <c>section.field</c>.</returns>
    public static List<string> GetChangedFields(EditorModel before, EditorModel after)
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

    private static void Visit(IModelMetadataProvider metadataProvider, EditorModel editor, IShape shape, EditorCaptureOptions options)
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
                        Visit(metadataProvider, editor, child, options);
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
            section = new EditorSection
            {
                Name = prefix,
            };

            editor.Sections.Add(section);
        }

        // Some drivers build several shapes of one view model with one prefix (the general settings group builds three).
        if (section.ModelTypes.Add(modelType))
        {
            var metadata = metadataProvider.GetMetadataForType(modelType);

            AddFields(section.Name, metadata, shape, section.Fields, section.Values, editor.Unsupported, options, 0);
        }
        else
        {
            // A driver that puts one view model in several cards may fill only each card's own fields, so a field the
            // earlier cards left at the view model's default takes its value from the card that fills it.
            var metadata = metadataProvider.GetMetadataForType(modelType);
            var cardValues = new JsonObject();
            var defaults = new JsonObject();

            AddFields(section.Name, metadata, shape, [], cardValues, [], options, 0);
            AddFields(section.Name, metadata, CreateDefault(modelType), [], defaults, [], options, 0);
            FillFromCard(section.Values, cardValues, defaults);
        }

        foreach (var item in shape.Items ?? [])
        {
            if (item is IShape child)
            {
                Visit(metadataProvider, editor, child, options);
            }
        }
    }

    private static void FillFromCard(JsonObject target, JsonObject card, JsonObject defaults)
    {
        foreach (var (name, value) in card)
        {
            var fallback = defaults?[name];

            // The card left the field at its default: another card fills it, or nobody does.
            if (JsonNode.DeepEquals(value, fallback))
            {
                continue;
            }

            if (!target.TryGetPropertyValue(name, out var existing) || JsonNode.DeepEquals(existing, fallback))
            {
                target[name] = value?.DeepClone();
            }
            else if (existing is JsonObject existingObject && value is JsonObject cardObject)
            {
                FillFromCard(existingObject, cardObject, fallback as JsonObject);
            }
        }
    }

    private static object CreateDefault(Type type)
        => type.IsAbstract || type.GetConstructor(Type.EmptyTypes) is null
            ? null
            : Activator.CreateInstance(type);

    private static Type GetViewModelType(IShape shape)
    {
        var type = shape.GetType();

        // A strongly typed shape derives from the view model type: a proxy type generated at run time, or a sealed
        // type Orchard Core's shape source generator writes at build time, which adds IShape to the view model.
        while (type.BaseType is not null && (type.Assembly.IsDynamic || IsCompiledShapeType(type)))
        {
            type = type.BaseType;
        }

        return typeof(IShape).IsAssignableFrom(type)
            ? null
            : type;
    }

    private static bool IsCompiledShapeType(Type type)
        => type.IsSealed &&
            typeof(IShape).IsAssignableFrom(type) &&
            type.BaseType is { } baseType &&
            baseType != typeof(object) &&
            !typeof(IShape).IsAssignableFrom(baseType) &&
            baseType.Assembly != typeof(IShape).Assembly;

    private static void AddFields(string path, ModelMetadata metadata, object model, List<EditorField> fields, JsonObject values, List<string> unsupported, EditorCaptureOptions options, int depth)
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
                var kind = EditorFieldKind.Value;

                if (depth == 0 && options.FixedFields.Contains(property.PropertyName))
                {
                    kind = EditorFieldKind.Fixed;
                }
                else if (property.UnderlyingOrModelType == typeof(string) && IsSecretName(property.PropertyName, options))
                {
                    kind = EditorFieldKind.Secret;
                }

                fields.Add(new EditorField(property.PropertyName, kind, property)
                {
                    // A text the page fills with a list as JSON text, as a script on the page writes it.
                    JsonText = kind == EditorFieldKind.Value && value is string text && TryReadJsonTextList(text, out _),
                });
                values[property.PropertyName] = ToNode(value, property.ModelType);

                continue;
            }

            if (property.ModelType == typeof(SecretInputViewModel))
            {
                var secret = value as SecretInputViewModel;

                fields.Add(new EditorField(property.PropertyName, EditorFieldKind.SecretReference, property));
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
                    fields.Add(new EditorField(property.PropertyName, EditorFieldKind.List, property));
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

            var children = new List<EditorField>();
            var childValues = new JsonObject();

            AddFields(fieldPath, property, value, children, childValues, unsupported, options, depth + 1);

            fields.Add(new EditorField(property.PropertyName, EditorFieldKind.Object, property)
            {
                Children = children,
            });
            values[property.PropertyName] = value is null ? null : childValues;
        }
    }

    private static bool IsSecretName(string name, EditorCaptureOptions options)
    {
        if (options.SecretFields.Contains(name))
        {
            return true;
        }

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

    private static void ValidateFields(string path, List<EditorField> fields, JsonObject changes, JsonObject target, List<string> errors, string readTool)
    {
        foreach (var change in changes)
        {
            var field = fields.FirstOrDefault(f => string.Equals(f.Name, change.Key, StringComparison.OrdinalIgnoreCase));
            var fieldPath = $"{path}.{change.Key}";

            if (field is null)
            {
                errors.Add($"'{fieldPath}' is not a field of this editor. Use {readTool} to see the fields.");

                continue;
            }

            switch (field.Kind)
            {
                case EditorFieldKind.Fixed:
                    errors.Add($"'{path}.{field.Name}' cannot be changed here.");
                    break;

                case EditorFieldKind.Secret:
                    errors.Add($"'{path}.{field.Name}' is a secret; it is never accepted in the chat. A person enters it on the admin page, or the field can name a secret of the Secrets module where the page offers that.");
                    break;

                case EditorFieldKind.SecretReference:
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
                            errors.Add($"'{path}.{field.Name}.{part.Key}' cannot be set here: a secret value is never accepted in the chat. Set Source to Secret and SecretName to the name of a secret, or let a person enter the value on the admin page.");
                        }
                    }

                    target[field.Name] = referenceTarget;
                    break;

                case EditorFieldKind.List:
                    if (change.Value is not null and not JsonArray)
                    {
                        errors.Add($"'{path}.{field.Name}' takes a list.");
                        break;
                    }

                    target[field.Name] = change.Value?.DeepClone();
                    break;

                case EditorFieldKind.Object:
                    if (change.Value is not JsonObject nested)
                    {
                        errors.Add($"'{path}.{field.Name}' takes an object.");
                        break;
                    }

                    var nestedTarget = new JsonObject();

                    ValidateFields($"{path}.{field.Name}", field.Children, nested, nestedTarget, errors, readTool);

                    target[field.Name] = nestedTarget;
                    break;

                default:
                    if (field.JsonText && change.Value is JsonArray list)
                    {
                        // The page posts this list as JSON text, as its script writes it.
                        target[field.Name] = list.ToJsonString();
                        break;
                    }

                    if (change.Value is JsonObject or JsonArray)
                    {
                        errors.Add($"'{path}.{field.Name}' takes a single value.");
                        break;
                    }

                    if (field.JsonText &&
                        change.Value is JsonValue jsonText &&
                        (!jsonText.TryGetValue<string>(out var given) || !TryReadJsonTextList(given, out _)))
                    {
                        errors.Add($"'{path}.{field.Name}' takes a list, such as [\"en-US\",\"fr-FR\"], or that list written as JSON text.");
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

    private static JsonObject DescribeObject(List<EditorField> fields, JsonObject values)
    {
        var result = new JsonObject();

        foreach (var field in fields)
        {
            if (field.Kind == EditorFieldKind.Fixed)
            {
                continue;
            }

            values.TryGetPropertyValue(field.Name, out var value);

            result[field.Name] = field.Kind switch
            {
                EditorFieldKind.Secret => new JsonObject
                {
                    ["hasValue"] = value is JsonValue secret && secret.TryGetValue<string>(out var text) && !string.IsNullOrEmpty(text),
                },
                EditorFieldKind.SecretReference => value is JsonObject reference
                    ? new JsonObject
                    {
                        [nameof(SecretInputViewModel.Source)] = reference[nameof(SecretInputViewModel.Source)]?.DeepClone(),
                        [nameof(SecretInputViewModel.SecretName)] = reference[nameof(SecretInputViewModel.SecretName)]?.DeepClone(),
                        [nameof(SecretInputViewModel.HasValue)] = reference[nameof(SecretInputViewModel.HasValue)]?.DeepClone(),
                    }
                    : null,
                EditorFieldKind.Object => value is JsonObject nested
                    ? DescribeObject(field.Children, nested)
                    : null,
                _ when field.JsonText && value is JsonValue jsonText && jsonText.TryGetValue<string>(out var text) && TryReadJsonTextList(text, out var list) => list,
                _ => value?.DeepClone(),
            };
        }

        return result;
    }

    private static JsonObject ObjectSchema(List<EditorField> fields)
    {
        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var field in fields)
        {
            if (field.Kind == EditorFieldKind.Fixed)
            {
                continue;
            }

            properties[field.Name] = FieldSchema(field);

            if (field.Kind is EditorFieldKind.Value or EditorFieldKind.List &&
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

    private static JsonObject FieldSchema(EditorField field)
    {
        JsonObject schema;

        switch (field.Kind)
        {
            case EditorFieldKind.Secret:
                schema = new JsonObject
                {
                    ["type"] = "object",
                    ["readOnly"] = true,
                    ["description"] = "A secret. Only whether it is set is shown, and a value is never accepted here; a person enters it on the admin page.",
                };

                return schema;

            case EditorFieldKind.SecretReference:
                schema = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        [nameof(SecretInputViewModel.Source)] = new JsonObject
                        {
                            ["type"] = "string",
                            ["enum"] = new JsonArray(nameof(SecretInputSource.Value), nameof(SecretInputSource.Secret)),
                            ["description"] = "Where the credential is kept: Value (entered on the admin page and kept protected) or Secret (a secret of the Secrets module, named in SecretName).",
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
                            ["description"] = "Whether a protected value is kept. It cannot be changed here.",
                        },
                    },
                    ["additionalProperties"] = false,
                    ["description"] = "A credential. Its value is never shown or accepted here; only where it is kept and the secret name can change.",
                };

                return schema;

            case EditorFieldKind.List:
                schema = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = ValueSchema(field.Metadata.ElementMetadata),
                };

                break;

            case EditorFieldKind.Object:
                schema = ObjectSchema(field.Children);

                break;

            default:
                schema = field.JsonText
                    ? new JsonObject
                    {
                        ["type"] = "array",
                        ["items"] = new JsonObject { ["type"] = "string" },
                    }
                    : ValueSchema(field.Metadata);

                break;
        }

        var description = field.JsonText
            ? JoinDescription("A list. The page stores it as JSON text, such as [\"en-US\",\"fr-FR\"]; give the list.", GetDescription(field.Metadata))
            : GetDescription(field.Metadata);

        if (!string.IsNullOrEmpty(description))
        {
            schema["description"] = description;
        }

        return schema;
    }

    private static string JoinDescription(string first, string second)
        => string.IsNullOrEmpty(second) ? first : $"{first} {second}";

    /// <summary>
    /// Marks a text field of a section as one that holds a list written as JSON text, for a page whose editor doesn't
    /// show its current value, so the value can't be recognized.
    /// </summary>
    /// <param name="section">The section.</param>
    /// <param name="fieldName">The field.</param>
    /// <returns><see langword="true"/> when the section has that text field.</returns>
    public static bool MarkJsonText(EditorSection section, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(section);

        var field = section.Fields.FirstOrDefault(f => f.Kind == EditorFieldKind.Value &&
            string.Equals(f.Name, fieldName, StringComparison.Ordinal) &&
            (f.Metadata?.UnderlyingOrModelType ?? typeof(string)) == typeof(string));

        if (field is null)
        {
            return false;
        }

        field.JsonText = true;

        return true;
    }

    /// <summary>
    /// Reads a list of values written as JSON text, such as <c>["en-US","fr-FR"]</c>.
    /// </summary>
    private static bool TryReadJsonTextList(string text, out JsonArray list)
    {
        list = null;

        if (string.IsNullOrWhiteSpace(text) || !text.TrimStart().StartsWith('['))
        {
            return false;
        }

        try
        {
            if (JsonNode.Parse(text) is JsonArray parsed && parsed.All(item => item is null or JsonValue))
            {
                list = parsed;

                return true;
            }
        }
        catch (JsonException)
        {
        }

        return false;
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
