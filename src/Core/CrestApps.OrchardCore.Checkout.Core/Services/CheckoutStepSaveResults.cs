using CrestApps.OrchardCore.Checkout.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// Turns what a checkout step's page service found wrong into a <see cref="CheckoutStepSaveResult"/>.
/// </summary>
public static class CheckoutStepSaveResults
{
    /// <summary>
    /// Returns a result that says the step wasn't saved, with the errors the model state holds.
    /// </summary>
    /// <param name="modelState">The model state the step's service wrote its errors to.</param>
    /// <param name="choosing">Whether the step needs the buyer to pick one of several options first.</param>
    public static CheckoutStepSaveResult NotSaved(ModelStateDictionary modelState, bool choosing = false)
    {
        ArgumentNullException.ThrowIfNull(modelState);

        var errors = modelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .SelectMany(entry => entry.Value.Errors.Select(error => new CheckoutStepError(entry.Key ?? string.Empty, error.ErrorMessage)))
            .ToList();

        return new CheckoutStepSaveResult
        {
            Choosing = choosing,
            Errors = errors,
        };
    }
}
