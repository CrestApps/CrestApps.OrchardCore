# Receipts

Provides configurable, printable proof-of-purchase receipts that any module can reuse.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Receipts | `CrestApps.OrchardCore.Receipts` | Provides the reusable receipt builder, printable receipt view, and receipt branding settings. Receipts are printed as business documents. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe. The Receipts feature depends on the Business Documents feature (`CrestApps.OrchardCore.BusinessDocuments`): the printable receipt view and the receipt e-mail body map the receipt with `ReceiptBusinessDocumentMapper` and render it with the shared `BusinessDocument` shape and HTML renderer, in the look of the document template for receipts.

## Extension points

- `IReceiptHtmlRenderer` (CrestApps.OrchardCore.Receipts.Abstractions): Renders a `ReceiptDocument` as a self-contained HTML fragment with inline styles, for the body of an email.
- `IReceiptService` (CrestApps.OrchardCore.Receipts.Abstractions): Builds printable receipt documents from consumer-supplied purchase data, merging in the tenant's configured issuer branding.

## Documentation

See [Receipts](../../CrestApps.Docs/docs/modules/receipts.md) in the technical manual.
