---
sidebar_label: Receipts
sidebar_position: 15
title: Receipts
description: A reusable, settings-driven module that builds branded, printable proof-of-purchase receipts any module can consume.
---

| | |
| --- | --- |
| **Feature Name** | Receipts |
| **Feature ID** | `CrestApps.OrchardCore.Receipts` |

The **Receipts** module provides a single, reusable way to turn purchase data into a branded, printable proof-of-purchase receipt. A consuming module (such as [Subscriptions](subscriptions), and future e-commerce modules) supplies only the data it knows about — who was billed, the line items, the taxes, the total, and the payment status — and the Receipts module merges in the tenant's configured issuer branding and produces a consistent, printable document.

The module deliberately holds **no persistence** of its own. Receipts are generated on demand from the consuming module's own records (its transaction ledger remains the single source of truth), so there is no duplicated financial store to keep consistent.

The implementation follows the same three-layer split used elsewhere in the solution:

- **`CrestApps.OrchardCore.Receipts.Abstractions`** defines the reusable contracts and document models: `ReceiptRequest`, `ReceiptDocument`, `ReceiptLineItem`, `ReceiptTaxLine`, `ReceiptStatus`, `ReceiptSettings`, and the `IReceiptService` entry point. These abstractions depend on nothing commerce-specific, so any module can reference them without taking a dependency on payments, checkout, or taxation.
- **`CrestApps.OrchardCore.Receipts.Core`** contains the default `IReceiptService` implementation that merges the configured branding and computes the subtotal.
- **`CrestApps.OrchardCore.Receipts`** contains the Orchard-specific wiring: the branding **settings** screen, its permission and admin-menu entry, and the reusable printable receipt view.

## Concepts

### Building a receipt

Any module builds a receipt by populating a `ReceiptRequest` and calling `IReceiptService.BuildAsync`:

```csharp
public sealed class MyController : Controller
{
    private readonly IReceiptService _receiptService;

    public MyController(IReceiptService receiptService)
    {
        _receiptService = receiptService;
    }

    public async Task<IActionResult> Receipt()
    {
        var request = new ReceiptRequest
        {
            BilledToName = "Ada Lovelace",
            BilledToEmail = "ada@example.test",
            Reference = "TX-1024",
            Currency = "USD",
            LineItems =
            [
                new ReceiptLineItem { Description = "Pro plan", Quantity = 1, Amount = 100m },
            ],
            TaxLines =
            [
                new ReceiptTaxLine { Description = "VAT — Ireland", Amount = 23m },
            ],
            TaxAmount = 23m,
            Total = 123m,
            Status = ReceiptStatus.Paid,
            IsTest = false,
        };

        var document = await _receiptService.BuildAsync(request);

        return View(document);
    }
}
```

`BuildAsync` returns a `ReceiptDocument` that combines the request data with the tenant's configured branding and a computed `Subtotal` (`Total - TaxAmount`). The consuming module never embeds issuer branding itself.

### `ReceiptRequest`

The data the consumer supplies:

| Property | Description |
| --- | --- |
| `BilledToName`, `BilledToEmail` | The customer the receipt is billed to. |
| `Reference` | A consumer reference such as a transaction, order, or invoice number. |
| `SourceLabel` | An optional label for the reference (for example `Order`); defaults to *Reference* when omitted. |
| `IssuedAt` | The local date the receipt was issued. |
| `Currency` | The ISO currency code used to format amounts. |
| `LineItems` | The purchased line items. |
| `TaxLines`, `TaxAmount` | The tax breakdown and total tax. |
| `Total` | The grand total charged. |
| `Status` | `Paid`, `Pending`, or `Failed`. |
| `IsTest` | Whether the payment ran in a gateway test mode. |
| `GatewayId` | An optional gateway identifier. |
| `Notes` | Optional free-form notes printed on the receipt. |

### Rendering the printable receipt

The module ships a reusable, self-contained printable view. Because Orchard Core compiles module views under `~/Areas/{ModuleId}/Views`, a consuming module renders the shared receipt from its own view with a full-path partial:

```html
@using CrestApps.OrchardCore.Receipts.Models
@model ReceiptDocument

<partial name="~/Areas/CrestApps.OrchardCore.Receipts/Views/ReceiptDocument.cshtml" model="Model" />
```

This lets every consumer print the same receipt layout while owning its own surrounding page (for example a print toolbar or a back link).

## Settings

Enable the **Receipts** feature, then browse to **Settings → Commerce → Receipts** to configure the issuer branding that appears on every receipt. Access requires the **Manage receipt settings** permission.

| Setting | Description |
| --- | --- |
| **Header title** | The heading printed at the top of every receipt. Leave empty to use the default *Payment receipt* heading. |
| **Business name** | The issuing business name. Leave empty to use the site name. |
| **Logo URL** | The logo shown at the top of the receipt. |
| **Business address** | The postal address of the issuing business. |
| **Contact email**, **Contact phone**, **Website** | Printed in the receipt footer. |
| **Footer text** | Free-form text printed at the bottom of every receipt, such as a thank-you note or return policy. |
| **Show a test-payment badge for test-mode payments** | When enabled, receipts generated from a payment gateway running in test mode display a visible test-payment badge. |

## Consumers

- **[Subscriptions](subscriptions)** renders a subscriber's payment receipt through this module. The subscriber dashboard links to a printable receipt for each recorded transaction; the transaction ledger stored on the subscription session remains the source of truth, and the receipt is generated on demand.
- Future e-commerce and one-time-purchase modules can reuse the same `IReceiptService` and printable view without duplicating any receipt or branding logic.
