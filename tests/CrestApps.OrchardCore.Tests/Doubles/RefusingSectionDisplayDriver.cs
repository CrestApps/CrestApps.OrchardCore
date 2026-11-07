using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// A section another module adds to an admin editor, which refuses every save: its error is reported under its own
/// prefix, and the editor must not be saved.
/// </summary>
/// <typeparam name="TModel">The edited record.</typeparam>
internal sealed class RefusingSectionDisplayDriver<TModel> : DisplayDriver<TModel>
    where TModel : class
{
    /// <summary>
    /// The error the section reports.
    /// </summary>
    public const string Error = "The other module's section refused it.";

    public override IDisplayResult Edit(TModel model, BuildEditorContext context)
        => Initialize<ExtraSectionViewModel>(ExtraSectionDisplayDriver<TModel>.ShapeType, section => section.Note = Error).Location("Content:5");

    public override Task<IDisplayResult> UpdateAsync(TModel model, UpdateEditorContext context)
    {
        context.Updater.ModelState.AddModelError($"{Prefix}.{nameof(ExtraSectionViewModel.Note)}", Error);

        return EditAsync(model, context);
    }
}
