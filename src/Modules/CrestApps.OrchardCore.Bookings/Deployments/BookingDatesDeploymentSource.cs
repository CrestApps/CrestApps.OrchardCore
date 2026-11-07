using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Bookings.Recipes;
using CrestApps.OrchardCore.Bookings.Services;
using OrchardCore.Deployment;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Bookings.Deployments;

/// <summary>
/// Writes the booking products' upcoming dates that are open into a <c>BookingDates</c> recipe step, in the shape the
/// step reads: each date's product, its <c>Date</c>, its <c>Time</c> for a time slot, its <c>Capacity</c> and its
/// <c>Price</c>. Past and closed dates, and the places held or sold on a date, stay on the site.
/// </summary>
internal sealed class BookingDatesDeploymentSource : DeploymentSourceBase<BookingDatesDeploymentStep>
{
    private readonly IBookingSlotStore _slots;
    private readonly IClock _clock;

    public BookingDatesDeploymentSource(IBookingSlotStore slots, IClock clock)
    {
        _slots = slots;
        _clock = clock;
    }

    protected override async Task ProcessAsync(BookingDatesDeploymentStep step, DeploymentPlanResult result)
    {
        var now = _clock.UtcNow;
        var dates = new JsonArray();

        foreach (var slot in (await _slots.GetAllAsync())
            .Where(slot => !slot.Closed && slot.StartsUtc >= now && !string.IsNullOrEmpty(slot.SlotKey))
            .OrderBy(slot => slot.StartsUtc)
            .ThenBy(slot => slot.ContentItemId, StringComparer.Ordinal))
        {
            var separator = slot.SlotKey.IndexOf('T');
            var date = new JsonObject
            {
                ["ContentItemId"] = slot.ContentItemId,
                ["Date"] = separator < 0 ? slot.SlotKey : slot.SlotKey[..separator],
            };

            if (separator >= 0)
            {
                date["Time"] = slot.SlotKey[(separator + 1)..];
            }

            date["Capacity"] = slot.Capacity;

            if (slot.Price is { } price)
            {
                date["Price"] = price;
            }

            dates.Add(date);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = BookingDatesStep.RecipeStepName,
            ["Dates"] = dates,
        });
    }
}
