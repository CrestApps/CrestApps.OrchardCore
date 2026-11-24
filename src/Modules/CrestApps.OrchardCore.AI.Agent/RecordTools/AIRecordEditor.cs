using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Agent.Editors;
using CrestApps.OrchardCore.AI.Records;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.AI.Agent.RecordTools;

/// <summary>
/// Replays the admin editor of a record kind through its display drivers, as the record's admin controller does on a
/// form post, with <see cref="EditorReplay"/>.
/// </summary>
internal static class AIRecordEditor
{
    // The view model property the editors of catalog records use to tell a new record from an existing one. It is
    // posted back as built.
    private const string IsNewField = "IsNew";

    /// <summary>
    /// Builds the editor of a record as its admin page does and reads the view models the drivers filled.
    /// </summary>
    /// <typeparam name="TModel">The record type.</typeparam>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="httpContext">The request of the caller.</param>
    /// <param name="kind">The record kind.</param>
    /// <param name="displayManager">The record's display manager.</param>
    /// <param name="record">The record; a copy of it is used, so the record is not changed.</param>
    /// <param name="isNew">Whether the record is new.</param>
    /// <returns>The editor, or <see langword="null"/> when no driver adds an editor.</returns>
    public static async Task<EditorModel> BuildAsync<TModel>(
        IServiceProvider services,
        HttpContext httpContext,
        AIRecordKind<TModel> kind,
        IDisplayManager<TModel> displayManager,
        TModel record,
        bool isNew)
        where TModel : CatalogItem, new()
    {
        var shape = await displayManager.BuildEditorAsync(kind.Copy(record), new FormValuesModelUpdater(services, httpContext, null), isNew, kind.EditorGroupId ?? string.Empty, string.Empty);

        return EditorReplay.Capture(services, shape, new EditorModel(), GetCaptureOptions(kind));
    }

    /// <summary>
    /// Posts form values to the editor of a record through its display drivers, as a form post of the admin page does,
    /// then checks the record's own rules, which the admin pages check after binding, and reads the editor back.
    /// </summary>
    /// <typeparam name="TModel">The record type.</typeparam>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="httpContext">The request of the caller.</param>
    /// <param name="kind">The record kind.</param>
    /// <param name="displayManager">The record's display manager.</param>
    /// <param name="record">The record the drivers change.</param>
    /// <param name="isNew">Whether the record is new.</param>
    /// <param name="formValues">The form values, from <see cref="EditorReplay.BuildFormValues"/>.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The model state of the post and the editor as the drivers rebuilt it.</returns>
    public static async Task<AIRecordReplay> ReplayAsync<TModel>(
        IServiceProvider services,
        HttpContext httpContext,
        AIRecordKind<TModel> kind,
        IDisplayManager<TModel> displayManager,
        TModel record,
        bool isNew,
        Dictionary<string, StringValues> formValues,
        CancellationToken cancellationToken)
        where TModel : CatalogItem, new()
    {
        var updater = new FormValuesModelUpdater(services, httpContext, formValues);
        var shape = await displayManager.UpdateEditorAsync(record, updater, isNew, kind.EditorGroupId ?? string.Empty, string.Empty);

        // As the admin pages do after binding: the record's own rules, reported against the editor.
        var validation = await kind.ValidateAsync(services, record, cancellationToken);

        AddValidationErrors(updater.ModelState, validation, typeof(TModel).Name);

        return new AIRecordReplay(updater, EditorReplay.Capture(services, shape, new EditorModel(), GetCaptureOptions(kind)));
    }

    /// <summary>
    /// Lists the fields a post changed and the requested fields it left as they were, with field names as the
    /// <c>values</c> name them.
    /// </summary>
    /// <param name="before">The editor built before the post.</param>
    /// <param name="after">The editor the drivers rebuilt after the post.</param>
    /// <param name="requested">The validated values, keyed by section.</param>
    /// <returns>The changed and the unchanged fields.</returns>
    public static (List<string> Changed, List<string> Unchanged) CompareFields(EditorModel before, EditorModel after, IReadOnlyDictionary<string, JsonObject> requested)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(requested);

        var onlySection = before.Sections.Count == 1 ? before.Sections[0].Name : null;
        var changedKeys = EditorReplay.GetChangedFields(before, after);
        var unchanged = new List<string>();

        foreach (var section in requested)
        {
            foreach (var field in section.Value)
            {
                var key = $"{section.Key}.{field.Key}";

                if (!changedKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
                {
                    unchanged.Add(ToFieldName(key, onlySection));
                }
            }
        }

        return (changedKeys.Select(key => ToFieldName(key, onlySection)).ToList(), unchanged);
    }

    /// <summary>
    /// Builds the JSON schema of the <c>values</c> a save takes: the fields of the editor's only section, or the
    /// sections keyed by name when the editor has several.
    /// </summary>
    /// <param name="editor">The editor.</param>
    /// <returns>The schema.</returns>
    public static JsonObject BuildValuesSchema(EditorModel editor)
    {
        ArgumentNullException.ThrowIfNull(editor);

        if (editor.Sections.Count == 1)
        {
            return EditorReplay.BuildSchema(editor.Sections[0]);
        }

        var properties = new JsonObject();

        foreach (var section in editor.Sections)
        {
            properties[section.Name] = EditorReplay.BuildSchema(section);
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["additionalProperties"] = false,
        };
    }

    /// <summary>
    /// Puts the values in the shape <see cref="EditorReplay.ValidateChanges"/> takes: keyed by section and then by
    /// field. Values keyed by field name alone are accepted for an editor with one section.
    /// </summary>
    /// <param name="editor">The editor.</param>
    /// <param name="values">The values the model sent.</param>
    /// <returns>The values keyed by section.</returns>
    public static JsonObject NormalizeValues(EditorModel editor, JsonObject values)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(values);

        if (editor.Sections.Count == 1)
        {
            var section = editor.Sections[0];

            if (!values.Any(value => string.Equals(value.Key, section.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return new JsonObject
                {
                    [section.Name] = values.DeepClone(),
                };
            }
        }

        return values;
    }

    /// <summary>
    /// Reports the rules the record's handlers failed against the editor, as the admin pages do after binding.
    /// </summary>
    /// <param name="modelState">The model state of the editor post.</param>
    /// <param name="validation">The result of the record's validation.</param>
    /// <param name="prefix">The form prefix of the record's editor.</param>
    public static void AddValidationErrors(ModelStateDictionary modelState, ValidationResultDetails validation, string prefix)
    {
        ArgumentNullException.ThrowIfNull(modelState);

        if (validation is null || validation.Succeeded)
        {
            return;
        }

        foreach (var error in validation.Errors)
        {
            var members = error.MemberNames?.Where(member => !string.IsNullOrEmpty(member)).ToArray();

            if (members is null || members.Length == 0)
            {
                modelState.AddModelError(prefix, error.ErrorMessage ?? "The record is not valid.");

                continue;
            }

            foreach (var member in members)
            {
                modelState.AddModelError($"{prefix}.{member}", error.ErrorMessage ?? "The value is not valid.");
            }
        }
    }

    /// <summary>
    /// Lists the errors of an editor post for the model, with field names as the <c>values</c> name them.
    /// </summary>
    /// <param name="modelState">The model state of the editor post.</param>
    /// <param name="editor">The editor.</param>
    /// <returns>The errors.</returns>
    public static List<AIRecordFieldError> GetErrors(ModelStateDictionary modelState, EditorModel editor)
    {
        ArgumentNullException.ThrowIfNull(modelState);
        ArgumentNullException.ThrowIfNull(editor);

        var onlySection = editor.Sections.Count == 1 ? editor.Sections[0].Name : null;

        return modelState
            .Where(entry => entry.Value.Errors.Count > 0)
            .SelectMany(entry => entry.Value.Errors.Select(error => new AIRecordFieldError(
                ToFieldName(entry.Key, onlySection),
                string.IsNullOrEmpty(error.ErrorMessage) ? "The value is not valid." : error.ErrorMessage)))
            .ToList();
    }

    private static EditorCaptureOptions GetCaptureOptions(AIRecordKind kind)
        => new()
        {
            SecretFields = new HashSet<string>(kind.SecretFields, StringComparer.OrdinalIgnoreCase),
            FixedFields = new HashSet<string>([IsNewField], StringComparer.OrdinalIgnoreCase),
        };

    private static string ToFieldName(string key, string onlySection)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        if (onlySection is not null && key.StartsWith(onlySection + ".", StringComparison.OrdinalIgnoreCase))
        {
            return key[(onlySection.Length + 1)..];
        }

        return string.Equals(key, onlySection, StringComparison.OrdinalIgnoreCase)
            ? null
            : key;
    }
}

/// <summary>
/// An error of a record save, for the model.
/// </summary>
/// <param name="Field">The field, as the <c>values</c> of saveRecord name it, or <see langword="null"/> for the record.</param>
/// <param name="Message">The error, as the editor or the record's rules wrote it.</param>
internal sealed record AIRecordFieldError(string Field, string Message);

/// <summary>
/// The outcome of replaying a post on a record's editor.
/// </summary>
/// <param name="Updater">The updater of the post, with the model state the drivers and the record's rules filled.</param>
/// <param name="Editor">The editor as the drivers rebuilt it after the post, or <see langword="null"/> when it shows nothing.</param>
internal sealed record AIRecordReplay(FormValuesModelUpdater Updater, EditorModel Editor);
