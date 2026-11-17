namespace CrestApps.OrchardCore.Banking.Models;

/// <summary>
/// A routing or account number that was refused, and why.
/// </summary>
/// <param name="Field">The field: <c>RoutingNumber</c> or <c>AccountNumber</c>.</param>
/// <param name="Message">Why, for the person who entered it.</param>
public sealed record BankAccountNumberError(string Field, string Message);
