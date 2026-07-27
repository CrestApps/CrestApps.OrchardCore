using CrestApps.Core;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Tools.Services;
using CrestApps.OrchardCore.AI.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.AI.Tools.Drivers;

/// <summary>
/// Base display driver that renders the configured AI tool instances for any extensible entity that can
/// expose tool instances to the AI model, such as AI profiles and AI profile templates.
/// </summary>
/// <typeparam name="TModel">The type of the entity the tool instances are attached to.</typeparam>
public abstract class AIToolInstancesDisplayDriverBase<TModel> : DisplayDriver<TModel>
    where TModel : ExtensibleEntity, new()
{
    private readonly IAIToolInstanceAccessor _instanceAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolInstancesDisplayDriverBase{TModel}"/> class.
    /// </summary>
    /// <param name="instanceAccessor">The accessor used to resolve the instances the current user may assign.</param>
    protected AIToolInstancesDisplayDriverBase(IAIToolInstanceAccessor instanceAccessor)
    {
        _instanceAccessor = instanceAccessor;
    }

    /// <summary>
    /// Gets the shape type used to render the tool instances editor. Override this to render a layout that
    /// matches the host model's editor.
    /// </summary>
    protected virtual string EditorShapeType => "EditToolInstances_Edit";

    /// <summary>
    /// Gets the shape location used to place the tool instances editor. Override this to place the editor
    /// on a host model that uses a different zone or tab layout.
    /// </summary>
    protected virtual string EditorLocation => "Content:7#Capabilities;9";

    /// <summary>
    /// Determines whether the tool instances editor applies to the given model.
    /// </summary>
    /// <param name="model">The model being edited.</param>
    /// <returns><c>true</c> when the editor should be rendered; otherwise, <c>false</c>.</returns>
    protected virtual bool CanHandle(TModel model) => true;

    /// <summary>
    /// Gets the tool instance names currently selected on the model. Override this to read the selection
    /// from a different storage location than <see cref="AIToolInstanceMetadata"/>.
    /// </summary>
    /// <param name="model">The model being edited.</param>
    /// <returns>The selected tool instance names.</returns>
    protected virtual string[] GetSelectedInstanceNames(TModel model)
    {
        return model.GetOrCreate<AIToolInstanceMetadata>().ToolInstanceNames ?? [];
    }

    /// <summary>
    /// Stores the selected tool instance names on the model. Override this to persist the selection to a
    /// different storage location than <see cref="AIToolInstanceMetadata"/>.
    /// </summary>
    /// <param name="model">The model being updated.</param>
    /// <param name="instanceNames">The selected tool instance names.</param>
    protected virtual void SetSelectedInstanceNames(TModel model, string[] instanceNames)
    {
        var metadata = model.GetOrCreate<AIToolInstanceMetadata>();

        metadata.ToolInstanceNames = instanceNames;

        model.Put(metadata);
    }

    public override async Task<IDisplayResult> EditAsync(TModel model, BuildEditorContext context)
    {
        if (!CanHandle(model))
        {
            return null;
        }

        var accessibleInstances = await _instanceAccessor.GetAccessibleInstancesAsync();

        if (accessibleInstances.Count == 0)
        {
            return null;
        }

        return Initialize<EditToolInstancesViewModel>(EditorShapeType, viewModel =>
        {
            var selectedNames = GetSelectedInstanceNames(model) ?? [];

            viewModel.Instances = accessibleInstances
                .Select(instance => new ToolInstanceEntry
                {
                    Name = instance.Name,
                    Description = instance.Description,
                    IsSelected = selectedNames.Contains(instance.Name, StringComparer.OrdinalIgnoreCase),
                })
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }).Location(EditorLocation);
    }

    public override async Task<IDisplayResult> UpdateAsync(TModel model, UpdateEditorContext context)
    {
        if (!CanHandle(model))
        {
            return null;
        }

        var accessibleInstances = await _instanceAccessor.GetAccessibleInstancesAsync();

        if (accessibleInstances.Count == 0)
        {
            return null;
        }

        var viewModel = new EditToolInstancesViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        var selectedNames = viewModel.Instances?
            .Where(entry => entry.IsSelected)
            .Select(entry => entry.Name);

        SetSelectedInstanceNames(model, selectedNames is null
            ? []
            : accessibleInstances
                .Select(instance => instance.Name)
                .Intersect(selectedNames, StringComparer.OrdinalIgnoreCase)
                .ToArray());

        return await EditAsync(model, context);
    }
}
