namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// What saving a checkout step from code did.
/// </summary>
public sealed class CheckoutStepSaveResult
{
    /// <summary>
    /// Gets a value indicating whether the step was saved.
    /// </summary>
    public bool Saved { get; init; }

    /// <summary>
    /// Gets a value indicating whether the step needs the buyer to pick one of several options first.
    /// </summary>
    public bool Choosing { get; init; }

    /// <summary>
    /// Gets what was wrong with what was given, by field.
    /// </summary>
    public IReadOnlyList<CheckoutStepError> Errors { get; init; } = [];

    /// <summary>
    /// Returns a result that says the step was saved.
    /// </summary>
    public static CheckoutStepSaveResult Success()
        => new() { Saved = true };
}
