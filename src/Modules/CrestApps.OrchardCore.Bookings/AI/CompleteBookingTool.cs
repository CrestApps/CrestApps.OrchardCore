using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Bookings.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Bookings.AI;

/// <summary>
/// Marks a confirmed booking as completed or as a no-show, through the finish its detail page uses
/// (<see cref="BookingService.FinishAsync"/>), which also notes it on the order under the caller's name.
/// </summary>
internal sealed class CompleteBookingTool : BookingsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "completeBooking";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "bookingId": {
          "type": "string",
          "description": "The booking's id, from listBookings. It must be confirmed."
        },
        "outcome": {
          "type": "string",
          "enum": ["completed", "noShow"],
          "description": "completed when the booking took place, noShow when the customer did not come."
        }
      },
      "required": ["bookingId", "outcome"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Marks a confirmed booking as completed, or as a no-show. Only a confirmed booking can be finished.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (ReadString(arguments, "bookingId") is not { } bookingId)
        {
            return "Nothing was changed: give the booking's id. Use listBookings to find it.";
        }

        var outcome = ReadString(arguments, "outcome");
        var noShow = string.Equals(outcome, "noShow", StringComparison.OrdinalIgnoreCase);

        if (!noShow && !string.Equals(outcome, "completed", StringComparison.OrdinalIgnoreCase))
        {
            return "Nothing was changed: outcome must be completed or noShow.";
        }

        if (await services.GetRequiredService<IBookingStore>().FindByIdAsync(bookingId, cancellationToken) is not { } booking)
        {
            return $"Nothing was changed: no booking has the id '{bookingId}'. Use listBookings to find it.";
        }

        if (!await services.GetRequiredService<BookingService>().FinishAsync(booking, noShow, GetActor(caller), cancellationToken))
        {
            return $"Nothing was changed: booking '{booking.Title}' is {ToCamel(booking.Status.ToString())}. Only a confirmed booking can be completed or marked as a no-show.";
        }

        if (await CommitAsync(services, cancellationToken) is { } error)
        {
            return error;
        }

        return Json(new
        {
            bookingId = booking.ItemId,
            title = booking.Title,
            status = ToCamel(booking.Status.ToString()),
            orderNumber = booking.OrderNumber,
            message = noShow
                ? $"Booking '{booking.Title}' was marked as a no-show, and the order notes it."
                : $"Booking '{booking.Title}' was completed, and the order notes it.",
        });
    }
}
