using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Services;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Bookings.Recipes;

/// <summary>
/// The "BookingDates" step: adds dates to booking products, such as a tour's departures, each as <c>Date</c>
/// (<c>yyyy-MM-dd</c>, with <c>Time</c> for a time slot) or <c>DaysFromToday</c>, so a sample's dates stay ahead of
/// today, with an optional <c>Capacity</c> and <c>Price</c>. A date the product has already is left as it is.
/// </summary>
internal sealed class BookingDatesStep : NamedRecipeStepHandler
{
    public const string RecipeStepName = "BookingDates";

    private readonly IBookingSlotStore _slots;
    private readonly BookingCapacityService _capacity;
    private readonly IClock _clock;

    public BookingDatesStep(IBookingSlotStore slots, BookingCapacityService capacity, IClock clock)
        : base(RecipeStepName)
    {
        _slots = slots;
        _capacity = capacity;
        _clock = clock;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var timeZone = await _capacity.GetTimeZoneAsync();
        var today = DateOnly.FromDateTime(await _capacity.GetLocalNowAsync());

        foreach (var token in (context.Step["Dates"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var contentItemId = token["ContentItemId"]?.GetValue<string>();
            var date = token["DaysFromToday"] is { } days
                ? BookingSchedule.ToKey(today.AddDays(days.GetValue<int>()))
                : token["Date"]?.GetValue<string>();
            var key = token["Time"]?.GetValue<string>() is { Length: > 0 } time ? date + "T" + time : date;

            if (string.IsNullOrEmpty(contentItemId) || BookingSchedule.GetStartUtc(key, timeZone) is not { } startsUtc)
            {
                context.Errors.Add($"The '{RecipeStepName}' step has a date without a product or a date that doesn't read as yyyy-MM-dd: '{key}'.");

                continue;
            }

            if (await _slots.FindAsync(contentItemId, key) is not null)
            {
                continue;
            }

            await _slots.CreateAsync(new BookingSlot
            {
                ItemId = global::OrchardCore.IdGenerator.GenerateId(),
                ContentItemId = contentItemId,
                SlotKey = key,
                StartsUtc = startsUtc,
                Capacity = Math.Max(0, token["Capacity"]?.GetValue<int>() ?? 10),
                Price = token["Price"] is { } price ? decimal.Parse(price.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture) : null,
                ModifiedUtc = _clock.UtcNow,
            });
        }
    }
}
