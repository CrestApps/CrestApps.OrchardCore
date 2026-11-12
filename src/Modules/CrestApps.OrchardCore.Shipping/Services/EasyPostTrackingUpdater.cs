using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.Webhooks.Core.Signatures;
using CrestApps.OrchardCore.Webhooks.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Shipping.Services;

/// <summary>
/// Applies EasyPost's tracking updates to the shipment sent with the tracking number: where the parcel has been, and
/// delivered when it arrived.
/// </summary>
public sealed class EasyPostTrackingUpdater
{
    private const int MaxEvents = 50;
    private const string SignaturePrefix = "hmac-sha256-hex=";

    // EasyPost signs the body with HMAC-SHA256, sent as "hmac-sha256-hex=<lowercase hex>".
    private static readonly IWebhookSignatureValidator _signature = new HmacWebhookSignatureValidator("easypost-hmac-sha256-hex", HashAlgorithmName.SHA256, WebhookSignatureEncoding.Hex, SignaturePrefix);

    private readonly IShipmentStore _shipments;
    private readonly IShipmentService _shipmentService;
    private readonly IClock _clock;

    public EasyPostTrackingUpdater(IShipmentStore shipments, IShipmentService shipmentService, IClock clock)
    {
        _shipments = shipments;
        _shipmentService = shipmentService;
        _clock = clock;
    }

    /// <summary>
    /// Returns whether the update was signed with the webhook secret, as EasyPost signs them.
    /// </summary>
    public static bool IsSigned(string body, string signature, string secret)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(signature))
        {
            return false;
        }

        // EasyPost normalizes the secret (NFKD) before keying the HMAC.
        return _signature.IsValid(new WebhookSignatureValidationContext
        {
            Secret = secret.Normalize(NormalizationForm.FormKD),
            Signature = signature,
            SignedContent = body ?? string.Empty,
        });
    }

    /// <summary>
    /// Applies a tracker update. Returns whether a shipment was found for it.
    /// </summary>
    public async Task<bool> ApplyAsync(JsonObject update, CancellationToken cancellationToken = default)
    {
        if (update?["result"] is not JsonObject tracker || tracker["tracking_code"]?.GetValue<string>() is not { Length: > 0 } trackingNumber)
        {
            return false;
        }

        var shipment = await _shipments.FindByTrackingNumberAsync(trackingNumber, cancellationToken);

        if (shipment is null)
        {
            return false;
        }

        var status = tracker["status"]?.GetValue<string>();
        var events = (tracker["tracking_details"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(detail => new ShipmentTrackingEvent
            {
                Utc = DateTime.TryParse(detail["datetime"]?.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var utc) ? utc : _clock.UtcNow,
                Status = Describe(detail["status"]?.GetValue<string>()),
                Message = detail["message"]?.GetValue<string>(),
                Location = string.Join(", ", new[] { detail["tracking_location"]?["city"]?.GetValue<string>(), detail["tracking_location"]?["state"]?.GetValue<string>() }.Where(part => !string.IsNullOrWhiteSpace(part))),
            })
            .OrderByDescending(entry => entry.Utc)
            .Take(MaxEvents)
            .ToList();

        // EasyPost retries and may deliver late: an update older than what the shipment already shows (the same
        // signed body sent again, or an earlier one arriving last) doesn't move the parcel back.
        if (shipment.TrackingEvents is { Count: > 0 } known &&
            (events.Count == 0 || events[0].Utc < known.Max(entry => entry.Utc)))
        {
            return false;
        }

        shipment.TrackingStatus = Describe(status);
        shipment.TrackingEvents = events;

        if (tracker["public_url"]?.GetValue<string>() is { Length: > 0 } url && string.IsNullOrEmpty(shipment.TrackingUrl))
        {
            shipment.TrackingUrl = url;
        }

        shipment.ModifiedUtc = _clock.UtcNow;

        // Delivered moves the shipment on, which saves it; anything else just records where it is.
        if (status == "delivered" && shipment.Status == ShipmentStatus.Shipped)
        {
            await _shipmentService.DeliverAsync(shipment, cancellationToken);
        }
        else
        {
            await _shipments.UpdateAsync(shipment, cancellationToken);
        }

        return true;
    }

    // EasyPost's statuses are codes, such as "in_transit" or "out_for_delivery".
    internal static string Describe(string status)
        => string.IsNullOrEmpty(status) ? null : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(status.Replace('_', ' ').ToLowerInvariant()) is var text && text.Length > 1 ? char.ToUpperInvariant(text[0]) + text[1..].ToLowerInvariant() : text;
}
