using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
using CrestApps.OrchardCore.AgenticCommerce.Payments;

namespace CrestApps.OrchardCore.AgenticCommerce.Acp;

/// <summary>
/// Turns ACP checkout JSON into the agent checkout's input and its view back into an ACP checkout session.
/// </summary>
public sealed class AcpCheckoutMapper
{
    private readonly IAgentCheckoutService _checkouts;

    public AcpCheckoutMapper(IAgentCheckoutService checkouts)
    {
        _checkouts = checkouts;
    }

    /// <summary>
    /// Returns the input of a create request.
    /// </summary>
    /// <param name="body">The request's body.</param>
    public static AgentCheckoutInput ReadInput(JsonObject body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var (address, buyerFromDetails) = ReadFulfillmentDetails(body["fulfillment_details"]);

        return new AgentCheckoutInput
        {
            Lines = ReadLines(body) ?? [],
            Buyer = ReadBuyer(body["buyer"]) ?? buyerFromDetails,
            Fulfillment = address is null ? null : new AgentFulfillmentInput { Address = address, OptionId = ReadSelectedOption(body) },
            DiscountCodes = AgentJson.Strings(body["discounts"], "codes") ?? AgentJson.Strings(body, "coupons") ?? [],
            Attribution = body["affiliate_attribution"]?.DeepClone() as JsonObject,
            OrderNotes = AgentJson.String(body, "order_notes"),
        };
    }

    /// <summary>
    /// Returns the changes of an update request: a member left out stays as it is, and <c>null</c> clears it.
    /// </summary>
    /// <param name="body">The request's body.</param>
    /// <param name="current">The checkout as it stands, for what isn't changed.</param>
    public static AgentCheckoutUpdate ReadUpdate(JsonObject body, AgentCheckoutView current)
    {
        ArgumentNullException.ThrowIfNull(body);

        var update = new AgentCheckoutUpdate
        {
            Lines = ReadLines(body),
            Attribution = body["affiliate_attribution"]?.DeepClone() as JsonObject,
            OrderNotes = AgentJson.String(body, "order_notes"),
        };

        if (body.ContainsKey("buyer"))
        {
            update.Buyer = ReadBuyer(body["buyer"]) ?? new AgentBuyer();
        }

        if (body["discounts"] is JsonObject)
        {
            update.DiscountCodes = AgentJson.Strings(body["discounts"], "codes") ?? [];
        }
        else if (body.ContainsKey("coupons"))
        {
            update.DiscountCodes = AgentJson.Strings(body, "coupons") ?? [];
        }

        var option = ReadSelectedOption(body);

        if (body.ContainsKey("fulfillment_details"))
        {
            var (address, buyer) = ReadFulfillmentDetails(body["fulfillment_details"]);

            if (address is null)
            {
                update.ClearFulfillment = true;
            }
            else
            {
                update.Fulfillment = new AgentFulfillmentInput { Address = address, OptionId = option ?? current?.Fulfillment?.SelectedOptionId };
            }

            update.Buyer ??= buyer is null || current?.Buyer is not null ? null : buyer;
        }
        else if (option is not null && current?.Fulfillment?.Address is not null)
        {
            update.Fulfillment = new AgentFulfillmentInput { Address = current.Fulfillment.Address, OptionId = option };
        }
        else if (option is not null && current?.Fulfillment?.PickupLocations.Any(location => location.Id == option) == true)
        {
            update.Fulfillment = new AgentFulfillmentInput { PickupLocationId = option };
        }

        return update;
    }

    /// <summary>
    /// Returns the payment of a complete request, in the payment handlers' shape or the older provider shape.
    /// </summary>
    /// <param name="body">The request's body.</param>
    public static AgentPayment ReadPayment(JsonObject body)
    {
        if (body?["payment_data"] is not JsonObject payment)
        {
            return null;
        }

        var handlerId = AgentJson.String(payment, "handler_id");
        var token = AgentJson.String(payment["instrument"]?["credential"], "token") ?? AgentJson.String(payment, "token");

        if (string.IsNullOrEmpty(handlerId))
        {
            handlerId = AgentJson.String(payment, "provider") switch
            {
                "stripe" => StripeSharedPaymentTokenHandler.AcpName,
                "test" => TestAgentPaymentHandler.Name,
                _ => null,
            };
        }

        return new AgentPayment
        {
            HandlerId = handlerId,
            InstrumentType = AgentJson.String(payment["instrument"], "type") ?? "card",
            CredentialType = AgentJson.String(payment["instrument"]?["credential"], "type"),
            Credential = token,
            BillingAddress = ReadAddress(payment["billing_address"]),
        };
    }

    /// <summary>
    /// Returns a checkout as an ACP checkout session.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="result">The agent checkout and where it stands.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<JsonObject> WriteAsync(AgentRequest request, AgentCheckoutResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);

        var view = result.View;
        var currency = view.Currency?.ToLowerInvariant();
        var handlers = await _checkouts.GetPaymentHandlersAsync(request.Context, view, cancellationToken);
        var json = new JsonObject
        {
            ["id"] = view.Id,
            ["protocol"] = new JsonObject { ["version"] = request.Version },
            ["capabilities"] = new JsonObject
            {
                ["payment"] = new JsonObject
                {
                    ["handlers"] = new JsonArray([.. handlers.Select(handler => (JsonNode)WriteHandler(handler))]),
                },
                ["extensions"] = new JsonArray(new JsonObject { ["name"] = "discount" }),
            },
            ["status"] = ToStatus(view.Status),
            ["currency"] = currency,
            ["line_items"] = WriteLines(view),
            ["fulfillment_options"] = WriteFulfillmentOptions(view),
            ["totals"] = WriteTotals(view),
            ["messages"] = new JsonArray([.. view.Messages.Select(message => (JsonNode)WriteMessage(message))]),
            ["links"] = new JsonArray([.. view.Links.Select(link => (JsonNode)new JsonObject { ["type"] = ToLinkType(link.Type), ["url"] = link.Url })]),
        };

        if (view.Buyer is not null)
        {
            json["buyer"] = WriteBuyer(view.Buyer);
        }

        if (view.Fulfillment?.Address is not null)
        {
            json["fulfillment_details"] = new JsonObject
            {
                ["name"] = view.Fulfillment.Address.Name ?? view.Buyer?.GetFullName(),
                ["address"] = WriteAddress(view.Fulfillment.Address, view.Buyer),
            };
        }

        var selected = view.Fulfillment?.SelectedPickupLocationId ?? view.Fulfillment?.SelectedOptionId;

        if (!string.IsNullOrEmpty(selected))
        {
            json["selected_fulfillment_options"] = new JsonArray(new JsonObject
            {
                ["type"] = string.IsNullOrEmpty(view.Fulfillment.SelectedPickupLocationId) ? "shipping" : "pickup",
                ["option_id"] = selected,
                ["item_ids"] = new JsonArray([.. view.Lines.Select(line => (JsonNode)line.Id)]),
            });
        }

        json["discounts"] = new JsonObject
        {
            ["codes"] = new JsonArray([.. view.DiscountCodes.Select(code => (JsonNode)code)]),
            ["applied"] = new JsonArray([.. view.Discounts.Select(discount => (JsonNode)WriteDiscount(discount, currency))]),
        };

        if (view.ExpiresUtc is { } expires)
        {
            json["expires_at"] = AgentJson.ToRfc3339(expires);
        }

        if (!string.IsNullOrEmpty(view.ContinueUrl))
        {
            json["continue_url"] = view.ContinueUrl;
        }

        if (view.Order is not null)
        {
            json["order"] = new JsonObject
            {
                ["id"] = view.Order.Id,
                ["checkout_session_id"] = view.Id,
                ["permalink_url"] = view.Order.PermalinkUrl,
                ["order_number"] = view.Order.Number,
            };
        }

        return json;
    }

    internal static string ToStatus(AgentCheckoutStatus status)
        => status switch
        {
            AgentCheckoutStatus.ReadyForComplete => "ready_for_payment",
            AgentCheckoutStatus.RequiresEscalation => "requires_escalation",
            AgentCheckoutStatus.CompleteInProgress => "complete_in_progress",
            AgentCheckoutStatus.Completed => "completed",
            AgentCheckoutStatus.Canceled => "canceled",
            AgentCheckoutStatus.Expired => "expired",
            _ => "not_ready_for_payment",
        };

    private static string ToLinkType(string type)
        => type switch
        {
            AgentLinkTypes.TermsOfService => "terms_of_use",
            _ => type,
        };

    private static JsonObject WriteHandler(AgentPaymentHandlerDescriptor handler)
    {
        var json = new JsonObject
        {
            ["id"] = handler.Id,
            ["name"] = handler.Name,
            ["version"] = handler.Version,
            ["spec"] = handler.Spec,
            ["requires_delegate_payment"] = handler.RequiresDelegatePayment,
            ["requires_pci_compliance"] = false,
            ["psp"] = handler.Psp ?? "seller_managed",
            ["config_schema"] = handler.Schema,
            ["instrument_schemas"] = new JsonArray(handler.Schema),
            ["config"] = handler.Config?.DeepClone() ?? new JsonObject(),
        };

        AgentJson.AddIf(json, "display_name", handler.DisplayName);

        return json;
    }

    private static JsonArray WriteLines(AgentCheckoutView view)
    {
        var lines = new JsonArray();

        foreach (var line in view.Lines)
        {
            lines.Add(new JsonObject
            {
                ["id"] = line.Id,
                ["item"] = new JsonObject { ["id"] = line.ItemId },
                ["quantity"] = line.Quantity,
                ["name"] = line.Title,
                ["unit_amount"] = AgentJson.ToMinor(line.UnitPrice, view.Currency),
                ["totals"] = new JsonArray
                {
                    Total("items_base_amount", "Base amount", line.Subtotal, view.Currency),
                    Total("discount", "Discount", line.Discount, view.Currency),
                    Total("subtotal", "Subtotal", line.Total, view.Currency),
                    Total("total", "Total", line.Total, view.Currency),
                },
            });
        }

        return lines;
    }

    private static JsonArray WriteFulfillmentOptions(AgentCheckoutView view)
    {
        var options = new JsonArray();

        foreach (var option in view.Fulfillment?.Options ?? [])
        {
            var entry = new JsonObject
            {
                ["type"] = "shipping",
                ["id"] = option.Id,
                ["title"] = option.Title,
                ["totals"] = new JsonArray(Total("total", "Shipping", option.Amount, view.Currency)),
            };

            AgentJson.AddIf(entry, "description", option.Description);
            AgentJson.AddIf(entry, "carrier", option.Carrier);
            options.Add(entry);
        }

        foreach (var location in view.Fulfillment?.PickupLocations ?? [])
        {
            options.Add(new JsonObject
            {
                ["type"] = "pickup",
                ["id"] = location.Id,
                ["title"] = location.Name,
                ["location"] = new JsonObject
                {
                    ["name"] = location.Name,
                    ["address"] = location.Address is null ? new JsonObject() : WriteAddress(location.Address, buyer: null),
                },
                ["totals"] = new JsonArray(Total("total", "Pickup", location.Fee, view.Currency)),
            });
        }

        return options;
    }

    private static JsonArray WriteTotals(AgentCheckoutView view)
    {
        var totals = view.Totals;
        var currency = view.Currency;
        var result = new JsonArray
        {
            Total("items_base_amount", "Item(s) total", totals.Subtotal, currency),
        };

        if (totals.Discount != 0)
        {
            result.Add(Total("discount", "Discount", totals.Discount, currency));
        }

        result.Add(Total("subtotal", "Subtotal", totals.Subtotal - totals.Discount, currency));

        if (totals.Fulfillment != 0)
        {
            result.Add(Total("fulfillment", "Shipping", totals.Fulfillment, currency));
        }

        if (totals.Tax != 0)
        {
            result.Add(Total("tax", "Tax", totals.Tax, currency));
        }

        if (totals.Fee != 0)
        {
            result.Add(Total("fee", "Fees", totals.Fee, currency));
        }

        result.Add(Total("total", "Total", totals.Total, currency));

        return result;
    }

    internal static JsonObject Total(string type, string displayText, decimal amount, string currency)
        => new()
        {
            ["type"] = type,
            ["display_text"] = displayText,
            ["amount"] = AgentJson.ToMinor(amount, currency),
        };

    private static JsonObject WriteMessage(AgentCheckoutMessage message)
    {
        var json = new JsonObject
        {
            ["type"] = message.Type switch
            {
                AgentMessageType.Warning => "warning",
                AgentMessageType.Info => "info",
                _ => "error",
            },
        };

        if (message.Type != AgentMessageType.Info)
        {
            json["code"] = ToCode(message);
        }

        json["content_type"] = "plain";
        json["content"] = message.Content;

        if (ToParam(message.Field) is { } param)
        {
            json["param"] = param;
        }

        if (message.Type == AgentMessageType.Error)
        {
            json["severity"] = message.Severity == AgentMessageSeverity.Unrecoverable ? "critical" : "high";
            json["resolution"] = message.Severity switch
            {
                AgentMessageSeverity.RequiresBuyerInput => "requires_buyer_input",
                AgentMessageSeverity.RequiresBuyerReview => "requires_buyer_review",
                _ => "recoverable",
            };
        }

        return json;
    }

    // ACP lists its codes; the store's own map to the closest.
    private static string ToCode(AgentCheckoutMessage message)
        => message.Code switch
        {
            AgentMessageCodes.ItemUnavailable or AgentMessageCodes.OutOfStock => "out_of_stock",
            AgentMessageCodes.AddressUndeliverable => "region_restricted",
            AgentMessageCodes.PaymentFailed or AgentMessageCodes.PaymentDeclined => "payment_declined",
            AgentMessageCodes.DiscountCodeInvalid => "discount_code_invalid",
            AgentMessageCodes.PriceChanged => message.Type == AgentMessageType.Warning ? "price_change" : "invalid",
            AgentMessageCodes.RequiresBuyerInput => "approval_required",
            AgentMessageCodes.InvalidState => "conflict",
            _ => message.Code,
        };

    private static string ToParam(string field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return null;
        }

        return field switch
        {
            AgentCheckoutFields.BuyerEmail => "$.buyer.email",
            AgentCheckoutFields.BuyerName => "$.buyer.first_name",
            AgentCheckoutFields.BuyerPhone => "$.buyer.phone_number",
            AgentCheckoutFields.FulfillmentAddress => "$.fulfillment_details.address",
            AgentCheckoutFields.FulfillmentOption or AgentCheckoutFields.FulfillmentPickup => "$.selected_fulfillment_options[0].option_id",
            AgentCheckoutFields.BillingAddress => "$.payment_data.billing_address",
            AgentCheckoutFields.Payment => "$.payment_data",
            _ => "$." + field,
        };
    }

    private static JsonObject WriteBuyer(AgentBuyer buyer)
    {
        var json = new JsonObject();

        AgentJson.AddIf(json, "email", buyer.Email);
        AgentJson.AddIf(json, "first_name", buyer.FirstName);
        AgentJson.AddIf(json, "last_name", buyer.LastName);
        AgentJson.AddIf(json, "full_name", buyer.FullName);
        AgentJson.AddIf(json, "phone_number", buyer.Phone);

        return json;
    }

    private static JsonObject WriteDiscount(AgentAppliedDiscount discount, string currency)
    {
        var json = new JsonObject();

        AgentJson.AddIf(json, "code", discount.Code);
        AgentJson.AddIf(json, "description", discount.Title);
        json["amount"] = AgentJson.ToMinor(discount.Amount, currency);
        json["automatic"] = discount.Automatic;

        return json;
    }

    internal static JsonObject WriteAddress(Address address, AgentBuyer buyer)
    {
        var json = new JsonObject
        {
            ["name"] = address.Name ?? buyer?.GetFullName() ?? string.Empty,
            ["line_one"] = address.AddressLine1 ?? string.Empty,
        };

        AgentJson.AddIf(json, "line_two", address.AddressLine2);
        json["city"] = address.City ?? string.Empty;
        json["state"] = address.Region ?? string.Empty;
        json["country"] = address.Country ?? string.Empty;
        json["postal_code"] = address.PostalCode ?? string.Empty;
        AgentJson.AddIf(json, "company", address.Company);

        return json;
    }

    internal static Address ReadAddress(JsonNode node, string fallbackName = null, string phone = null)
    {
        if (node is not JsonObject json)
        {
            return null;
        }

        return new Address
        {
            Name = AgentJson.String(json, "name") ?? fallbackName,
            Phone = phone,
            Company = AgentJson.String(json, "company"),
            AddressLine1 = AgentJson.String(json, "line_one"),
            AddressLine2 = AgentJson.String(json, "line_two"),
            City = AgentJson.String(json, "city"),
            Region = AgentJson.String(json, "state"),
            PostalCode = AgentJson.String(json, "postal_code"),
            Country = AgentJson.String(json, "country")?.ToUpperInvariant(),
        };
    }

    private static (Address Address, AgentBuyer Buyer) ReadFulfillmentDetails(JsonNode node)
    {
        if (node is not JsonObject json)
        {
            return (null, null);
        }

        var address = ReadAddress(json["address"], AgentJson.String(json, "name"), AgentJson.String(json, "phone_number"));

        var email = AgentJson.String(json, "email");
        var buyer = string.IsNullOrEmpty(email)
            ? null
            : new AgentBuyer
            {
                Email = email,
                FullName = AgentJson.String(json, "name"),
                Phone = AgentJson.String(json, "phone_number"),
            };

        return (address, buyer);
    }

    private static string ReadSelectedOption(JsonObject body)
        => body["selected_fulfillment_options"] is JsonArray options
            ? options.OfType<JsonObject>().Select(option => AgentJson.String(option, "option_id")).FirstOrDefault(id => !string.IsNullOrEmpty(id))
            : AgentJson.String(body, "fulfillment_option_id");

    private static List<AgentCheckoutLineInput> ReadLines(JsonObject body)
    {
        // 2026-01-30 renamed "items" to "line_items"; both are read.
        if ((body["line_items"] ?? body["items"]) is not JsonArray items)
        {
            return null;
        }

        return items
            .OfType<JsonObject>()
            .Select(item => new AgentCheckoutLineInput
            {
                ItemId = AgentJson.String(item, "id") ?? AgentJson.String(item["item"], "id"),

                // The 2026-04-17 schema leaves quantity off an item, but every example sends one.
                Quantity = AgentJson.Int(item, "quantity") ?? AgentJson.Int(item["item"], "quantity") ?? 1,
            })
            .ToList();
    }

    private static AgentBuyer ReadBuyer(JsonNode node)
    {
        if (node is not JsonObject json)
        {
            return null;
        }

        return new AgentBuyer
        {
            Email = AgentJson.String(json, "email"),
            FirstName = AgentJson.String(json, "first_name"),
            LastName = AgentJson.String(json, "last_name"),
            FullName = AgentJson.String(json, "full_name") ?? AgentJson.String(json, "name"),
            Phone = AgentJson.String(json, "phone_number"),
        };
    }

    /// <summary>
    /// Returns what the agent observed about the buyer, from a complete request's risk signals.
    /// </summary>
    /// <param name="body">The request's body.</param>
    public static Dictionary<string, string> ReadSignals(JsonObject body)
    {
        var signals = new Dictionary<string, string>(StringComparer.Ordinal);

        if (body?["risk_signals"] is JsonObject json)
        {
            foreach (var (name, value) in json)
            {
                if (value is JsonValue scalar && scalar.GetValueKind() == JsonValueKind.String)
                {
                    signals[name] = scalar.GetValue<string>();
                }
            }
        }

        return signals;
    }

    /// <summary>
    /// Returns the buyer's marketing consent by channel, from a complete request.
    /// </summary>
    /// <param name="body">The request's body.</param>
    public static Dictionary<string, bool> ReadConsents(JsonObject body)
    {
        var consents = new Dictionary<string, bool>(StringComparer.Ordinal);

        foreach (var consent in (body?["marketing_consents"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (AgentJson.String(consent, "channel") is { } channel && AgentJson.Bool(consent, "opted_in") is { } optedIn)
            {
                consents["marketing." + channel] = optedIn;
            }
        }

        return consents;
    }
}
