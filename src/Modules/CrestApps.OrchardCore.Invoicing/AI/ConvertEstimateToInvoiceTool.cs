using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Invoicing.AI;

/// <summary>
/// Turns an estimate into a draft invoice as the estimate's <b>Invoice</b> buttons do
/// (<see cref="IEstimateCommandService.ConvertAsync"/>: the whole estimate, or its deposit as a prepayment invoice) and as
/// its <b>Invoice part</b> form does (<see cref="IEstimateCommandService.InvoiceProgressAsync"/>: a percent of each line).
/// Like the page, it needs both <see cref="InvoicingPermissions.ManageEstimates"/> and
/// <see cref="InvoicingPermissions.ManageInvoices"/>. The invoice is a draft: nothing is numbered, posted, sent or charged.
/// </summary>
internal sealed class ConvertEstimateToInvoiceTool : InvoiceAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "convertEstimateToInvoice";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "estimate": {
          "type": "string",
          "description": "The estimate's number, such as EST-1001, or its id."
        },
        "conversion": {
          "type": "string",
          "enum": ["full", "deposit", "progress"],
          "description": "What to invoice: the whole estimate, its deposit as a prepayment invoice, or a percent of its lines (progress invoicing)."
        },
        "percent": {
          "type": "number",
          "description": "With progress: the percent of every line with an amount to invoice now, more than 0 and at most 100."
        },
        "lines": {
          "type": "array",
          "description": "With progress, instead of percent: the percent of each chosen line to invoice now. Line ids come from getRecord with kind estimate.",
          "items": {
            "type": "object",
            "properties": {
              "lineId": { "type": "string", "description": "The estimate line's id." },
              "percent": { "type": "number", "description": "The percent of that line to invoice now, more than 0 and at most 100." }
            },
            "required": ["lineId", "percent"],
            "additionalProperties": false
          }
        }
      },
      "required": ["estimate", "conversion"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Turns a sent or accepted estimate into a draft invoice: the whole estimate, its deposit as a prepayment invoice, or a percent of its lines (progress invoicing). Converting the whole estimate again returns the invoice made the first time. The invoice stays a draft for staff to check and issue.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        // As the estimate's page: both permissions.
        if (!await IsAuthorizedAsync(services, caller, InvoicingPermissions.ManageEstimates) ||
            !await IsAuthorizedAsync(services, caller, InvoicingPermissions.ManageInvoices))
        {
            return NotAllowedMessage;
        }

        var estimate = await EstimateToolSupport.FindAsync(services, Read(arguments, "estimate"), cancellationToken);

        if (estimate is null)
        {
            return "Nothing was invoiced: no estimate has that number or id.";
        }

        var conversion = Read(arguments, "conversion")?.ToLowerInvariant();
        Dictionary<string, decimal> percents = null;

        if (conversion is not ("full" or "deposit" or "progress"))
        {
            return "Nothing was invoiced: conversion is full, deposit or progress.";
        }

        if (conversion == "progress")
        {
            var (read, error) = ReadPercents(arguments, estimate);

            if (error is not null)
            {
                return error;
            }

            percents = read;
        }

        // The estimate commands discard the session's unsaved work when they refuse, so earlier work is written first.
        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlier)
        {
            return earlier;
        }

        var commands = services.GetRequiredService<IEstimateCommandService>();
        EstimateCommandResult result;

        try
        {
            result = conversion switch
            {
                "full" => await commands.ConvertAsync(estimate.ItemId, EstimateConversion.Full, cancellationToken),
                "deposit" => await commands.ConvertAsync(estimate.ItemId, EstimateConversion.Deposit, cancellationToken),
                _ => await commands.InvoiceProgressAsync(estimate.ItemId, percents, cancellationToken),
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MarkSessionClosed(services);

            return $"Invoicing estimate {estimate.Number ?? estimate.ItemId} stopped with an error. Check its page before trying again.";
        }

        if (!result.Succeeded || result.Invoice is null)
        {
            MarkSessionClosed(services);

            return Json(new
            {
                estimateId = estimate.ItemId,
                estimateNumber = estimate.Number,
                errors = result.Errors,
                message = "Nothing was invoiced; read the errors.",
            });
        }

        var invoice = result.Invoice;

        return Json(new
        {
            estimateId = estimate.ItemId,
            estimateNumber = estimate.Number,
            estimateStatus = ToCamel((result.Estimate ?? estimate).Status.ToString()),
            alreadyDone = result.AlreadyDone,
            invoice = new
            {
                id = invoice.ItemId,
                kind = ToCamel(invoice.Kind.ToString()),
                status = ToCamel(invoice.Status.ToString()),
                currency = invoice.Currency,
                total = invoice.Totals?.Total ?? 0m,
                adminUrl = InvoiceAdminPath(services, invoice.ItemId),
            },
            message = result.AlreadyDone
                ? $"Estimate {estimate.Number} was invoiced already: this is the invoice made the first time."
                : $"A draft invoice of {Format(invoice.Totals?.Total ?? 0m, invoice.Currency)} was made from estimate {estimate.Number}. It is a draft: staff check it and issue it.",
        });
    }

    private static (Dictionary<string, decimal> Percents, string Error) ReadPercents(AIFunctionArguments arguments, Estimate estimate)
    {
        if (!TryReadDecimal(arguments, "percent", out var percent))
        {
            return (null, "Nothing was invoiced: percent must be a number.");
        }

        var lines = InvoicingToolArguments.ReadArray(arguments, "lines");
        var hasPercent = percent is not null;
        var hasLines = lines is { Count: > 0 };

        if (hasPercent == hasLines)
        {
            return (null, "Nothing was invoiced: with progress, give either percent (for every line) or lines (a percent for each chosen line), not both.");
        }

        var percents = new Dictionary<string, decimal>(StringComparer.Ordinal);

        if (percent is { } every)
        {
            if (every is <= 0m or > 100m)
            {
                return (null, "Nothing was invoiced: percent must be more than 0 and at most 100.");
            }

            foreach (var line in estimate.Lines.Where(line => line.HasAmount && !string.IsNullOrEmpty(line.Id)))
            {
                percents[line.Id] = every;
            }

            return (percents, null);
        }

        var ids = estimate.Lines.Where(line => !string.IsNullOrEmpty(line.Id)).Select(line => line.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var item in lines)
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("lineId", out var lineId) || lineId.ValueKind != JsonValueKind.String ||
                !item.TryGetProperty("percent", out var value) || !InvoicingToolArguments.TryGetDecimal(value, out var linePercent))
            {
                return (null, "Nothing was invoiced: every line needs a lineId and a percent.");
            }

            var id = lineId.GetString();

            if (!ids.Contains(id))
            {
                return (null, $"Nothing was invoiced: estimate {estimate.Number} has no line '{id}'. Read its lines with getRecord, kind estimate.");
            }

            if (linePercent is <= 0m or > 100m)
            {
                return (null, "Nothing was invoiced: each line's percent must be more than 0 and at most 100.");
            }

            percents[id] = linePercent;
        }

        return (percents, null);
    }
}
