namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// What was wrong with a field given to a checkout step.
/// </summary>
/// <param name="Field">The field, such as <c>Email</c> or <c>Address.PostalCode</c>, or empty for the step as a whole.</param>
/// <param name="Message">What was wrong, for the buyer.</param>
public sealed record CheckoutStepError(string Field, string Message);
