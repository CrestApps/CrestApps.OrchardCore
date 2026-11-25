using CrestApps.OrchardCore.Emails;
using Microsoft.Extensions.Localization;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Core.Emails;

/// <summary>
/// The editor of a module's templated e-mails: one template per kind of the module's group (on or off, a subject and a
/// body) and a layout around them all. A module's settings driver builds the <see cref="TemplatedEmailTemplatesEditorViewModel"/>
/// with <see cref="Build"/>, renders the shared <see cref="LayoutShapeType"/> shape (the layout card) and one
/// <see cref="TemplateShapeType"/> shape (Resources) for each of the entries (a card each), and reads it back
/// with <see cref="Apply"/>, which checks every part with the restricted Liquid the e-mails render with.
/// </summary>
public static class TemplatedEmailTemplatesEditor
{
    /// <summary>
    /// The name of the shared shape that edits the layout (with the introduction above it).
    /// </summary>
    public const string LayoutShapeType = "TemplatedEmailLayoutEditor";

    /// <summary>
    /// The name of the shared shape that edits one template, the entry at <see cref="TemplatedEmailTemplatesEditorViewModel.Index"/>.
    /// </summary>
    public const string TemplateShapeType = "TemplatedEmailTemplateEditor";

    /// <summary>
    /// Fills the editor from the group's kinds and the templates kept for them.
    /// </summary>
    /// <param name="model">The editor.</param>
    /// <param name="kinds">The group's kinds, in the order they are shown.</param>
    /// <param name="templates">The templates kept, by kind.</param>
    /// <param name="layout">The layout kept.</param>
    /// <param name="localizerFactory">The localizer factory, for the kinds' titles and values.</param>
    public static void Build(
        TemplatedEmailTemplatesEditorViewModel model,
        IEnumerable<TemplatedEmailKind> kinds,
        IReadOnlyDictionary<string, TemplatedEmailTemplate> templates,
        string layout,
        IStringLocalizerFactory localizerFactory)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(localizerFactory);

        model.Layout = layout;
        model.Templates = [.. (kinds ?? []).Select(kind =>
        {
            var entry = templates is not null && templates.TryGetValue(kind.Name, out var template)
                ? new TemplatedEmailTemplateEditorEntry { Kind = kind.Name, IsEnabled = template.IsEnabled, Subject = template.Subject, Body = template.Body }
                : new TemplatedEmailTemplateEditorEntry { Kind = kind.Name, Body = "{{ Content }}" };

            entry.Title = localizerFactory.Localize(kind.Title).Value;
            entry.Values = kind.Values is null ? null : localizerFactory.Localize(kind.Values).Value;

            return entry;
        })];
    }

    /// <summary>
    /// Reads the editor back: the templates of the group's kinds as written, the templates kept for kinds the group no
    /// longer registers (such as one whose feature is off) as they were, and the layout. A part that doesn't parse or
    /// uses a filter that isn't available is reported with its field name.
    /// </summary>
    /// <param name="model">The editor as posted.</param>
    /// <param name="kinds">The group's kinds.</param>
    /// <param name="kept">The templates kept before.</param>
    /// <returns>The templates and layout to keep, and the errors by field.</returns>
    public static TemplatedEmailTemplatesEdit Apply(
        TemplatedEmailTemplatesEditorViewModel model,
        IEnumerable<TemplatedEmailKind> kinds,
        IReadOnlyDictionary<string, TemplatedEmailTemplate> kept)
    {
        ArgumentNullException.ThrowIfNull(model);

        var names = (kinds ?? []).Select(kind => kind.Name).ToHashSet(StringComparer.Ordinal);
        var edit = new TemplatedEmailTemplatesEdit
        {
            Layout = string.IsNullOrWhiteSpace(model.Layout) ? null : model.Layout,
        };

        foreach (var (key, value) in kept ?? new Dictionary<string, TemplatedEmailTemplate>())
        {
            if (!names.Contains(key))
            {
                edit.Templates[key] = value;
            }
        }

        var entries = model.Templates ?? [];

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];

            Check(edit, $"{nameof(model.Templates)}[{index}].{nameof(entry.Subject)}", entry.Subject);
            Check(edit, $"{nameof(model.Templates)}[{index}].{nameof(entry.Body)}", entry.Body);

            if (!string.IsNullOrEmpty(entry.Kind) && names.Contains(entry.Kind))
            {
                edit.Templates[entry.Kind] = new TemplatedEmailTemplate
                {
                    IsEnabled = entry.IsEnabled,
                    Subject = string.IsNullOrWhiteSpace(entry.Subject) ? null : entry.Subject.Trim(),
                    Body = string.IsNullOrWhiteSpace(entry.Body) ? null : entry.Body,
                };
            }
        }

        Check(edit, nameof(model.Layout), model.Layout);

        return edit;
    }

    private static void Check(TemplatedEmailTemplatesEdit edit, string field, string liquid)
    {
        if (!string.IsNullOrWhiteSpace(liquid) && !LiquidTemplatedEmailTemplates.TryValidate(liquid, out var error))
        {
            edit.Errors.Add(new KeyValuePair<string, string>(field, error));
        }
    }
}

/// <summary>
/// What <see cref="TemplatedEmailTemplatesEditor.Apply"/> read back.
/// </summary>
public sealed class TemplatedEmailTemplatesEdit
{
    /// <summary>
    /// Gets the templates to keep, by kind.
    /// </summary>
    public Dictionary<string, TemplatedEmailTemplate> Templates { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the layout to keep.
    /// </summary>
    public string Layout { get; set; }

    /// <summary>
    /// Gets the parts that can't be saved: the field name and why.
    /// </summary>
    public List<KeyValuePair<string, string>> Errors { get; } = [];
}

/// <summary>
/// The editor of a module's templated e-mails.
/// </summary>
public class TemplatedEmailTemplatesEditorViewModel
{
    /// <summary>
    /// Gets or sets the text above the editor: what the templates read (<c>{{ Content }}</c> and the module's models).
    /// </summary>
    public string Introduction { get; set; }

    /// <summary>
    /// Gets or sets the entry of <see cref="Templates"/> that a template shape renders. The layout shape ignores it.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the layout around every e-mail of the group.
    /// </summary>
    public string Layout { get; set; }

    /// <summary>
    /// Gets or sets the template of each kind.
    /// </summary>
    public List<TemplatedEmailTemplateEditorEntry> Templates { get; set; } = [];
}

/// <summary>
/// The template of one kind in the editor.
/// </summary>
public class TemplatedEmailTemplateEditorEntry
{
    /// <summary>
    /// Gets or sets the kind.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the kind's title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the values the kind's templates read, besides the shared ones.
    /// </summary>
    public string Values { get; set; }

    /// <summary>
    /// Gets or sets whether the template is used.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the subject.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the body.
    /// </summary>
    public string Body { get; set; }
}
