---
sidebar_label: PDF documents
title: PDF documents
description: "The PDF building block: IPdfDocumentRenderer over a designed PdfDocumentLayout, and the PdfDocumentWriter, PdfCanvas and PdfFlow primitives for documents that place or flow their own content."
---

The PDF building block writes PDF files for every module that prints something: a certificate, a check, a pay statement, an invoice, a report export. It is two libraries, not a feature:

| Project | Holds |
| --- | --- |
| `CrestApps.OrchardCore.Pdf.Abstractions` | `IPdfDocumentRenderer`, the layout model in `CrestApps.OrchardCore.Pdf.Models`, `PdfColor` and `PdfPageSize`. |
| `CrestApps.OrchardCore.Pdf.Core` | The renderer, the drawing primitives, fonts, text wrapping, images and QR codes, on PdfPig and QRCoder. |

A module never references another PDF library: `PdfLibraryLicenseTests` bans the libraries whose licenses do not allow it. Never copy the drawing, font or text-wrapping code, and never draw a QR code as an image. [Business Documents](./business-documents) builds its receipts and invoices on this block, and [Reports](./reports) exports PDFs with it.

Positions and sizes are in points (72 to the inch) from the top left of the page, as a document is laid out.

## A designed layout

For a document whose design is data, such as a certificate template, describe the pages and hand them to `IPdfDocumentRenderer`:

```csharp
services.AddPdfDocumentRenderer();
```

`AddPdfDocumentRenderer()` (Pdf.Core) registers the renderer once, however many modules call it. Then:

```csharp
var fonts = PdfFontSet.GetInstalled("Noto Sans");
var page = new PdfPageLayout(PdfPageSize.Letter.Landscape);

page.Elements.Add(new PdfTextElement
{
    Text = holderName,
    X = 72,
    Top = 200,
    Width = 0,
    FontSize = 32,
    Bold = true,
    Alignment = PdfTextAlignment.Center,
});

page.Elements.Add(new PdfQrCodeElement { Value = verificationUrl, X = 640, Top = 440, Size = 96 });

var layout = new PdfDocumentLayout
{
    Title = "Certificate",
    RegularFont = fonts.Regular,
    BoldFont = fonts.Bold,
};

layout.Pages.Add(page);

byte[] pdf = renderer.Render(layout);
```

The layout model:

| Type | What it is |
| --- | --- |
| `PdfDocumentLayout` | `Title` and `Author` (the document information), `RegularFont` and `BoldFont` (TrueType files, embedded as a subset of the characters used; without a bold file the regular face is outlined), and the `Pages`. |
| `PdfPageLayout` | `Size`, an optional `BackgroundColor`, an optional `BackgroundImage` (a JPEG or 8-bit PNG stretched over the page, behind the elements) and the `Elements`, drawn in order so a later one is drawn over an earlier one. |
| `PdfElement` | The base of every element: `X` and `Top`. |
| `PdfTextElement` | `Text` on one line, or wrapped to `Width` (0 writes each line at `X`); `FontSize`, `Bold`, `Color`, `Alignment` (`PdfTextAlignment`: `Left`, `Center`, `Right`) and `MaxLines` (a longer text ends its last line with an ellipsis; 0 writes every line). Line breaks are kept, and a character the font has no glyph for is written as a question mark. |
| `PdfImageElement` | A JPEG or 8-bit PNG (`Data`) stretched to `Width` by `Height`. A file of another kind is not drawn. |
| `PdfLineElement` | A line from (`X`, `Top`) to (`X2`, `Top2`), with `Color` and `Thickness`. |
| `PdfRectangleElement` | `Width` by `Height`, with an optional `Fill` and an optional `Outline` of `OutlineThickness`. |
| `PdfQrCodeElement` | A QR code holding `Value`, drawn as vector squares with its quiet zone on a white square, `Size` points wide, in `Color`. |

`PdfPageSize` has `Letter`, `Legal`, `A4`, `FromInches` and `Landscape` (the same paper on its side). `PdfColor` has `Black`, `White` and `FromRgb`, `TryParseHex` for a stored brand color such as `#1E3A8A`, and `TryParse` and `Parse` for a color a designer typed.

The certificate template of [Skills](./skills) (`CertificateDocumentBuilder`) is built this way.

## Documents that place or flow their own content

When code lays a document out itself, use the primitives of Pdf.Core:

| Type | Use |
| --- | --- |
| `PdfDocumentWriter` | The entry point for documents that place their content themselves, such as a check, a pay statement or a form: add the fonts, add a page, draw on it, and write the file. |
| `PdfCanvas` | Draws on a page: text, lines, shapes and pictures at exact positions. |
| `PdfFlow` | The entry point for documents whose content flows from page to page, such as an invoice with any number of lines: it keeps the page being filled and the position on it, adds a page when the next block does not fit, and writes page numbers in the footers. |
| `PdfFontSet` | The regular and bold faces of an installed family. `PdfFontSet.GetInstalled(family)` finds the family, or the font that stands in for it (`PdfFontResolver`, which knows the file names on Windows, Linux and macOS). |
| `PdfFontFace` | One face, measured and written with PdfPig. |
| `PdfText` | Cleans a value (keeps its line breaks, turns other control characters into spaces) and wraps it to a width, between words and inside a word too long for the line. |
| `PdfImage` | A PNG or JPEG with its size in pixels read from the file, to keep a logo's proportions. |
| `PdfQrCode` | Encodes a value with medium error correction and draws it as vector squares. |

## Fonts

Every PDF embeds the font it is written in, so it looks the same everywhere. The font must be installed on the server. A site whose documents hold letters beyond Latin installs a family that has them, such as Noto Sans, and names it where the document lets you choose a font (the certificate template's **Font**, for one).
