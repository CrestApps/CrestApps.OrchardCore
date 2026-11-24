using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Invoicing.ViewModels;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Invoicing.AI;

/// <summary>
/// Drafts an estimate as <b>Commerce › Estimates › New</b> saves one: the form the page posts is filled from the arguments
/// and written onto a new estimate by the page's own editor (<see cref="EstimateDraftEditor"/>), with the page's
/// defaults and checks, then worked out by the calculator, validated by the estimate's rules and stored as a draft. It is
/// never sent: <c>sendEstimate</c> does that.
/// </summary>
internal sealed class DraftEstimateTool : InvoiceAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "draftEstimate";

    private const int MaxLines = 100;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "customer": {
          "type": "string",
          "description": "Who the estimate is for, as a party such as User:42 or Account:7, when known."
        },
        "customerName": {
          "type": "string",
          "description": "The customer's name, printed on the estimate."
        },
        "customerEmail": {
          "type": "string",
          "description": "The e-mail address the estimate is sent to."
        },
        "currency": {
          "type": "string",
          "description": "A three-letter currency code such as USD; the store's default when left out."
        },
        "reference": {
          "type": "string",
          "description": "A reference printed on the estimate."
        },
        "expiryDate": {
          "type": "string",
          "description": "The last day the customer can accept it, as yyyy-MM-dd. Left out, it is set when the estimate is sent, from the invoicing settings."
        },
        "depositPercent": {
          "type": "number",
          "description": "The percent of the total asked as a deposit, from 0 to 100."
        },
        "depositAmount": {
          "type": "number",
          "description": "The amount asked as a deposit, used instead of depositPercent."
        },
        "onAcceptance": {
          "type": "string",
          "enum": ["none", "full", "deposit"],
          "description": "What accepting the estimate invoices at once: nothing, the whole estimate, or its deposit. none when left out."
        },
        "lines": {
          "type": "array",
          "description": "The lines of the estimate.",
          "items": {
            "type": "object",
            "properties": {
              "description": { "type": "string", "description": "What is offered." },
              "quantity": { "type": "number", "description": "How many; 1 when left out." },
              "unitPrice": { "type": "number", "description": "The price of one, in the estimate's currency." }
            },
            "required": ["description", "unitPrice"],
            "additionalProperties": false
          }
        }
      },
      "required": ["lines"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Drafts an estimate (a quote) for a customer (a party such as User:42, or a name and e-mail) from lines of description, quantity and unit price, with an optional expiry date, deposit and what accepting it invoices. It stays a draft for staff to check; sendEstimate sends it. Returns its id, total and admin link.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, caller, InvoicingPermissions.ManageEstimates))
        {
            return NotAllowedMessage;
        }

        var (lines, linesError) = ReadLines(arguments);

        if (linesError is not null)
        {
            return linesError;
        }

        if (!TryReadDate(arguments, "expiryDate", out var expiryDate))
        {
            return "Nothing was drafted: give expiryDate as yyyy-MM-dd, such as 2026-12-31.";
        }

        if (!TryReadDecimal(arguments, "depositPercent", out var depositPercent) || !TryReadDecimal(arguments, "depositAmount", out var depositAmount))
        {
            return "Nothing was drafted: depositPercent and depositAmount must be numbers.";
        }

        var onAcceptance = EstimateConversion.None;

        if (Read(arguments, "onAcceptance") is { } conversion &&
            (!Enum.TryParse(conversion, ignoreCase: true, out onAcceptance) || !Enum.IsDefined(onAcceptance) || int.TryParse(conversion, out _)))
        {
            return "Nothing was drafted: onAcceptance is none, full or deposit.";
        }

        var customer = Read(arguments, "customer");
        var party = default(PartyRef);

        if (customer is not null)
        {
            if (!PartyRef.TryParse(customer, out party) || party.IsEmpty)
            {
                return $"Nothing was drafted: '{customer}' is not a party. Give it as Kind:Id, such as User:42 or Account:7, or give customerName and customerEmail instead.";
            }

            // The page's picker only offers parties that exist.
            if (services.GetService<IPartyDirectory>() is { } parties && await parties.GetNameAsync(party, cancellationToken) is null)
            {
                return $"Nothing was drafted: no customer is {customer}.";
            }
        }

        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlier)
        {
            return earlier;
        }

        var settings = await services.GetRequiredService<ISiteService>().GetSettingsAsync<InvoicingSettings>();
        var estimates = services.GetRequiredService<IEstimateManager>();
        var estimate = await estimates.NewAsync(cancellationToken: cancellationToken);

        // The form the New estimate page posts, starting from the defaults the page opens with.
        var model = new EstimateEditViewModel
        {
            BillTo = party.IsEmpty ? null : party.ToString(),
            ContactName = Read(arguments, "customerName"),
            ContactEmail = Read(arguments, "customerEmail"),
            Currency = Read(arguments, "currency") ?? settings.DefaultCurrency,
            PricesIncludeTax = settings.PricesIncludeTax,
            Reference = Read(arguments, "reference"),
            ExpiryDate = expiryDate,
            DepositPercent = depositPercent,
            DepositAmount = depositAmount,
            OnAcceptance = onAcceptance,
            Lines = [.. lines.Select(line => new InvoiceLineEditViewModel
            {
                Kind = DocumentLineKind.Item,
                Name = line.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
            })],
        };

        EstimateDraftEditor.Apply(model, estimate);

        // The page's own checks of the form.
        if (!string.IsNullOrEmpty(estimate.Currency) && estimate.Currency.Length != 3)
        {
            return "Nothing was drafted: the currency must be a three-letter code, such as USD.";
        }

        await InvoiceCalculation.ApplyAsync(services.GetRequiredService<IDocumentCalculator>(), estimate, settings, issueDate: null, cancellationToken);

        var validation = await estimates.ValidateAsync(estimate, cancellationToken);

        if (!validation.Succeeded)
        {
            return "Nothing was drafted: " + string.Join(" ", validation.Errors.Select(error => error.ErrorMessage));
        }

        await estimates.CreateAsync(estimate, cancellationToken);

        // Committed at once, so the id the answer gives is real and a send in the same message reads the draft, as the
        // New estimate page saves it before sending.
        if (!await SaveAsync(services, cancellationToken))
        {
            return "Nothing was drafted: the estimate could not be saved because something changed at the same moment. Ask again in your next message.";
        }

        return Json(new
        {
            id = estimate.ItemId,
            status = ToCamel(estimate.Status.ToString()),
            customer = estimate.BillTo?.Contact?.Name,
            party = party.IsEmpty ? null : party.ToString(),
            currency = estimate.Currency,
            total = estimate.Totals?.Total ?? 0m,
            expiryDate = Date(estimate.ExpiryDate),
            depositPercent = estimate.DepositPercent,
            depositAmount = estimate.DepositAmount,
            onAcceptance = ToCamel(estimate.OnAcceptance.ToString()),
            adminUrl = EstimateToolSupport.AdminPath(services, "EstimatesAdmin", estimate.ItemId),
            message = $"Estimate drafted for {Format(estimate.Totals?.Total ?? 0m, estimate.Currency)}. It is a draft: nothing was sent. Check it, then send it with sendEstimate or from its page.",
        });
    }

    private static (List<(string Description, decimal Quantity, decimal UnitPrice)> Lines, string Error) ReadLines(AIFunctionArguments arguments)
    {
        var items = InvoicingToolArguments.ReadArray(arguments, "lines");

        if (items is null || items.Count == 0)
        {
            return (null, "Nothing was drafted: give at least one line with a description and a unit price.");
        }

        if (items.Count > MaxLines)
        {
            return (null, $"Nothing was drafted: an estimate takes at most {MaxLines} lines from a chat.");
        }

        var lines = new List<(string, decimal, decimal)>();

        foreach (var item in items)
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("description", out var description) || description.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(description.GetString()) ||
                !item.TryGetProperty("unitPrice", out var price) || !InvoicingToolArguments.TryGetDecimal(price, out var unitPrice))
            {
                return (null, "Nothing was drafted: every line needs a description and a unit price.");
            }

            var quantity = 1m;

            if (item.TryGetProperty("quantity", out var quantityValue) && quantityValue.ValueKind != JsonValueKind.Null)
            {
                if (!InvoicingToolArguments.TryGetDecimal(quantityValue, out quantity) || quantity <= 0m)
                {
                    return (null, $"Nothing was drafted: the quantity of \"{description.GetString().Trim()}\" must be more than zero.");
                }
            }

            lines.Add((description.GetString().Trim(), quantity, unitPrice));
        }

        return (lines, null);
    }
}
