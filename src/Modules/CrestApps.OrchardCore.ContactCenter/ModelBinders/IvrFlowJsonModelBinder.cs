using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Deployments;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.ModelBinders;

/// <summary>
/// Binds the IVR menu tree from the JSON an operator typed into the entry point editor. Malformed JSON is a
/// format error of the editor, reported here against the field; whether the parsed flow is runnable is a rule
/// of the entry point itself and is checked by its handler, so a recipe import and the editor reject the same
/// flows.
/// </summary>
public sealed class IvrFlowJsonModelBinder : IModelBinder
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="IvrFlowJsonModelBinder"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public IvrFlowJsonModelBinder(IStringLocalizer<IvrFlowJsonModelBinder> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);

        if (value == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        // The raw text is kept on the model state so a rejected edit is shown back as typed rather than as
        // whatever the entity last held.
        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, value);

        var json = value.FirstValue;

        if (string.IsNullOrWhiteSpace(json))
        {
            bindingContext.Result = ModelBindingResult.Success(null);

            return Task.CompletedTask;
        }

        try
        {
            var flow = JsonSerializer.Deserialize<IvrFlow>(json, ContactCenterDeploymentSerializer.Options);
            bindingContext.Result = ModelBindingResult.Success(flow);
        }
        catch (JsonException exception)
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, S["The IVR menu is not valid JSON: {0}", exception.Message]);
            bindingContext.Result = ModelBindingResult.Failed();
        }

        return Task.CompletedTask;
    }
}
