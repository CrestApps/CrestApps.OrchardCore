using CrestApps.OrchardCore.Bookings.Recipes;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Bookings.Deployments;

/// <summary>
/// Exports the booking products' upcoming open dates, with their capacity and price, as a <c>BookingDates</c> recipe step. Past and closed dates, and the places held or sold, stay on the site.
/// </summary>
public sealed class BookingDatesDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<BookingDatesDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<BookingDatesDeploymentStep>("Booking dates");

    /// <inheritdoc/>
    public override string Name => BookingDatesStep.RecipeStepName;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
