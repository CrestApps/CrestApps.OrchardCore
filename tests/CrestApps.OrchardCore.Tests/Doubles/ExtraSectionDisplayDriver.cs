using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// A section another module adds to an admin editor with a display driver of its own: the test registers it beside the
/// editor's built-in drivers and checks that its shape reaches the editor.
/// </summary>
/// <typeparam name="TModel">The edited record.</typeparam>
internal sealed class ExtraSectionDisplayDriver<TModel> : DisplayDriver<TModel>
    where TModel : class
{
    /// <summary>
    /// The shape type of the section.
    /// </summary>
    public const string ShapeType = "ExtraSection_Edit";

    /// <summary>
    /// What the section shows.
    /// </summary>
    public const string Note = "Added by another module";

    public override IDisplayResult Edit(TModel model, BuildEditorContext context)
        => Initialize<ExtraSectionViewModel>(ShapeType, section => section.Note = Note).Location("Content:5");
}
