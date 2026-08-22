using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Binds subscription admin list search text to a parsed subscription query filter result.
/// </summary>
public sealed class SubscriptionFilterEngineModelBinder : IModelBinder
{
    private readonly ISubscriptionAdminListFilterParser _parser;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionFilterEngineModelBinder"/> class.
    /// </summary>
    /// <param name="parser">The parser used to convert search text into a filter result.</param>
    public SubscriptionFilterEngineModelBinder(ISubscriptionAdminListFilterParser parser)
    {
        _parser = parser;
    }

    /// <summary>
    /// Binds the model by reading the request value and parsing it as subscription filter text.
    /// </summary>
    /// <param name="bindingContext">The model binding context for the current request value.</param>
    /// <returns>A completed task after the model binding result is set.</returns>
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var modelName = bindingContext.ModelName;

        // Try to fetch the value of the argument by name q=
        var valueProviderResult = bindingContext.ValueProvider.GetValue(modelName);

        if (valueProviderResult == ValueProviderResult.None)
        {
            bindingContext.Result = ModelBindingResult.Success(_parser.Parse(string.Empty));

            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(modelName, valueProviderResult);

        // When value is null or empty the parser returns an empty result.
        bindingContext.Result = ModelBindingResult.Success(_parser.Parse(valueProviderResult.FirstValue));

        return Task.CompletedTask;
    }
}
