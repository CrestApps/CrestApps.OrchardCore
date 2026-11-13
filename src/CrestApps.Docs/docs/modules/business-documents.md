---
sidebar_label: Business Documents
sidebar_position: 16
title: Business Documents
description: One document model, template catalog, HTML renderer, PDF renderer and themeable shape for receipts, payment requests, invoices, estimates, credit notes and any other printable business document.
user_manual:
  - user-manual/commerce/document-templates-and-number-series
---

| Feature | Feature ID | What it adds |
| --- | --- | --- |
| **Business Documents** | `CrestApps.OrchardCore.BusinessDocuments` | The `BusinessDocument` model, document templates, the e-mail-safe HTML renderer, the themeable `BusinessDocument` shape and the number series settings page. |
| **Business Documents PDF** | `CrestApps.OrchardCore.BusinessDocuments.Pdf` | The PDF renderer. Depends on Business Documents. |

The **Business Documents** module is the building block every module uses to print a business document: a receipt, a payment request, an invoice, an estimate, a credit note, a statement. A module builds one neutral `BusinessDocument`, and the module renders it as an e-mail body, as a page, or as a PDF, in the look of the document template for its kind. The module names none of its consumers: a consumer registers the kinds of documents it prints.

The projects follow the solution's three-layer split:

- **`CrestApps.OrchardCore.BusinessDocuments.Abstractions`** holds the document model, the template record, the renderer and template manager contracts, the feature ids, the permissions and `AddBusinessDocumentKind`.
- **`CrestApps.OrchardCore.BusinessDocuments.Core`** holds the template store and manager, the HTML renderer and `AddBusinessDocumentsCore()`.
- **`CrestApps.OrchardCore.BusinessDocuments`** holds the admin pages, the shape, the PDF renderer, the recipe and deployment steps and the number series settings page.

## The document model

Every value in a `BusinessDocument` is text that is already written for the reader: amounts carry their currency (use `IMoneyFormatter`), and dates are in the reader's time zone (use `ILocalClock`). The renderers never format money or dates and never compute anything; they lay the document out and encode it.

| Member | What it holds |
| --- | --- |
| `Kind` | The open name the consumer registered, such as `Invoice`. It picks the template and the shape alternate. |
| `Title`, `Number` | The heading and the number. A draft may have no number. |
| `Culture` | The culture the document is rendered in, such as `fr-FR`. The renderers render inside a `CultureScope`, so their default labels follow it. `null` keeps the current culture. |
| `Status` | An optional badge: `Label` and `Tone` (`Neutral`, `Success`, `Warning`, `Danger`, `Info`). |
| `Watermark` | Optional text across every page, such as `DRAFT` or `VOID`. |
| `Issuer`, `Recipient`, `ShipTo` | `BusinessDocumentParty`: `Name`, `Lines` (formatted address lines), `TaxId`, `Email`, `Phone`, `Website`. |
| `RecipientTitle`, `ShipToTitle` | The titles of the party blocks. `null` prints the renderer's localized "Bill to" and "Ship to". |
| `Fields` | Labelled values printed near the top: dates, a reference, the terms. |
| `Columns`, `Rows` | The line table. A column has a `Name`, a `Title`, an `Alignment` (`Start`, `Center`, `End`) and a relative `Width` (0 sizes it to its content). A row has a `Kind` (`Item`, `Section`, `Subtotal`, `Note`), `Cells` keyed by column name and an optional `Detail` printed in smaller text under the first cell. |
| `Totals` | `Label`, `Value`, `Emphasized` (the total or amount due) and `Muted` (a tax line). |
| `Sections` | Titled text blocks after the totals: notes, terms, payment instructions. |
| `Footer`, `LogoUrl`, `AccentColor`, `PaperSize` | The look. `AccentColor` must be `#RRGGBB`; any other value is ignored. `PaperSize` is `Default`, `Letter` or `A4`; `Default` takes the template's, and prints on Letter when there is none. |

## Registering a kind

A module registers each kind of document it prints, so the template editor lists it:

```csharp
services.AddBusinessDocumentKind("Invoice", LocalizationSource.Create<Startup>("Invoices"));
```

## Rendering

```csharp
public sealed class InvoicePrinter
{
    private readonly IBusinessDocumentTemplateManager _templates;
    private readonly IBusinessDocumentHtmlRenderer _htmlRenderer;
    private readonly IServiceProvider _services;

    // ...

    public async Task<(string Html, byte[] Pdf)> PrintAsync(BusinessDocument document, string templateId)
    {
        // The template's logo, accent color, paper size and footer fill what the document leaves empty.
        await _templates.ApplyAsync(document, templateId);

        var html = await _htmlRenderer.RenderAsync(document);

        // The PDF renderer is registered only when the Business Documents PDF feature is enabled.
        var pdfRenderer = _services.GetService<IBusinessDocumentPdfRenderer>();
        var pdf = pdfRenderer is null ? null : await pdfRenderer.RenderAsync(document);

        return (html, pdf);
    }
}
```

| Service | Output |
| --- | --- |
| `IBusinessDocumentHtmlRenderer` | A self-contained HTML fragment for an e-mail body: tables and inline styles, no stylesheet and no script. Every value is HTML-encoded, an accent color that is not `#RRGGBB` is ignored, and a logo address with a scheme other than `http` or `https` is left out. A watermark is printed as a banner at the top. |
| `BusinessDocument` shape | A Bootstrap card for a page, with print styles and the watermark across the card. Render it with `@await DisplayAsync(await New.BusinessDocument(Document: document))`. |
| `IBusinessDocumentPdfRenderer` | A PDF file (see below). |

### The shape and its alternates

The shape takes the document as its `Document` property. The module adds a `BusinessDocument__{Kind}` alternate, built from the letters, digits and underscores of the kind, so a theme or a module gives one kind its own look with a `BusinessDocument-{Kind}.cshtml` view, such as `BusinessDocument-Receipt.cshtml`.

### The PDF

The PDF renderer lays the document out on the shared PDF library (`CrestApps.OrchardCore.Pdf.Core`, written with the Apache-2.0-licensed PdfPig): the title, number and status beside the issuer and its logo, a rule in the accent color, the recipient, ship-to and fields side by side, the line table with its header row filled with the accent color and repeated on every page, wrapped cells, section, subtotal and note rows, the totals on the right with the emphasized total, the sections, the footer, **Page X of Y** on every page, and the watermark as large light text behind the content of every page. A row moves to the next page whole; only a row taller than a page is split between its lines.

- **Logo.** The logo is read only from the site's media library, through Orchard Core's media file store, and only when it is a PNG or JPEG file (checked from its content) of at most 2 MB. The address may be relative (`/media/logo.png`), absolute with the media library's path, or a media path (`logo.png`). The server never fetches any other address, so a document cannot make it call a URL someone typed. Without the **Media** feature, PDFs are written without a logo. A logo PdfPig cannot embed is left out and logged as a warning.
- **Fonts.** The PDF embeds the installed Arial, or the TrueType font that stands in for it. A host with no usable font writes the PDF in Helvetica, which cannot show text outside Latin-1; each such document is counted on `crestapps.business_document.pdf.font_fallbacks` (meter `CrestApps.OrchardCore.BusinessDocuments`) and a warning is logged once.

## Document templates

A `BusinessDocumentTemplate` is a catalog record (YesSql collection `BusinessDocuments`, index `BusinessDocumentTemplateIndex`) managed through `IBusinessDocumentTemplateManager`, with created, modified and owner stamping:

| Member | What it holds |
| --- | --- |
| `Name`, `Description` | The template's name (required) and notes. |
| `Kinds` | The kinds it is for; empty for every kind. |
| `IsDefault` | Whether it is used for its kinds when a document names no template. Saving a template as the default in the admin clears the default flag of the other templates that claim the same kinds (or every kind, when it does too). |
| `LogoUrl`, `AccentColor`, `PaperSize`, `Footer` | The look it gives the documents. |

`GetForKindAsync(kind, templateId)` returns the named template when it exists and is for the kind; otherwise the default for the kind, a template that lists the kind before one for every kind; otherwise `null`. `ApplyAsync(document, templateId)` copies the template's logo, accent color, paper size and footer onto the document where the document has none.

The admin pages are at **Settings > Business documents > Document templates** (see the [user manual](../user-manual/commerce/document-templates-and-number-series.md)). Other modules can add fields to the template editor with a `DisplayDriver<BusinessDocumentTemplate>`.

## Number series settings

**Settings > Business documents > Number series** (settings group `number-series`) lists every series registered with `services.AddNumberSeries(name, title, defaultFormat)` and stores the formats the site chooses in `NumberSeriesSettings`, which `INumberSeries` reads. Each format is checked with `NumberSeriesFormat.Validate()`, and nothing is saved while a series has an error. A series set back to the format it was registered with is removed from the settings, so it follows its registration again.

## Permissions

| Permission | Lets a user |
| --- | --- |
| `ManageBusinessDocumentTemplates` (**Manage business document templates**) | Add, change and remove document templates. |
| `ManageNumberSeries` (**Manage number series**) | Change the formats of the number series. It is registered for the `number-series` settings group. |

Both are granted to the Administrator role by default.

## Recipes and deployment

The `BusinessDocumentTemplates` recipe step creates or updates templates by `ItemId`; the **Document templates** deployment step exports all of them in the same shape. The created, modified and owner members are not exported. `PaperSize` is `0` (Default), `1` (Letter) or `2` (A4).

```json
{
  "name": "BusinessDocumentTemplates",
  "Templates": [
    {
      "ItemId": "4bdt0000000000000000000001",
      "Name": "Standard",
      "Kinds": [ "Invoice", "Receipt" ],
      "IsDefault": true,
      "LogoUrl": "/media/logo.png",
      "AccentColor": "#2563EB",
      "PaperSize": 2,
      "Footer": "Thank you for your business."
    }
  ]
}
```

The number series formats travel in Orchard Core's `Settings` step:

```json
{
  "name": "Settings",
  "NumberSeriesSettings": {
    "Formats": {
      "Financial.Invoice": { "Pattern": "INV-{yyyy}-{seq:0000}", "FirstNumber": 1, "Reset": 1 }
    }
  }
}
```

`Reset` is `0` (never), `1` (each year) or `2` (each month). Both are described to CrestApps Recipes when that feature is enabled.

## Consumers

- **[Receipts](receipts)** registers the `Receipt` kind and maps its `ReceiptDocument` to a `BusinessDocument`: the printable receipt view renders the shape, and the receipt e-mail body comes from the HTML renderer.
- **[Transactions](transactions)** registers the `PaymentRequest` kind (`TransactionsConstants.BusinessDocumentKinds.PaymentRequest`, listed as *Payment requests*) while the Receipts module is on: `ITransactionInvoiceBuilder` returns the payment request of a transaction (shown to customers as its invoice) as a `BusinessDocument` with the payment request template applied, and the invoice and pay pages render the `BusinessDocument` shape. An invoicing module replaces the builder so a transaction posted for a real invoice shows that invoice.
