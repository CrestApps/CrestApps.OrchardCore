using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using YesSql;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Orders.AI;

/// <summary>
/// Creates or changes an order the store puts together for a buyer, or answers a quote request, as the draft order
/// editor of Commerce › Orders does (<see cref="DraftOrderService.PriceAsync(DraftOrderInput, string, string, Order, CancellationToken)"/>
/// and <see cref="DraftOrderService.SaveAsync"/>, with the editor's limits, <see cref="DraftOrderInputRules"/>): priced for
/// the buyer, held from stock when saved, and committed at once, as the editor does, so stock someone else changed at the
/// same moment is reported. Stricter than the editor: a quantity or quoted price out of range is refused instead of being
/// clamped or dropped.
/// </summary>
internal sealed class SaveDraftOrderTool : OrdersAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveDraftOrder";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "orderId": {
          "type": "string",
          "description": "The draft order or quote request to change, from listOrders. Leave it out to create one."
        },
        "customerEmail": {
          "type": "string",
          "description": "The buyer's email; the invoice goes there. Required for a new order."
        },
        "customerName": {
          "type": "string",
          "description": "The buyer's name."
        },
        "lines": {
          "type": "array",
          "description": "The order's products. When given, they replace the order's lines; keep a line's lineId (from getOrder) to keep it. Leave it out to keep the lines.",
          "items": {
            "type": "object",
            "properties": {
              "lineId": {
                "type": "string",
                "description": "The line to keep, from getOrder; left out for a new line."
              },
              "productId": {
                "type": "string",
                "description": "The product's content item id, from listProducts."
              },
              "variantId": {
                "type": "string",
                "description": "The variant, for a product sold in variants."
              },
              "quantity": {
                "type": "integer",
                "description": "How many, 1 to 10,000."
              },
              "unitPrice": {
                "type": "number",
                "description": "A quoted price for each unit instead of the catalog's, zero or more."
              }
            },
            "required": ["productId", "quantity"],
            "additionalProperties": false
          }
        },
        "note": {
          "type": "string",
          "description": "A note for the buyer on the invoice, or 'none' to clear it."
        },
        "payByDate": {
          "type": "string",
          "description": "The date to pay by, yyyy-MM-dd, or 'none' to clear it. A quote is offered until then."
        },
        "addsToOrderId": {
          "type": "string",
          "description": "For a new order: the paid order these items are added to, for the same buyer."
        },
        "priceOnly": {
          "type": "boolean",
          "description": "Price the lines for the buyer without saving anything."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates or changes an order the store puts together for a buyer, or answers a quote request: buyer email and name, products with variant, quantity and an optional quoted price, a note and a pay-by date. It returns the priced lines and any stock problems, holds the items from stock when saved, can price without saving, and can start a new draft that adds items to a paid order.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        Order order = null;

        if (ReadString(arguments, "orderId") is not null)
        {
            (order, var findError) = await FindOrderAsync(services, arguments, cancellationToken);

            if (order is null)
            {
                return findError;
            }

            if (order.Invoice is null || order.Status != OrderStatus.Draft)
            {
                return $"Nothing was changed: order {order.OrderNumber} is not a draft the store put together, or the buyer has started paying it, so it can't be changed here.";
            }

            if (ReadString(arguments, "addsToOrderId") is not null)
            {
                return "Nothing was changed: addsToOrderId is set when a draft is created.";
            }
        }

        var (lines, linesError) = ReadLines(arguments, order);

        if (linesError is not null)
        {
            return linesError;
        }

        var dueText = ReadString(arguments, "payByDate");
        var due = order?.Invoice?.DueUtc;

        if (dueText is not null)
        {
            if (string.Equals(dueText, "none", StringComparison.OrdinalIgnoreCase))
            {
                due = null;
            }
            else if (DateTime.TryParseExact(dueText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                due = DraftOrderInputRules.ToDueUtc(date);
            }
            else
            {
                return "Nothing was saved: write payByDate as yyyy-MM-dd, or 'none' to clear it.";
            }
        }

        var note = ReadString(arguments, "note");
        var input = new DraftOrderInput
        {
            CustomerEmail = ReadString(arguments, "customerEmail") ?? order?.CustomerEmail,
            CustomerName = ReadString(arguments, "customerName") ?? order?.CustomerName,
            Note = note is null ? order?.Invoice?.Note : string.Equals(note, "none", StringComparison.OrdinalIgnoreCase) ? null : note,
            DueUtc = due,
            AddsToOrderId = order is null ? ReadString(arguments, "addsToOrderId") : order.Invoice.AddsToOrderId,
            Lines = lines,
        };

        var drafts = services.GetRequiredService<DraftOrderService>();

        if (ReadBool(arguments, "priceOnly") == true)
        {
            var pricing = await drafts.PriceAsync(input, order?.CartId, order?.Currency, order, cancellationToken);
            var priced = pricing.Lines.Select(line =>
            {
                var quoted = input.Lines.FirstOrDefault(entry => entry.LineId == line.Line.LineId)?.UnitPrice;

                return new
                {
                    lineId = line.Line.LineId,
                    productId = line.Line.ContentItemId,
                    variantId = line.Line.VariantId,
                    title = line.Title,
                    quantity = line.Line.Quantity,
                    catalogUnitPrice = line.UnitPrice,
                    quotedUnitPrice = quoted,
                    lineTotal = quoted is { } price ? price * line.Line.Quantity : line.LineTotal,
                    problem = line.Problem?.Value,
                };
            }).ToArray();

            return Json(new
            {
                priced = true,
                saved = false,
                currency = pricing.Currency,
                lines = priced,
                subtotal = priced.Sum(line => line.lineTotal),
                message = "Priced for the buyer; nothing was saved. Shipping and tax are added when the buyer pays.",
            });
        }

        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlierError)
        {
            return earlierError;
        }

        var session = services.GetRequiredService<YesSqlSession>();
        var (saved, problems) = await drafts.SaveAsync(order, input, GetActor(caller), cancellationToken);

        if (problems.Count > 0)
        {
            // As the editor: what the save began, such as stock held for new lines, is discarded with it.
            await session.CancelAsync();
            MarkClosed(services);

            return $"Nothing was saved: {string.Join(" ", problems)}";
        }

        try
        {
            await session.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            await session.CancelAsync();
            MarkClosed(services);

            return "Nothing was saved: the stock changed at the same moment. Try again in your next message.";
        }

        return Json(new
        {
            orderId = saved.ItemId,
            number = saved.OrderNumber,
            status = "draft",
            customer = saved.CustomerName,
            customerEmail = saved.CustomerEmail,
            currency = saved.Currency,
            lines = saved.Lines.Select(line => new
            {
                lineId = line.LineId,
                productId = line.ContentItemId,
                title = line.Title,
                variant = line.VariantTitle,
                quantity = line.Quantity,
                unitPrice = line.UnitPrice,
                quotedUnitPrice = line.QuotedUnitPrice,
                lineTotal = line.LineTotal,
            }).ToArray(),
            subtotal = saved.Subtotal,
            payBy = saved.Invoice?.DueUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            addsToOrderNumber = saved.Invoice?.AddsToOrderNumber,
            adminPage = GetAdminPath(services, "OrdersDetail", new { orderId = saved.ItemId }),
            message = order is null
                ? $"Order {saved.OrderNumber} saved as a draft, with its items held from stock. Send the invoice with sendOrderInvoice when it's ready."
                : $"Order {saved.OrderNumber} saved.",
        });
    }

    // The lines given, in the editor's limits, or the order's own lines when none are given.
    private static (List<DraftOrderLineInput> Lines, string Error) ReadLines(AIFunctionArguments arguments, Order order)
    {
        if (ReadJson(arguments, "lines") is not { } element)
        {
            return ((order?.Lines ?? [])
                .Select(line => new DraftOrderLineInput(line.LineId, line.ContentItemId, line.VariantId, line.Quantity, line.QuotedUnitPrice))
                .ToList(), null);
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return (null, "Nothing was saved: lines must be a list of products.");
        }

        var lines = new List<DraftOrderLineInput>();

        foreach (var entry in element.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object ||
                !entry.TryGetProperty("productId", out var productElement) ||
                productElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(productElement.GetString()))
            {
                return (null, "Nothing was saved: each line needs a productId.");
            }

            if (!entry.TryGetProperty("quantity", out var quantityElement) ||
                quantityElement.ValueKind != JsonValueKind.Number ||
                !quantityElement.TryGetInt32(out var quantity) ||
                quantity is < 1 or > DraftOrderInputRules.MaxQuantity)
            {
                return (null, $"Nothing was saved: each line needs a quantity from 1 to {DraftOrderInputRules.MaxQuantity}.");
            }

            decimal? unitPrice = null;

            if (entry.TryGetProperty("unitPrice", out var priceElement) && priceElement.ValueKind != JsonValueKind.Null)
            {
                if (priceElement.ValueKind != JsonValueKind.Number || !priceElement.TryGetDecimal(out var price) || !DraftOrderInputRules.IsAcceptedPrice(price))
                {
                    return (null, "Nothing was saved: a quoted unit price is a number from zero to below 100,000,000.");
                }

                unitPrice = price;
            }

            var lineId = entry.TryGetProperty("lineId", out var lineElement) && lineElement.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(lineElement.GetString())
                ? lineElement.GetString().Trim()
                : null;

            if (lineId is not null && order?.Lines.Any(line => line.LineId == lineId) != true)
            {
                return (null, $"Nothing was saved: the order has no line '{lineId}'. Use getOrder for its line ids, or leave lineId out for a new line.");
            }

            var variantId = entry.TryGetProperty("variantId", out var variantElement) && variantElement.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(variantElement.GetString())
                ? variantElement.GetString().Trim()
                : null;

            lines.Add(new DraftOrderLineInput(lineId ?? UniqueId.GenerateId(), productElement.GetString().Trim(), variantId, quantity, unitPrice));
        }

        if (lines.Count > DraftOrderInputRules.MaxLines)
        {
            return (null, $"Nothing was saved: a draft order has at most {DraftOrderInputRules.MaxLines} lines.");
        }

        return (lines, null);
    }

    private static void MarkClosed(IServiceProvider services)
    {
        if (services.GetService<OrdersToolScope>() is { } scope)
        {
            scope.SessionClosed = true;
        }
    }
}
