# Business Documents

Prints every business document the same way, such as receipts, payment requests, invoices, estimates and credit notes: one neutral document model, document templates, an e-mail-safe HTML renderer, a PDF renderer and a themeable shape. It also holds the settings page of the number series documents are numbered with.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Business Documents | `CrestApps.OrchardCore.BusinessDocuments` | The `BusinessDocument` model, document templates (**Settings > Business documents > Document templates**), the HTML renderer, the `BusinessDocument` shape and the number series settings page (**Settings > Business documents > Number series**). |
| Business Documents PDF | `CrestApps.OrchardCore.BusinessDocuments.Pdf` | The PDF renderer, written with PdfPig. Depends on Business Documents. Reads the logo from the media library when the Media feature is enabled. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe. Modules that print documents, such as Receipts, depend on it.

## Usage

A module registers the kinds of documents it prints and renders a `BusinessDocument` whose values are already formatted:

```csharp
services.AddBusinessDocumentKind("Invoice", LocalizationSource.Create<Startup>("Invoices"));
```

```csharp
await templateManager.ApplyAsync(document, templateId);

var html = await htmlRenderer.RenderAsync(document);
var pdf = await pdfRenderer.RenderAsync(document); // Business Documents PDF only.
```

In a view: `@await DisplayAsync(await New.BusinessDocument(Document: document))`. A theme overrides one kind with `BusinessDocument-{Kind}.cshtml`.

## Extension points

- `BusinessDocument` and its parts (CrestApps.OrchardCore.BusinessDocuments.Abstractions): the neutral, already formatted document model.
- `AddBusinessDocumentKind` and `BusinessDocumentOptions` (Abstractions): register the kinds of documents a module prints.
- `IBusinessDocumentHtmlRenderer`, `IBusinessDocumentPdfRenderer` (Abstractions): render a document as an e-mail body or a PDF file.
- `IBusinessDocumentTemplateManager` (Abstractions): the template catalog, `GetForKindAsync` and `ApplyAsync`.
- `DisplayDriver<BusinessDocumentTemplate>`: add fields to the template editor.
- The `BusinessDocument` shape and its `BusinessDocument__{Kind}` alternates.

## Data portability

- Recipe step and deployment step `BusinessDocumentTemplates` (array `Templates`).
- `NumberSeriesSettings` in Orchard Core's `Settings` step.

## Permissions

- `ManageBusinessDocumentTemplates` (Manage business document templates)
- `ManageNumberSeries` (Manage number series)

## Dependencies

- `CrestApps.OrchardCore.Pdf.Core` for the PDF layout primitives (PdfPig, Apache-2.0).
- Orchard Core Media (optional) for PDF logos.

## Documentation

See [Business Documents](../../CrestApps.Docs/docs/modules/business-documents.md) in the technical manual and [Document Templates and Number Series](../../CrestApps.Docs/docs/user-manual/commerce/document-templates-and-number-series.md) in the user manual.
