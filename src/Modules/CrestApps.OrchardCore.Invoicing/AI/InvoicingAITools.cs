using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Reports;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Invoicing.AI;

/// <summary>
/// Registers the invoicing AI tools, which administrators can pick on AI profiles: find invoices, summarize what
/// customers owe, draft an invoice and send a reminder; and read, issue, approve, void, write off and record payments on
/// an invoice. Each acts as the signed-in user, with their permissions.
/// </summary>
[RequireFeatures(AIConstants.Feature.Area)]
public sealed class InvoicingAIToolsStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public InvoicingAIToolsStartup(IStringLocalizer<InvoicingAIToolsStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAITool<FindInvoicesTool>(FindInvoicesTool.ToolName).WithTitle(S["Find invoices"]).WithDescription(S["Finds invoices by status, customer or number, with what each still owes."]).WithCategory(S["Invoicing"]).Selectable();
        services.AddCoreAITool<SummarizeReceivablesTool>(SummarizeReceivablesTool.ToolName).WithTitle(S["Summarize receivables"]).WithDescription(S["Sums what customers owe on open invoices, by currency and how late it is, with the customers who owe most."]).WithCategory(S["Invoicing"]).Selectable();
        services.AddCoreAITool<DraftInvoiceTool>(DraftInvoiceTool.ToolName).WithTitle(S["Draft an invoice"]).WithDescription(S["Drafts an invoice for a customer from lines; staff check and issue it."]).WithCategory(S["Invoicing"]).Selectable();
        services.AddCoreAITool<SendInvoiceReminderTool>(SendInvoiceReminderTool.ToolName).WithTitle(S["Send an invoice reminder"]).WithDescription(S["E-mails an open invoice again to its customer with its pay link."]).WithCategory(S["Invoicing"]).Selectable();

        // The invoice's life cycle: read one invoice, issue, approve, and void, write off or record a payment after a
        // preview whose token the action requires (the owner's review, F23).
        services.TryAddScoped<InvoicingToolScope>();

        services.AddCoreAITool<GetInvoiceTool>(GetInvoiceTool.TheName)
            .WithTitle(S["Get an invoice"])
            .WithDescription(S["Gets one invoice in full: lines, totals, what is paid, credited and due, history, late-fee debit notes, credit notes and what you may do with it now."])
            .WithCategory(S["Invoicing"])
            .Selectable();

        services.AddCoreAITool<IssueInvoiceTool>(IssueInvoiceTool.TheName)
            .WithTitle(S["Issue an invoice"])
            .WithDescription(S["Issues a draft invoice: numbers it and posts what it totals to the ledger as owed, optionally e-mailing it to the customer, or submits it for approval when its total needs approval."])
            .WithCategory(S["Invoicing"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<DecideInvoiceApprovalTool>(DecideInvoiceApprovalTool.TheName)
            .WithTitle(S["Approve or reject an invoice"])
            .WithDescription(S["For invoices waiting for approval only (findInvoices with status pendingApproval): approves one, which issues it, or rejects it with a reason, which makes it a draft again. Other approval requests use decideApprovalRequest."])
            .WithCategory(S["Invoicing"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<PreviewInvoiceActionTool>(PreviewInvoiceActionTool.TheName)
            .WithTitle(S["Preview an invoice void, write-off or payment"])
            .WithDescription(S["Shows what voiding, writing off or recording a payment on an invoice would do, without changing anything, and returns the previewToken the action requires."])
            .WithCategory(S["Invoicing"])
            .Selectable();

        services.AddCoreAITool<VoidInvoiceTool>(VoidInvoiceTool.TheName)
            .WithTitle(S["Void an invoice"])
            .WithDescription(S["Voids an open invoice nothing was paid or credited on, or waives a late fee by voiding its debit note. Needs the previewToken from previewInvoiceAction."])
            .WithCategory(S["Invoicing"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<WriteOffInvoiceTool>(WriteOffInvoiceTool.TheName)
            .WithTitle(S["Write off an invoice"])
            .WithDescription(S["Writes off everything still owed on an open invoice as uncollectible, with a reason. Needs the previewToken from previewInvoiceAction."])
            .WithCategory(S["Invoicing"])
            .RequireApproval()
            .Selectable();

        services.AddCoreAITool<RecordInvoicePaymentTool>(RecordInvoicePaymentTool.TheName)
            .WithTitle(S["Record an invoice payment"])
            .WithDescription(S["Records money received outside the site against an open invoice, up to what is still owed, and when it was received. Needs the previewToken from previewInvoiceAction and a new idempotency key."])
            .WithCategory(S["Invoicing"])
            .RequireApproval()
            .Selectable();
    }
}

/// <summary>
/// The base of the invoicing AI tools: reading arguments, checking the caller's permissions and writing invoices.
/// </summary>
public abstract class InvoicingToolBase : AIFunction
{
    protected const string NotAllowedMessage = "The signed-in user is not allowed to do this, or no one is signed in.";

    protected static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object> { ["Strict"] = false };

    protected static string Read(AIFunctionArguments arguments, string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        var text = value is JsonElement element ? (element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString()) : value.ToString();

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    protected static JsonElement Schema(string schema)
        => JsonSerializer.Deserialize<JsonElement>(schema);

    protected static async Task<bool> IsAllowedAsync(IServiceProvider services, Permission permission)
    {
        var user = services.GetService<IHttpContextAccessor>()?.HttpContext?.User;

        return user?.Identity?.IsAuthenticated == true && await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, permission);
    }

    protected static async Task<DateOnly> TodayAsync(IServiceProvider services)
        => DateOnly.FromDateTime((await services.GetRequiredService<ILocalClock>().GetLocalNowAsync()).DateTime);

    protected static JsonObject Describe(Invoice invoice, DateOnly today)
        => new()
        {
            ["id"] = invoice.ItemId,
            ["number"] = invoice.Number,
            ["customer"] = invoice.BillTo?.Contact?.Name,
            ["status"] = invoice.Status.ToString(),
            ["settlement"] = invoice.Settlement.ToString(),
            ["currency"] = invoice.Currency,
            ["total"] = invoice.Totals?.Total ?? 0m,
            ["amountDue"] = invoice.AmountDue,
            ["issueDate"] = invoice.IssueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["dueDate"] = invoice.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["overdue"] = invoice.Status == InvoiceStatus.Open && invoice.AmountDue > 0m && invoice.DueDate is { } due && due < today,
        };

    protected static string AdminLink(IServiceProvider services, string invoiceId)
    {
        var httpContext = services.GetService<IHttpContextAccessor>()?.HttpContext;

        return httpContext is null || services.GetService<LinkGenerator>() is not { } links
            ? null
            : links.GetPathByAction(httpContext, "Detail", "InvoicesAdmin", new { area = InvoicingConstants.Features.Area, itemId = invoiceId });
    }
}

/// <summary>
/// Finds invoices by status, customer name or number.
/// </summary>
public sealed class FindInvoicesTool : InvoicingToolBase
{
    public const string ToolName = "findInvoices";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "status": { "type": "string", "enum": ["draft", "open", "overdue", "paid", "void", "uncollectible", "pendingApproval"], "description": "Only invoices in this state." },
            "search": { "type": "string", "description": "Part of the invoice number, the customer's name or the reference." },
            "take": { "type": "integer", "description": "How many to return, 1 to 25; 10 when not given." }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => ToolName;

    public override string Description => "Finds the store's invoices by state (draft, open, overdue, paid, void, uncollectible, pendingApproval), by part of the number, customer name or reference, newest first: each with its number, customer, total, amount still due, dates and whether it is overdue.";

    public override JsonElement JsonSchema => _schema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var services = arguments.Services;

        if (!await IsAllowedAsync(services, InvoicingPermissions.ViewInvoices))
        {
            return NotAllowedMessage;
        }

        var today = await TodayAsync(services);
        var query = new InvoiceQuery { Search = Read(arguments, "search") };

        switch (Read(arguments, "status")?.ToLowerInvariant())
        {
            case "draft": query.Status = InvoiceStatus.Draft; break;
            case "open": query.Status = InvoiceStatus.Open; break;
            case "overdue": query.Status = InvoiceStatus.Open; query.OverdueOn = today; break;
            case "paid": query.Settlement = InvoiceSettlement.Paid; query.IssuedOnly = true; break;
            case "void": query.Status = InvoiceStatus.Void; break;
            case "uncollectible": query.Status = InvoiceStatus.Uncollectible; break;
            case "pendingapproval": query.Status = InvoiceStatus.PendingApproval; break;
        }

        var take = int.TryParse(Read(arguments, "take"), out var count) ? Math.Clamp(count, 1, 25) : 10;
        var page = await services.GetRequiredService<IInvoiceManager>().PageAsync(1, take, query, cancellationToken);
        var result = new JsonObject
        {
            ["count"] = page.Count,
            ["invoices"] = new JsonArray([.. page.Entries.Select(invoice => (JsonNode)Describe(invoice, today))]),
        };

        return result.ToJsonString(JsonOptions);
    }
}

/// <summary>
/// Sums what customers owe on open invoices.
/// </summary>
public sealed class SummarizeReceivablesTool : InvoicingToolBase
{
    public const string ToolName = "summarizeReceivables";

    private const int MostOpenInvoices = 1000;

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {},
          "additionalProperties": false
        }
        """);

    public override string Name => ToolName;

    public override string Description => "Sums what customers still owe on open invoices, per currency (amounts in different currencies are never added): the total, how much is overdue, aging by days past due (not due, 1-30, 31-60, 61-90, over 90) and the five customers who owe most.";

    public override JsonElement JsonSchema => _schema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var services = arguments.Services;

        if (!await IsAllowedAsync(services, InvoicingPermissions.ViewInvoices))
        {
            return NotAllowedMessage;
        }

        var today = await TodayAsync(services);
        var page = await services.GetRequiredService<IInvoiceManager>().PageAsync(1, MostOpenInvoices, new InvoiceQuery { Status = InvoiceStatus.Open, IssuedOnly = true }, cancellationToken);
        var rows = InvoicingReportMath.Aging(page.Entries, today);
        var currencies = new JsonArray();

        foreach (var group in rows.GroupBy(row => row.Currency))
        {
            var buckets = new decimal[InvoicingReportMath.BucketCount];

            foreach (var row in group)
            {
                for (var i = 0; i < buckets.Length; i++)
                {
                    buckets[i] += row.Buckets[i];
                }
            }

            currencies.Add(new JsonObject
            {
                ["currency"] = group.Key,
                ["owed"] = buckets.Sum(),
                ["overdue"] = buckets.Skip(1).Sum(),
                ["invoices"] = group.Sum(row => row.Invoices),
                ["aging"] = new JsonObject
                {
                    ["notDue"] = buckets[0],
                    ["days1To30"] = buckets[1],
                    ["days31To60"] = buckets[2],
                    ["days61To90"] = buckets[3],
                    ["over90"] = buckets[4],
                },
                ["topCustomers"] = new JsonArray([.. group.OrderByDescending(row => row.Total).Take(5).Select(row => (JsonNode)new JsonObject
                {
                    ["customer"] = row.Customer,
                    ["owed"] = row.Total,
                    ["overdue"] = row.Buckets.Skip(1).Sum(),
                })]),
            });
        }

        return new JsonObject
        {
            ["asOf"] = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["openInvoices"] = page.Count,
            ["partial"] = page.Count > MostOpenInvoices,
            ["currencies"] = currencies,
        }.ToJsonString(JsonOptions);
    }
}

/// <summary>
/// Drafts an invoice from a customer and lines. It never issues: staff check and issue the draft.
/// </summary>
public sealed class DraftInvoiceTool : InvoicingToolBase
{
    public const string ToolName = "draftInvoice";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "customer": { "type": "string", "description": "The customer as a party, such as User:42 or Account:7, when known." },
            "customerName": { "type": "string", "description": "The customer's name, when no party is given." },
            "customerEmail": { "type": "string", "description": "The customer's billing e-mail, when no party is given." },
            "currency": { "type": "string", "description": "A three-letter currency code; the store's default when not given." },
            "reference": { "type": "string", "description": "A reference printed on the invoice." },
            "lines": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "description": { "type": "string" },
                  "quantity": { "type": "number" },
                  "unitPrice": { "type": "number" }
                },
                "required": ["description", "unitPrice"]
              }
            }
          },
          "required": ["lines"],
          "additionalProperties": false
        }
        """);

    public override string Name => ToolName;

    public override string Description => "Drafts an invoice for a customer (a party such as User:42, or a name and e-mail) from lines of description, quantity and unit price. It is left as a draft for staff to check and issue, and the tool returns its id, total and the admin link.";

    public override JsonElement JsonSchema => _schema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var services = arguments.Services;

        if (!await IsAllowedAsync(services, InvoicingPermissions.ManageInvoices))
        {
            return NotAllowedMessage;
        }

        var lines = ReadLines(arguments);

        if (lines.Count == 0)
        {
            return "Give at least one line with a description and a unit price.";
        }

        var invoices = services.GetRequiredService<IInvoiceManager>();
        var settings = await services.GetRequiredService<ISiteService>().GetSettingsAsync<InvoicingSettings>();
        var invoice = await invoices.NewAsync(cancellationToken: cancellationToken);
        var party = PartyRef.TryParse(Read(arguments, "customer"), out var parsed) ? parsed : default;
        var name = Read(arguments, "customerName");
        var email = Read(arguments, "customerEmail");

        invoice.BillTo = new DocumentParty
        {
            Party = party,
            Contact = name is null && email is null ? invoice.BillTo?.Contact : new ContactInfo { Name = name, Email = email },
        };
        invoice.Currency = Read(arguments, "currency")?.ToUpperInvariant() ?? invoice.Currency ?? settings.DefaultCurrency;
        invoice.Reference = Read(arguments, "reference");

        foreach (var (description, quantity, unitPrice) in lines)
        {
            invoice.Lines.Add(new DocumentLine
            {
                Id = IdGenerator.GenerateId(),
                Kind = DocumentLineKind.Item,
                Name = description,
                Quantity = quantity,
                UnitPrice = unitPrice,
            });
        }

        await InvoiceCalculation.ApplyAsync(services.GetRequiredService<IDocumentCalculator>(), invoice, settings, issueDate: null, cancellationToken);

        var validation = await invoices.ValidateAsync(invoice, cancellationToken);

        if (!validation.Succeeded)
        {
            return "The invoice could not be drafted: " + string.Join(" ", validation.Errors.Select(error => error.ErrorMessage));
        }

        await invoices.CreateAsync(invoice, cancellationToken);

        return new JsonObject
        {
            ["id"] = invoice.ItemId,
            ["status"] = "Draft",
            ["currency"] = invoice.Currency,
            ["total"] = invoice.Totals?.Total ?? 0m,
            ["adminUrl"] = AdminLink(services, invoice.ItemId),
            ["note"] = "The invoice is a draft: staff check and issue it.",
        }.ToJsonString(JsonOptions);
    }

    private static List<(string Description, decimal Quantity, decimal UnitPrice)> ReadLines(AIFunctionArguments arguments)
    {
        var lines = new List<(string, decimal, decimal)>();

        if (arguments is null || !arguments.TryGetValue("lines", out var value) || value is null)
        {
            return lines;
        }

        var element = value is JsonElement json ? json : JsonSerializer.SerializeToElement(value, JsonOptions);

        if (element.ValueKind != JsonValueKind.Array)
        {
            return lines;
        }

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("description", out var description) || string.IsNullOrWhiteSpace(description.GetString()) ||
                !item.TryGetProperty("unitPrice", out var price) || !price.TryGetDecimal(out var unitPrice))
            {
                continue;
            }

            var quantity = item.TryGetProperty("quantity", out var quantityValue) && quantityValue.TryGetDecimal(out var parsed) && parsed > 0m ? parsed : 1m;

            lines.Add((description.GetString().Trim(), quantity, unitPrice));
        }

        return lines;
    }
}

/// <summary>
/// E-mails an open invoice again to its customer, with its PDF and pay link.
/// </summary>
public sealed class SendInvoiceReminderTool : InvoicingToolBase
{
    public const string ToolName = "sendInvoiceReminder";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "invoice": { "type": "string", "description": "The invoice's number, such as INV-1001, or its id." }
          },
          "required": ["invoice"],
          "additionalProperties": false
        }
        """);

    public override string Name => ToolName;

    public override string Description => "Reminds a customer of an open invoice that still owes something by e-mailing it to them again, with its PDF and pay link. Give the invoice's number or id.";

    public override JsonElement JsonSchema => _schema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var services = arguments.Services;

        if (!await IsAllowedAsync(services, InvoicingPermissions.ManageInvoices))
        {
            return NotAllowedMessage;
        }

        var key = Read(arguments, "invoice");
        var invoices = services.GetRequiredService<IInvoiceManager>();
        var invoice = key is null ? null : await invoices.FindByNumberAsync(key, cancellationToken) ?? await invoices.FindByIdAsync(key, cancellationToken);

        if (invoice is null)
        {
            return "No invoice has that number or id.";
        }

        if (invoice.Status != InvoiceStatus.Open || invoice.AmountDue <= 0m)
        {
            return $"Invoice {invoice.Number ?? invoice.ItemId} owes nothing now, so no reminder was sent.";
        }

        var sent = await services.GetRequiredService<IInvoiceSender>().SendAsync(new InvoiceSendRequest { InvoiceId = invoice.ItemId }, cancellationToken);

        return sent.Succeeded
            ? $"Invoice {invoice.Number} was e-mailed again to {invoice.BillTo?.Contact?.Email ?? "its customer"}: {invoice.AmountDue.ToString("0.00", CultureInfo.InvariantCulture)} {invoice.Currency} is still due."
            : $"Invoice {invoice.Number} could not be sent: {string.Join(" ", sent.Errors)}";
    }
}
