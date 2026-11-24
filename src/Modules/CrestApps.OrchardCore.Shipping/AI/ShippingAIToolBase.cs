using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using YesSql;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Shipping.AI;

/// <summary>
/// A Shipping AI tool. It acts as the person using the AI: it refuses a caller who is not signed in or who lacks the
/// permission of the admin page it stands for (<see cref="ShippingPermissions.FulfillOrders"/> for shipments and labels,
/// <see cref="ShippingPermissions.ManageShipping"/> for rates), before it reads an argument, and refuses any argument
/// its schema does not declare. A change is committed at once, as the page does at the end of its request.
/// </summary>
internal abstract class ShippingAIToolBase : AIFunction
{
    /// <summary>
    /// The answer to a caller who is not signed in or lacks the permission.
    /// </summary>
    public const string NotAllowedMessage = "You are not allowed to use this function.";

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private HashSet<string> _argumentNames;

    /// <summary>
    /// Gets the permission the caller needs; <see cref="ShippingPermissions.FulfillOrders"/> unless a tool says otherwise.
    /// </summary>
    protected virtual Permission Permission => ShippingPermissions.FulfillOrders;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;
        var user = services.GetService<IUserAccessor>()?.User ?? services.GetService<IHttpContextAccessor>()?.HttpContext?.User;
        var caller = user?.Identity?.IsAuthenticated == true ? user : null;
        var authorizationService = services.GetService<IAuthorizationService>();

        if (caller is null || authorizationService is null || !await authorizationService.AuthorizeAsync(caller, Permission))
        {
            return NotAllowedMessage;
        }

        _argumentNames ??= GetArgumentNames(JsonSchema);

        if (arguments.Keys.FirstOrDefault(name => !_argumentNames.Contains(name)) is { } unknown)
        {
            return $"Nothing was done: '{unknown}' is not an argument of {Name}. Use only the arguments the tool declares.";
        }

        return await InvokeAsync(arguments, services, caller, cancellationToken);
    }

    /// <summary>
    /// Runs the tool for a signed-in caller who has the permission.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool's result.</returns>
    protected abstract Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken);

    /// <summary>
    /// Finds an order by the <c>orderId</c> or <c>orderNumber</c> argument.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="arguments">The arguments.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The order, or the sentence that says why there is none.</returns>
    protected static async Task<(Order Order, string Error)> FindOrderAsync(IServiceProvider services, AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var store = services.GetRequiredService<IOrderStore>();

        if (ReadString(arguments, "orderId") is { } orderId)
        {
            return await store.FindByIdAsync(orderId, cancellationToken) is { } byId
                ? (byId, null)
                : (null, $"Nothing was done: no order has the id '{orderId}'. Use listOrders to find it.");
        }

        if (ReadString(arguments, "orderNumber") is { } number)
        {
            return await store.FindByNumberAsync(number, cancellationToken) is { } byNumber
                ? (byNumber, null)
                : (null, $"Nothing was done: no order has the number '{number}'. Use listOrders to find it.");
        }

        return (null, "Nothing was done: give the order's id or its number. Use listOrders to find it.");
    }

    /// <summary>
    /// Finds a shipment by the <c>shipmentId</c> argument.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="arguments">The arguments.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The shipment, or the sentence that says why there is none.</returns>
    protected static async Task<(Shipment Shipment, string Error)> FindShipmentAsync(IServiceProvider services, AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (ReadString(arguments, "shipmentId") is not { } shipmentId)
        {
            return (null, "Nothing was done: give the shipment's id. Use getOrderFulfillment to find it.");
        }

        return await services.GetRequiredService<IShipmentStore>().FindByIdAsync(shipmentId, cancellationToken) is { } shipment
            ? (shipment, null)
            : (null, $"Nothing was done: no shipment has the id '{shipmentId}'. Use getOrderFulfillment to find it.");
    }

    /// <summary>
    /// Commits the work of the session at once. When the order or the shipment changed at the same moment, nothing is
    /// kept and the answer says so.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="null"/> when it was saved, or the sentence that says why not.</returns>
    protected static async Task<string> CommitAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var session = services.GetRequiredService<YesSqlSession>();

        try
        {
            await session.SaveChangesAsync(cancellationToken);

            return null;
        }
        catch (ConcurrencyException)
        {
            await session.CancelAsync();

            return "Nothing was saved: the order or the shipment changed at the same moment. Read it again with getOrderFulfillment and retry in your next message.";
        }
    }

    /// <summary>
    /// Turns a shipment service refusal into the sentence the order page shows.
    /// </summary>
    /// <param name="error">The refusal.</param>
    /// <returns>The sentence.</returns>
    protected static string Explain(ShipmentError error)
        => error switch
        {
            ShipmentError.OrderNotPaid => "Nothing was done: only a paid order can be shipped.",
            ShipmentError.NothingToShip => "Nothing was done: say how many of each line go in the shipment.",
            ShipmentError.TooMany => "Nothing was done: that's more than is left to ship. Lower the quantities; getOrderFulfillment says what is left.",
            _ => "Nothing was done: the shipment can't change that way now. Read the order again with getOrderFulfillment.",
        };

    /// <summary>
    /// Builds the allow-listed fields of a shipment.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="shipment">The shipment.</param>
    /// <returns>The fields.</returns>
    protected static async Task<object> DescribeAsync(IServiceProvider services, Shipment shipment)
    {
        var events = new List<object>();

        foreach (var entry in (shipment.TrackingEvents ?? []).OrderByDescending(entry => entry.Utc).Take(20))
        {
            events.Add(new
            {
                at = await LocalTimeAsync(services, entry.Utc),
                status = entry.Status,
                message = entry.Message,
                location = entry.Location,
            });
        }

        return new
        {
            id = shipment.ItemId,
            number = shipment.Number,
            orderId = shipment.OrderId,
            orderNumber = shipment.OrderNumber,
            status = ToCamel(shipment.Status.ToString()),
            lines = shipment.Lines.Select(line => new { lineId = line.LineId, title = line.Title, sku = line.Sku, quantity = line.Quantity }),
            carrier = shipment.Carrier,
            trackingNumber = shipment.TrackingNumber,
            trackingUrl = shipment.TrackingUrl,
            trackingStatus = shipment.TrackingStatus,
            trackingEvents = events,
            note = shipment.Note,
            hasLabel = !string.IsNullOrEmpty(shipment.LabelUrl),
            labelCost = shipment.LabelCost,
            labelCurrency = shipment.LabelCurrency,
            createdAt = await LocalTimeAsync(services, shipment.CreatedUtc),
            shippedAt = await LocalTimeAsync(services, shipment.ShippedUtc),
            deliveredAt = await LocalTimeAsync(services, shipment.DeliveredUtc),
            returnedAt = await LocalTimeAsync(services, shipment.ReturnedUtc),
        };
    }

    /// <summary>
    /// Writes a UTC time in the caller's or the site's time zone.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="utc">The UTC time, or <see langword="null"/>.</param>
    /// <returns>The local time with its offset, or <see langword="null"/>.</returns>
    protected static async Task<string> LocalTimeAsync(IServiceProvider services, DateTime? utc)
    {
        if (utc is null || utc.Value == default || services.GetService<ILocalClock>() is not { } localClock)
        {
            return null;
        }

        var local = await localClock.ConvertToLocalAsync(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc));

        return local.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Writes an enum value as the tools name it, starting in lower case.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The name.</returns>
    protected static string ToCamel(string value)
        => string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];

    /// <summary>
    /// Writes an amount for a sentence.
    /// </summary>
    /// <param name="amount">The amount.</param>
    /// <param name="currency">The currency.</param>
    /// <returns>The amount and currency.</returns>
    protected static string Format(decimal amount, string currency)
        => $"{amount.ToString("0.00##", CultureInfo.InvariantCulture)} {currency}";

    /// <summary>
    /// Writes a result as JSON.
    /// </summary>
    /// <param name="value">The result, built from an explicit list of fields.</param>
    /// <returns>The JSON text.</returns>
    protected static string Json(object value)
        => JsonSerializer.Serialize(value, _jsonOptions);

    /// <summary>
    /// Parses a JSON schema.
    /// </summary>
    /// <param name="schema">The schema text.</param>
    /// <returns>The schema.</returns>
    protected static JsonElement Schema(string schema)
        => JsonSerializer.Deserialize<JsonElement>(schema);

    /// <summary>
    /// Reads an argument as JSON.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The value, or <see langword="null"/> when it is missing or null.</returns>
    protected static JsonElement? ReadJson(AIFunctionArguments arguments, string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        var element = value is JsonElement json ? json : JsonSerializer.SerializeToElement(value);

        return element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : element;
    }

    /// <summary>
    /// Reads a text argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The trimmed text, or <see langword="null"/> when it is missing or blank.</returns>
    protected static string ReadString(AIFunctionArguments arguments, string name)
    {
        if (ReadJson(arguments, name) is not { } element)
        {
            return null;
        }

        var text = element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// Reads a decimal number argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="value">The number, or <see langword="null"/> when it is missing.</param>
    /// <returns><see langword="false"/> when it is given but is not a number.</returns>
    protected static bool TryReadDecimal(AIFunctionArguments arguments, string name, out decimal? value)
    {
        value = null;

        if (ReadJson(arguments, name) is not { } element)
        {
            return true;
        }

        if ((element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var number)) ||
            (element.ValueKind == JsonValueKind.String && decimal.TryParse(element.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out number)))
        {
            value = number;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Reads a yes or no argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The value, or <see langword="null"/> when it is missing or not true or false.</returns>
    protected static bool? ReadBool(AIFunctionArguments arguments, string name)
        => ReadJson(arguments, name) is { } element
            ? element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(element.GetString(), out var parsed) => parsed,
                _ => null,
            }
            : null;

    /// <summary>
    /// Reads the <c>lines</c> argument: a list of order lines and quantities.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="lines">The lines, or <see langword="null"/> when the argument is missing.</param>
    /// <returns><see langword="false"/> when the argument is given but is not a list of lines with a whole quantity.</returns>
    protected static bool TryReadLines(AIFunctionArguments arguments, out List<ShipmentLineRequest> lines)
    {
        lines = null;

        if (ReadJson(arguments, "lines") is not { } element)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        lines = [];

        foreach (var entry in element.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object ||
                !entry.TryGetProperty("lineId", out var lineId) || lineId.ValueKind != JsonValueKind.String ||
                !entry.TryGetProperty("quantity", out var quantity) || quantity.ValueKind != JsonValueKind.Number || !quantity.TryGetInt32(out var count) || count < 1)
            {
                return false;
            }

            lines.Add(new ShipmentLineRequest(lineId.GetString(), count));
        }

        return true;
    }

    private static HashSet<string> GetArgumentNames(JsonElement schema)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        if (schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in properties.EnumerateObject())
            {
                names.Add(property.Name);
            }
        }

        return names;
    }
}
