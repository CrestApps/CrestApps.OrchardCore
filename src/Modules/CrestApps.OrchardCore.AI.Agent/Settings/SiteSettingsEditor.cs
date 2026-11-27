using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Editors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Agent.Settings;

/// <summary>
/// Replays a site settings page the way the Settings admin controller does: it builds the editor of a settings group
/// through the site display drivers, reads the editor view models the drivers filled, and posts changed values back
/// through the same drivers, so validation, secret handling, side effects and the tenant reload stay theirs. The
/// editor replay itself is <see cref="EditorReplay"/>, which the generic record tools share.
/// </summary>
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

    // The tool that shows the fields of a settings group, named in the errors.
    private const string ReadTool = "getSiteSettings";

    // The wrapper a settings driver adds to the page when saving it reloads the tenant.
    private const string ReloadWarningWrapper = "Settings_Wrapper__Reload";

    // Settings pages whose driver fills its editor from other properties than the ones it binds on save (a script on
    // the page writes the bound ones), so the editor reads them as null. Their current values come from the saved
    // settings instead, so the tools show them and a save posts them back unchanged. The Cultures page shows a list of
    // cultures and posts SupportedCultures as JSON text and DefaultCulture from it.
    private static readonly UnshownField[] _unshownFields =
    [
        new("LocalizationSettings", "SupportedCultures", "LocalizationSettings", "SupportedCultures", JsonText: true),
        new("LocalizationSettings", "DefaultCulture", "LocalizationSettings", "DefaultCulture", JsonText: false),
    ];

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
        var editor = Capture(services, shape);

        if (editor is not null)
        {
            FillUnshownFields(site, editor);
        }

        if (editor is null || editor.Sections.Count == 0)
        {
            var logger = services.GetService<ILoggerFactory>()?.CreateLogger(typeof(SiteSettingsEditor));

            if (logger?.IsEnabled(LogLevel.Debug) == true)
            {
                logger.LogDebug(
                    "The settings editor of the group '{GroupId}' has no section the tools can read. Has HttpContext: {HasHttpContext}; signed in: {SignedIn}. Zones: {Zones}. Unsupported: {Unsupported}.",
                    groupId,
                    httpContext is not null,
                    httpContext?.User?.Identity?.IsAuthenticated == true,
                    DescribeZones(shape),
                    editor is null ? "(no editor)" : string.Join("; ", editor.Unsupported));
            }
        }

        return editor;
    }

    private static void FillUnshownFields(ISite site, SettingsEditor editor)
    {
        foreach (var entry in _unshownFields)
        {
            var section = editor.Sections.FirstOrDefault(s =>
                string.Equals(s.Name, entry.Section, StringComparison.Ordinal) ||
                s.Name.EndsWith($".{entry.Section}", StringComparison.Ordinal));

            if (section is null || !section.Fields.Any(field => field.Kind == EditorFieldKind.Value && field.Name == entry.Field))
            {
                continue;
            }

            if (entry.JsonText)
            {
                EditorReplay.MarkJsonText(section, entry.Field);
            }

            if (section.Values[entry.Field] is not null || site.Properties?[entry.SettingsKey]?[entry.Property] is not { } saved)
            {
                continue;
            }

            section.Values[entry.Field] = entry.JsonText
                ? JsonValue.Create(saved.ToJsonString())
                : saved.DeepClone();
        }
    }

    private static string DescribeZones(IShape shape)
    {
        if (shape is null)
        {
            return "(no shape)";
        }

        var zones = new List<string>();

        foreach (var zone in shape.Properties)
        {
            if (zone.Value is not IShape zoneShape)
            {
                continue;
            }

            var items = (zoneShape.Items ?? [])
                .OfType<IShape>()
                .Select(item => $"{item.Metadata.Type} [{item.GetType().Name}, prefix '{item.Metadata.Prefix}', {item.Items?.Count ?? 0} items]");

            zones.Add($"{zone.Key}: {string.Join(", ", items)}");
        }

        return zones.Count == 0 ? "(none)" : string.Join(" | ", zones);
    }

    /// <summary>
    /// Reads the editor view models of a built or updated settings editor.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="shape">The root shape the display manager returned.</param>
    /// <returns>The editor, or <see langword="null"/> when the shape has no editor outside the actions zone.</returns>
    public static SettingsEditor Capture(IServiceProvider services, IShape shape)
    {
        if (shape is null)
        {
            return null;
        }

        return EditorReplay.Capture(services, shape, new SettingsEditor
        {
            ReloadsTenant = shape.Metadata.Wrappers.Contains(ReloadWarningWrapper),
        });
    }

    /// <summary>
    /// Checks the changes the model sent against the fields of the editor and puts them in the editor's spelling.
    /// </summary>
    /// <param name="editor">The editor built from the current settings.</param>
    /// <param name="changes">The changes, keyed by section and then by field.</param>
    /// <param name="normalized">The changes with the section and field names of the editor.</param>
    /// <returns>The problems found, empty when the changes can be posted.</returns>
    public static List<string> ValidateChanges(SettingsEditor editor, JsonObject changes, out Dictionary<string, JsonObject> normalized)
        => EditorReplay.ValidateChanges(editor, changes, out normalized, ReadTool);

    /// <summary>
    /// Builds the form values a settings page post would send: every field of every section the editor was built
    /// with, with the changes merged over the current values.
    /// </summary>
    /// <param name="editor">The editor built from the current settings.</param>
    /// <param name="changes">The validated changes, keyed by section.</param>
    /// <returns>The form values.</returns>
    public static Dictionary<string, StringValues> BuildFormValues(SettingsEditor editor, IReadOnlyDictionary<string, JsonObject> changes)
        => EditorReplay.BuildFormValues(editor, changes);

    /// <summary>
    /// Describes the values of a section for the model, without any secret.
    /// </summary>
    /// <param name="section">The section.</param>
    /// <returns>The values without any secret.</returns>
    public static JsonObject DescribeValues(EditorSection section)
        => EditorReplay.DescribeValues(section);

    /// <summary>
    /// Builds the JSON schema of the editable fields of a section, from the view model metadata.
    /// </summary>
    /// <param name="section">The section.</param>
    /// <returns>The schema.</returns>
    public static JsonObject BuildSchema(EditorSection section)
        => EditorReplay.BuildSchema(section);

    /// <summary>
    /// Lists the fields whose described value differs between two editors of the same group.
    /// </summary>
    /// <param name="before">The editor built before the save.</param>
    /// <param name="after">The editor built from the saved settings.</param>
    /// <returns>The changed fields, as <c>section.field</c>.</returns>
    public static List<string> GetChangedFields(SettingsEditor before, SettingsEditor after)
        => EditorReplay.GetChangedFields(before, after);
}

/// <summary>
/// A field of a settings page that its editor doesn't fill, read from the saved site settings instead.
/// </summary>
/// <param name="Section">The section, the form prefix without <c>ISite.</c>.</param>
/// <param name="Field">The field of the view model.</param>
/// <param name="SettingsKey">The key of the settings object in the site's properties.</param>
/// <param name="Property">The property of the settings object.</param>
/// <param name="JsonText">Whether the page posts the field as a list written as JSON text.</param>
internal sealed record UnshownField(string Section, string Field, string SettingsKey, string Property, bool JsonText);

/// <summary>
/// The editor of one settings group, read from the view models its display drivers filled.
/// </summary>
internal sealed class SettingsEditor : EditorModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the page warns that saving it reloads the tenant.
    /// </summary>
    public bool ReloadsTenant { get; set; }
}
