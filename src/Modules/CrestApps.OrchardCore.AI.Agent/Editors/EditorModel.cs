using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Agent.Editors;

/// <summary>
/// What <see cref="EditorReplay.Capture{TEditor}"/> skips and which fields it treats specially.
/// </summary>
internal sealed class EditorCaptureOptions
{
    /// <summary>
    /// The options of an editor with no special field and only the actions zone skipped.
    /// </summary>
    public static readonly EditorCaptureOptions Default = new();

    /// <summary>
    /// Gets or sets the zones that hold no editor, such as the zone of the save button. The controllers ignore them
    /// when they decide whether an editor has anything to show.
    /// </summary>
    public string[] IgnoredZones { get; set; } = ["Actions"];

    /// <summary>
    /// Gets or sets the names of view model properties that hold a secret, besides those whose name says so.
    /// </summary>
    public IReadOnlySet<string> SecretFields { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the names of top-level view model properties the page posts back unchanged and the model neither
    /// sees nor sets, such as <c>IsNew</c>.
    /// </summary>
    public IReadOnlySet<string> FixedFields { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// An admin editor, read from the view models its display drivers filled.
/// </summary>
internal class EditorModel
{
    /// <summary>
    /// Gets the sections of the editor, one per form prefix.
    /// </summary>
    public List<EditorSection> Sections { get; } = [];

    /// <summary>
    /// Gets the reasons the editor cannot be posted back from the chat, empty when it can.
    /// </summary>
    public List<string> Unsupported { get; } = [];
}

/// <summary>
/// The fields of one form prefix of an editor and the values the drivers filled them with.
/// </summary>
internal sealed class EditorSection
{
    /// <summary>
    /// Gets or sets the form prefix, such as <c>ISite.AdminSettings</c> or <c>TaxCategory</c>.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets the editable fields.
    /// </summary>
    public List<EditorField> Fields { get; } = [];

    /// <summary>
    /// Gets the values of the fields, secrets included. They never leave the tool; see <see cref="EditorReplay.DescribeValues"/>.
    /// </summary>
    public JsonObject Values { get; } = new();

    /// <summary>
    /// Gets the view model types read for the prefix.
    /// </summary>
    public HashSet<Type> ModelTypes { get; } = [];
}

/// <summary>
/// One editable field of an editor section.
/// </summary>
internal sealed class EditorField
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EditorField"/> class.
    /// </summary>
    /// <param name="name">The property name of the view model.</param>
    /// <param name="kind">How the field is read and written.</param>
    /// <param name="metadata">The model metadata of the property.</param>
    public EditorField(string name, EditorFieldKind kind, ModelMetadata metadata)
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
    public EditorFieldKind Kind { get; }

    /// <summary>
    /// Gets the model metadata of the property.
    /// </summary>
    public ModelMetadata Metadata { get; }

    /// <summary>
    /// Gets or sets the fields of a nested object.
    /// </summary>
    public List<EditorField> Children { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether a text field holds a list written as JSON text, such as
    /// <c>["en-US","fr-FR"]</c>, as a script on the page posts it. A list given for it is posted as that text.
    /// </summary>
    public bool JsonText { get; set; }
}

/// <summary>
/// How an editor field is read and written.
/// </summary>
internal enum EditorFieldKind
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

    /// <summary>
    /// A field the page posts back as it was built, such as whether the record is new: never shown or changed.
    /// </summary>
    Fixed,
}
