using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Models;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// Turns UCP checkout JSON into the agent checkout's input and its view back into UCP JSON, in the agreed version's
/// shape.
/// </summary>
public sealed class UcpCheckoutMapper
{
    private readonly IAgentCheckoutService _checkouts;
    private readonly IEnumerable<IUcpCheckoutExtension> _extensions;

    /// <summary>
    /// Initializes a new instance of the <see cref="UcpCheckoutMapper"/> class.
    /// </summary>
    /// <param name="checkouts">The agent checkout.</param>
    /// <param name="extensions">The checkout extensions.</param>
    public UcpCheckoutMapper(IAgentCheckoutService checkouts, IEnumerable<IUcpCheckoutExtension> extensions)
    {
        _checkouts = checkouts;
        _extensions = extensions;
    }

    /// <summary>
    /// Returns the input of a create request.
    /// </summary>
    /// <param name="body">The request's body.</param>
    public static AgentCheckoutInput ReadInput(JsonObject body)
    {
        ArgumentNullException.ThrowIfNull(body);

        return new AgentCheckoutInput
        {
            Lines = ReadLines(body) ?? [],
            Buyer = ReadBuyer(body["buyer"]),
            Fulfillment = ReadFulfillment(body["fulfillment"]),
            DiscountCodes = AgentJson.Strings(body["discounts"], "codes") ?? [],
            Attribution = body["attribution"]?.DeepClone() as JsonObject,
            Signals = ReadSignals(body["signals"]),
            Consents = ReadConsents(body["buyer"]),
        };
    }

    /// <summary>
    /// Returns the changes of an update request. UCP sends the whole checkout; a member it leaves out stays as it is.
    /// </summary>
    /// <param name="body">The request's body.</param>
    public static AgentCheckoutUpdate ReadUpdate(JsonObject body)
    {
        ArgumentNullException.ThrowIfNull(body);

        return new AgentCheckoutUpdate
        {
            Lines = ReadLines(body),
            Buyer = body["buyer"] is JsonObject ? ReadBuyer(body["buyer"]) : null,
            Fulfillment = body["fulfillment"] is JsonObject ? ReadFulfillment(body["fulfillment"]) : null,
            DiscountCodes = body["discounts"] is JsonObject ? AgentJson.Strings(body["discounts"], "codes") ?? [] : null,
            Attribution = body["attribution"]?.DeepClone() as JsonObject,
            Signals = body["signals"] is JsonObject ? ReadSignals(body["signals"]) : null,
            Consents = body["buyer"] is JsonObject ? ReadConsents(body["buyer"]) : null,
        };
    }

    /// <summary>
    /// Returns the payment of a complete request: the selected instrument, or the only one.
    /// </summary>
    /// <param name="body">The request's body.</param>
    public static AgentPayment ReadPayment(JsonObject body)
    {
        if (body?["payment"]?["instruments"] is not JsonArray instruments || instruments.Count == 0)
        {
            return null;
        }

        var instrument = instruments.FirstOrDefault(entry => AgentJson.Bool(entry, "selected") == true) ?? instruments[0];

        var payment = new AgentPayment
        {
            HandlerId = AgentJson.String(instrument, "handler_id"),
            InstrumentType = AgentJson.String(instrument, "type"),
            CredentialType = AgentJson.String(instrument?["credential"], "type"),
            Credential = AgentJson.String(instrument?["credential"], "token"),
            Brand = AgentJson.String(instrument?["display"], "brand"),
            LastDigits = AgentJson.String(instrument?["display"], "last_digits"),
            BillingAddress = ReadAddress(instrument?["billing_address"]),
        };

        // AP2's checkout mandate goes with the payment, so the completion checks it whichever binding brought it.
        if (AgentJson.String(body["ap2"], "checkout_mandate") is { Length: > 0 } mandate)
        {
            payment.Properties[Ap2.Ap2CheckoutExtension.MandateProperty] = mandate;
        }

        return payment;
    }

    /// <summary>
    /// Returns a checkout as UCP JSON.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="result">The agent checkout and where it stands.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<JsonObject> WriteAsync(AgentRequest request, AgentCheckoutResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);

        var view = result.View;
        var currency = view.Currency;
        var json = new JsonObject
        {
            ["ucp"] = await WriteUcpAsync(request, view, error: !result.Succeeded, cancellationToken),
            ["id"] = view.Id,
            ["status"] = ToStatus(view.Status),
            ["currency"] = currency?.ToUpperInvariant(),
            ["line_items"] = WriteLines(view),
            ["totals"] = WriteTotals(view.Totals, currency),
            ["links"] = WriteLinks(view.Links),
            ["payment"] = WritePayment(request.Json),
        };

        if (view.Buyer is not null)
        {
            json["buyer"] = WriteBuyer(view.Buyer);

            if (view.Consents?.Count > 0 && request.Has(UcpConstants.Capabilities.BuyerConsent))
            {
                json["buyer"]["consent"] = WriteConsent(view.Consents, request.Version);
            }
        }

        var messages = new JsonArray();

        foreach (var message in view.Messages)
        {
            messages.Add(WriteMessage(message));
        }

        if (!result.Succeeded && result.Error is not null)
        {
            messages.Add(WriteMessage(AgentCheckoutMessage.Error(AgentMessageCodes.InvalidState, null, result.Error, AgentMessageSeverity.Unrecoverable)));
        }

        if (messages.Count > 0)
        {
            json["messages"] = messages;
        }

        if (view.ExpiresUtc is { } expires)
        {
            json["expires_at"] = AgentJson.ToRfc3339(expires);
        }

        if (!string.IsNullOrEmpty(view.ContinueUrl))
        {
            json["continue_url"] = view.ContinueUrl;
        }

        if (request.Has(UcpConstants.Capabilities.Fulfillment) && view.Fulfillment?.Required == true)
        {
            json["fulfillment"] = WriteFulfillment(view, request.Version);
        }

        if (request.Has(UcpConstants.Capabilities.Discount))
        {
            json["discounts"] = new JsonObject
            {
                ["codes"] = new JsonArray([.. view.DiscountCodes.Select(code => (JsonNode)code)]),
                ["applied"] = new JsonArray([.. view.Discounts.Select(discount => (JsonNode)WriteDiscount(discount, currency))]),
            };
        }

        if (view.Order is not null)
        {
            var order = new JsonObject { ["id"] = view.Order.Id };

            AgentJson.AddIf(order, "label", view.Order.Number);
            AgentJson.AddIf(order, "permalink_url", view.Order.PermalinkUrl);
            json["order"] = order;
        }

        var context = new UcpCheckoutContext
        {
            Request = request,
            Checkout = result.Checkout,
            View = view,
            Json = json,
        };

        foreach (var extension in _extensions)
        {
            await extension.WritingAsync(context);
        }

        return json;
    }

    /// <summary>
    /// Returns a business error answer, for a checkout that couldn't be made: UCP answers it with HTTP 200.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="error">What went wrong.</param>
    /// <param name="code">The error's code.</param>
    public static JsonObject WriteError(AgentRequest request, string error, string code = AgentMessageCodes.Invalid)
        => new()
        {
            ["ucp"] = new JsonObject
            {
                ["version"] = request.Version,
                ["status"] = "error",
            },
            ["messages"] = new JsonArray(WriteMessage(AgentCheckoutMessage.Error(code, null, error, AgentMessageSeverity.Unrecoverable))),
        };

    private async Task<JsonObject> WriteUcpAsync(AgentRequest request, AgentCheckoutView view, bool error, CancellationToken cancellationToken)
    {
        var capabilities = new JsonObject();

        foreach (var (name, version) in request.Capabilities)
        {
            // A checkout answer carries checkout and its extensions; orders and other roots are for their own answers.
            if (name == UcpConstants.Capabilities.Checkout || IsCheckoutExtension(name))
            {
                capabilities[name] = new JsonArray(new JsonObject { ["version"] = version });
            }
        }

        var ucp = new JsonObject
        {
            ["version"] = request.Version,
        };

        if (error)
        {
            ucp["status"] = "error";
        }

        ucp["capabilities"] = capabilities;
        ucp["payment_handlers"] = UcpProfileBuilder.WritePaymentHandlers(await _checkouts.GetPaymentHandlersAsync(request.Context, view, cancellationToken));

        return ucp;
    }

    private static bool IsCheckoutExtension(string name)
        => name is UcpConstants.Capabilities.Fulfillment or UcpConstants.Capabilities.Discount or UcpConstants.Capabilities.BuyerConsent
            or UcpConstants.Capabilities.Ap2Mandate or UcpConstants.Capabilities.Ap2MandateLegacy;

    internal static string ToStatus(AgentCheckoutStatus status)
        => status switch
        {
            AgentCheckoutStatus.RequiresEscalation => "requires_escalation",
            AgentCheckoutStatus.ReadyForComplete => "ready_for_complete",
            AgentCheckoutStatus.CompleteInProgress => "complete_in_progress",
            AgentCheckoutStatus.Completed => "completed",
            AgentCheckoutStatus.Canceled or AgentCheckoutStatus.Expired => "canceled",
            _ => "incomplete",
        };

    private static JsonArray WriteLines(AgentCheckoutView view)
    {
        var lines = new JsonArray();

        foreach (var line in view.Lines)
        {
            var item = new JsonObject
            {
                ["id"] = line.ItemId,
                ["title"] = line.Title,
                ["price"] = AgentJson.ToMinor(line.UnitPrice, view.Currency),
            };

            AgentJson.AddIf(item, "image_url", line.ImageUrl);

            var totals = new JsonArray
            {
                new JsonObject { ["type"] = "subtotal", ["amount"] = AgentJson.ToMinor(line.Subtotal, view.Currency) },
            };

            if (line.Discount > 0)
            {
                totals.Add(new JsonObject { ["type"] = "discount", ["amount"] = -AgentJson.ToMinor(line.Discount, view.Currency) });
            }

            totals.Add(new JsonObject { ["type"] = "total", ["amount"] = AgentJson.ToMinor(line.Total, view.Currency) });

            lines.Add(new JsonObject
            {
                ["id"] = line.Id,
                ["item"] = item,
                ["quantity"] = line.Quantity,
                ["totals"] = totals,
            });
        }

        return lines;
    }

    internal static JsonArray WriteTotals(AgentCheckoutTotals totals, string currency)
    {
        // The parts add up to the total, which every UCP platform checks before it completes on its own.
        var result = new JsonArray
        {
            new JsonObject { ["type"] = "subtotal", ["amount"] = AgentJson.ToMinor(totals.Subtotal, currency) },
        };

        if (totals.Discount != 0)
        {
            result.Add(new JsonObject { ["type"] = "discount", ["amount"] = -AgentJson.ToMinor(totals.Discount, currency) });
        }

        if (totals.Fulfillment != 0)
        {
            result.Add(new JsonObject { ["type"] = "fulfillment", ["amount"] = AgentJson.ToMinor(totals.Fulfillment, currency) });
        }

        if (totals.Tax != 0)
        {
            result.Add(new JsonObject { ["type"] = "tax", ["amount"] = AgentJson.ToMinor(totals.Tax, currency) });
        }

        if (totals.Fee != 0)
        {
            result.Add(new JsonObject { ["type"] = "fee", ["display_text"] = "Fees", ["amount"] = AgentJson.ToMinor(totals.Fee, currency) });
        }

        result.Add(new JsonObject { ["type"] = "total", ["amount"] = AgentJson.ToMinor(totals.Total, currency) });

        return result;
    }

    private static JsonArray WriteLinks(IEnumerable<AgentLink> links)
    {
        var result = new JsonArray();

        foreach (var link in links ?? [])
        {
            var type = link.Type switch
            {
                AgentLinkTypes.TermsOfService => "terms_of_service",
                AgentLinkTypes.PrivacyPolicy => "privacy_policy",
                AgentLinkTypes.ReturnPolicy => "refund_policy",
                AgentLinkTypes.ShippingPolicy => "shipping_policy",
                AgentLinkTypes.Faq => "faq",
                _ => null,
            };

            if (type is not null)
            {
                result.Add(new JsonObject
                {
                    ["type"] = type,
                    ["url"] = link.Url,
                });
            }
        }

        return result;
    }

    private static JsonObject WriteBuyer(AgentBuyer buyer)
    {
        var json = new JsonObject();

        AgentJson.AddIf(json, "first_name", buyer.FirstName);
        AgentJson.AddIf(json, "last_name", buyer.LastName);
        AgentJson.AddIf(json, "full_name", string.IsNullOrEmpty(buyer.FirstName) ? buyer.FullName : null);
        AgentJson.AddIf(json, "email", buyer.Email);
        AgentJson.AddIf(json, "phone_number", buyer.Phone);

        return json;
    }

    // 2026-04-08 gives consent as booleans per purpose; 2026-08-25 as a map of purposes to { granted }.
    internal static JsonObject WriteConsent(IDictionary<string, bool> consents, string version)
    {
        var json = new JsonObject();

        foreach (var (purpose, granted) in consents)
        {
            json[purpose] = version == AgenticCommerceConstants.Versions.Ucp20260408
                ? JsonValue.Create(granted)
                : new JsonObject { ["granted"] = granted };
        }

        return json;
    }

    private static JsonObject WriteDiscount(AgentAppliedDiscount discount, string currency)
    {
        var json = new JsonObject();

        AgentJson.AddIf(json, "code", discount.Code);
        AgentJson.AddIf(json, "title", discount.Title);
        json["amount"] = AgentJson.ToMinor(discount.Amount, currency);
        json["automatic"] = discount.Automatic;

        return json;
    }

    internal static JsonObject WriteMessage(AgentCheckoutMessage message)
    {
        var json = new JsonObject
        {
            ["type"] = message.Type switch
            {
                AgentMessageType.Warning => "warning",
                AgentMessageType.Info => "info",
                _ => "error",
            },
            // UCP's standard code for a refused payment is payment_failed; it has no payment_declined.
            ["code"] = message.Code == AgentMessageCodes.PaymentDeclined ? AgentMessageCodes.PaymentFailed : message.Code,
            ["content"] = message.Content,
        };

        if (ToPath(message.Field) is { } path)
        {
            json["path"] = path;
        }

        if (message.Type == AgentMessageType.Error)
        {
            json["severity"] = message.Severity switch
            {
                AgentMessageSeverity.RequiresBuyerInput => "requires_buyer_input",
                AgentMessageSeverity.RequiresBuyerReview => "requires_buyer_review",
                AgentMessageSeverity.Unrecoverable => "unrecoverable",
                _ => "recoverable",
            };
        }

        return json;
    }

    internal static string ToPath(string field)
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
            AgentCheckoutFields.FulfillmentAddress => "$.fulfillment.methods[0].destinations[0]",
            AgentCheckoutFields.FulfillmentOption => "$.fulfillment.methods[0].groups[0].selected_option_id",
            AgentCheckoutFields.FulfillmentPickup => "$.fulfillment.methods[0].selected_destination_id",
            AgentCheckoutFields.BillingAddress => "$.payment.instruments[0].billing_address",
            AgentCheckoutFields.Payment => "$.payment",
            _ => "$." + field,
        };
    }

    internal static JsonObject WriteFulfillment(AgentCheckoutView view, string version)
    {
        var fulfillment = view.Fulfillment;
        var lineIds = new JsonArray([.. view.Lines.Select(line => (JsonNode)line.Id)]);
        var methods = new JsonArray();
        var pickup = !string.IsNullOrEmpty(fulfillment.SelectedPickupLocationId);

        if (!pickup)
        {
            var shipping = new JsonObject
            {
                ["id"] = "shipping",
                ["type"] = "shipping",
                ["line_item_ids"] = lineIds.DeepClone(),
            };

            if (fulfillment.Address is not null)
            {
                var destination = WriteAddress(fulfillment.Address);

                var destinationId = string.IsNullOrEmpty(fulfillment.DestinationId) ? "dest_1" : fulfillment.DestinationId;

                destination["id"] = destinationId;
                shipping["destinations"] = new JsonArray(destination);
                shipping["selected_destination_id"] = destinationId;

                var options = new JsonArray();

                foreach (var option in fulfillment.Options)
                {
                    var entry = new JsonObject
                    {
                        ["id"] = option.Id,
                        ["title"] = option.Title,
                    };

                    if (!string.IsNullOrEmpty(option.Description))
                    {
                        // 2026-08-25 made the description rich text; 2026-04-08 sends plain text.
                        entry["description"] = version == AgenticCommerceConstants.Versions.Ucp20260408
                            ? option.Description
                            : new JsonObject { ["plain"] = option.Description };
                    }

                    AgentJson.AddIf(entry, "carrier", option.Carrier);
                    entry["totals"] = new JsonArray(new JsonObject { ["type"] = "total", ["amount"] = AgentJson.ToMinor(option.Amount, view.Currency) });
                    options.Add(entry);
                }

                var group = new JsonObject
                {
                    ["id"] = "group_1",
                    ["line_item_ids"] = lineIds.DeepClone(),
                    ["options"] = options,
                };

                if (!string.IsNullOrEmpty(fulfillment.SelectedOptionId))
                {
                    group["selected_option_id"] = fulfillment.SelectedOptionId;
                }

                shipping["groups"] = new JsonArray(group);
            }

            methods.Add(shipping);
        }

        if (fulfillment.PickupLocations.Count > 0)
        {
            var destinations = new JsonArray();

            foreach (var location in fulfillment.PickupLocations)
            {
                var destination = location.Address is null ? new JsonObject() : WriteAddress(location.Address);

                destination["id"] = location.Id;
                destination["name"] = location.Name;
                destinations.Add(destination);
            }

            var pickupMethod = new JsonObject
            {
                ["id"] = "pickup",
                ["type"] = "pickup",
                ["line_item_ids"] = lineIds.DeepClone(),
                ["destinations"] = destinations,
            };

            if (pickup)
            {
                pickupMethod["selected_destination_id"] = fulfillment.SelectedPickupLocationId;
            }

            methods.Add(pickupMethod);
        }

        var available = new JsonArray(new JsonObject { ["type"] = "shipping" });

        if (fulfillment.PickupLocations.Count > 0)
        {
            available.Add(new JsonObject { ["type"] = "pickup" });
        }

        return new JsonObject
        {
            ["methods"] = methods,
            ["available_methods"] = available,
        };
    }

    internal static JsonObject WriteAddress(Address address)
    {
        var json = new JsonObject();

        if (!string.IsNullOrWhiteSpace(address.Name))
        {
            var parts = address.Name.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

            json["first_name"] = parts[0];

            if (parts.Length > 1)
            {
                json["last_name"] = parts[1];
            }
        }

        AgentJson.AddIf(json, "street_address", address.AddressLine1);
        AgentJson.AddIf(json, "extended_address", address.AddressLine2);
        AgentJson.AddIf(json, "address_locality", address.City);
        AgentJson.AddIf(json, "address_region", address.Region);
        AgentJson.AddIf(json, "postal_code", address.PostalCode);
        AgentJson.AddIf(json, "address_country", address.Country);
        AgentJson.AddIf(json, "phone_number", address.Phone);

        return json;
    }

    // UCP requires payment in every checkout answer: the instruments the platform sent with this request, without their
    // credentials (a business must never echo them), or none.
    internal static JsonObject WritePayment(JsonNode body)
    {
        var instruments = new JsonArray();

        if (body?["payment"]?["instruments"] is JsonArray sent)
        {
            foreach (var instrument in sent.OfType<JsonObject>())
            {
                var copy = new JsonObject();

                foreach (var name in new[] { "id", "handler_id", "type", "selected", "display", "billing_address" })
                {
                    if (instrument[name] is { } value)
                    {
                        copy[name] = value.DeepClone();
                    }
                }

                instruments.Add(copy);
            }
        }

        return new JsonObject { ["instruments"] = instruments };
    }

    internal static Address ReadAddress(JsonNode node)
    {
        if (node is not JsonObject json)
        {
            return null;
        }

        var name = AgentJson.String(json, "full_name") ?? $"{AgentJson.String(json, "first_name")} {AgentJson.String(json, "last_name")}".Trim();

        return new Address
        {
            Name = string.IsNullOrWhiteSpace(name) ? null : name,
            AddressLine1 = AgentJson.String(json, "street_address"),
            AddressLine2 = AgentJson.String(json, "extended_address"),
            // locality and region aren't UCP's names, but some clients (the UCP conformance suite among them) send them.
            City = AgentJson.String(json, "address_locality") ?? AgentJson.String(json, "locality"),
            Region = AgentJson.String(json, "address_region") ?? AgentJson.String(json, "region"),
            PostalCode = AgentJson.String(json, "postal_code"),
            Country = AgentJson.String(json, "address_country")?.ToUpperInvariant(),
            Phone = AgentJson.String(json, "phone_number"),
        };
    }

    private static List<AgentCheckoutLineInput> ReadLines(JsonObject body)
    {
        if (body["line_items"] is not JsonArray items)
        {
            return null;
        }

        return items
            .OfType<JsonObject>()
            .Select(item => new AgentCheckoutLineInput
            {
                ItemId = AgentJson.String(item["item"], "id") ?? AgentJson.String(item, "id"),
                Quantity = AgentJson.Int(item, "quantity") ?? 1,
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
            FirstName = AgentJson.String(json, "first_name"),
            LastName = AgentJson.String(json, "last_name"),
            FullName = AgentJson.String(json, "full_name") ?? AgentJson.String(json, "name"),
            Email = AgentJson.String(json, "email"),
            Phone = AgentJson.String(json, "phone_number"),
        };
    }

    private static AgentFulfillmentInput ReadFulfillment(JsonNode node)
    {
        if (node?["methods"] is not JsonArray methods)
        {
            return null;
        }

        foreach (var method in methods.OfType<JsonObject>())
        {
            var type = AgentJson.String(method, "type");
            var selected = AgentJson.String(method, "selected_destination_id");

            if (type == "pickup" && !string.IsNullOrEmpty(selected))
            {
                return new AgentFulfillmentInput { PickupLocationId = selected };
            }

            if (type != "shipping")
            {
                continue;
            }

            var destinations = method["destinations"] as JsonArray;
            var destination = destinations?.OfType<JsonObject>().FirstOrDefault(entry => selected is not null && AgentJson.String(entry, "id") == selected)
                ?? destinations?.OfType<JsonObject>().FirstOrDefault();
            var option = (method["groups"] as JsonArray)?.OfType<JsonObject>().Select(group => AgentJson.String(group, "selected_option_id")).FirstOrDefault(id => !string.IsNullOrEmpty(id));

            return new AgentFulfillmentInput
            {
                Address = ReadAddress(destination),
                OptionId = option,
                DestinationId = AgentJson.String(destination, "id"),
            };
        }

        return null;
    }

    private static Dictionary<string, string> ReadSignals(JsonNode node)
    {
        var signals = new Dictionary<string, string>(StringComparer.Ordinal);

        if (node is JsonObject json)
        {
            foreach (var (name, value) in json)
            {
                if (value is JsonValue scalar)
                {
                    signals[name] = scalar.ToString();
                }
            }
        }

        return signals;
    }

    private static Dictionary<string, bool> ReadConsents(JsonNode buyer)
    {
        var consents = new Dictionary<string, bool>(StringComparer.Ordinal);

        if (buyer?["consent"] is JsonObject json)
        {
            foreach (var (purpose, value) in json)
            {
                var granted = value is JsonObject entry ? AgentJson.Bool(entry, "granted") : value is JsonValue scalar && scalar.TryGetValue<bool>(out var flag) ? flag : null;

                if (granted.HasValue)
                {
                    consents[purpose] = granted.Value;
                }
            }
        }

        return consents;
    }
}
