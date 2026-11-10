using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Core;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// Saves the Contact step from code with the rules of its page, through <see cref="CustomerContactStepService"/>.
/// </summary>
public sealed class CustomerCheckoutContactStep : ICheckoutContactStep
{
    private readonly CustomerContactStepService _contactStep;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomerCheckoutContactStep"/> class.
    /// </summary>
    /// <param name="contactStep">The Contact step's service.</param>
    public CustomerCheckoutContactStep(CustomerContactStepService contactStep)
    {
        _contactStep = contactStep;
    }

    /// <inheritdoc />
    public string StepKey
        => CustomersConstants.ContactStepKey;

    /// <inheritdoc />
    public async Task<CheckoutStepSaveResult> SaveAsync(CheckoutFlow flow, CheckoutContactDetails details, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(details);

        var modelState = new ModelStateDictionary();
        var saved = await _contactStep.SaveAsync(flow, new CustomerContactInput
        {
            Name = details.Name,
            Email = details.Email,
            Phone = details.Phone,
            BillingAddress = AddressEditorViewModel.From(details.BillingAddress),
        }, modelState, prefix: null);

        return saved
            ? CheckoutStepSaveResult.Success()
            : CheckoutStepSaveResults.NotSaved(modelState);
    }
}
