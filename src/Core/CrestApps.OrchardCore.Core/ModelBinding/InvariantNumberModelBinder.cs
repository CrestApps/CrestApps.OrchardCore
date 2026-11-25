using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Core.ModelBinding;

/// <summary>
/// Binds a posted <see cref="decimal"/>, <see cref="double"/> or <see cref="float"/> by reading it with the invariant
/// culture first and the request culture second, never with a group separator.
/// </summary>
/// <remarks>
/// A browser submits an <c>&lt;input type="number"&gt;</c> value with a dot ("12.50") whatever language the page is shown
/// in, and script writes numbers the same way, but the form value provider hands the value to the binder tagged with the
/// request culture. Read in de-DE the dot is a group separator, so "12.50" bound as 1250; in fr-FR it did not bind at all.
/// The invariant reading comes first. A text box may hold the culture's decimal comma ("12,50"), so the request culture is
/// tried second. Neither reading takes a group separator, so no spelling of a number is read as a hundred times it; a
/// value that fails both is a model error, as any value that is not a number is. Values the query string, the route and
/// an <c>__Invariant</c> form field supply are already tagged with the invariant culture, and empty values, are left to
/// the framework's binder.
/// </remarks>
/// <param name="modelType">The non-nullable numeric type to bind.</param>
/// <param name="frameworkBinder">The framework's binder for the type.</param>
internal sealed class InvariantNumberModelBinder(Type modelType, IModelBinder frameworkBinder) : IModelBinder
{
    private const NumberStyles Styles =
        NumberStyles.AllowLeadingWhite |
        NumberStyles.AllowTrailingWhite |
        NumberStyles.AllowLeadingSign |
        NumberStyles.AllowDecimalPoint |
        NumberStyles.AllowExponent;

    private readonly Type _modelType = modelType;
    private readonly IModelBinder _frameworkBinder = frameworkBinder;

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);

        if (valueProviderResult == ValueProviderResult.None ||
            CultureInfo.InvariantCulture.Equals(valueProviderResult.Culture) ||
            string.IsNullOrWhiteSpace(valueProviderResult.FirstValue))
        {
            return _frameworkBinder.BindModelAsync(bindingContext);
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);

        var value = valueProviderResult.FirstValue;

        if (TryParse(value, CultureInfo.InvariantCulture, out var model) ||
            TryParse(value, valueProviderResult.Culture, out model))
        {
            bindingContext.Result = ModelBindingResult.Success(model);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, new FormatException(), bindingContext.ModelMetadata);
        }

        return Task.CompletedTask;
    }

    private bool TryParse(string value, CultureInfo culture, out object model)
    {
        model = null;

        if (_modelType == typeof(decimal))
        {
            if (decimal.TryParse(value, Styles, culture, out var decimalValue))
            {
                model = decimalValue;
            }
        }
        else if (_modelType == typeof(double))
        {
            if (double.TryParse(value, Styles, culture, out var doubleValue) && double.IsFinite(doubleValue))
            {
                model = doubleValue;
            }
        }
        else if (float.TryParse(value, Styles, culture, out var floatValue) && float.IsFinite(floatValue))
        {
            model = floatValue;
        }

        return model is not null;
    }
}
