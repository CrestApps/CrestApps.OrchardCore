---
sidebar_label: PDF Support
sidebar_position: 2
title: AI Documents (PDF) Support
description: PDF text extraction support for the AI Documents feature.
---

| | |
| --- | --- |
| **Feature Name** | AI Documents (PDF) |
| **Feature ID** | `CrestApps.OrchardCore.AI.Documents.Pdf` |

Extends the 'AI Documents' feature by allowing PDF file.

## Overview

This module extends the AI Documents feature with PDF document support.

## Features

- **PDF Text Extraction**: Extract text content from PDF documents
- **Page-by-Page Processing**: Text is extracted from each page of the PDF

## Getting Started

1. Enable the `AI Documents (PDF)` feature in Orchard Core admin
2. Upload PDF files in the Documents tab of your chat interactions
3. Text content will be automatically extracted and used for RAG


## Technical Details

This module uses the [PdfPig](https://github.com/UglyToad/PdfPig) library for PDF text extraction. PdfPig is a fully open-source PDF library that:
- Extracts text content from PDF documents
- Does not require any external dependencies
- Works cross-platform

## Limitations

- **Scanned PDFs**: Scanned documents that contain images of text (not actual text) will not be extracted correctly. For best results, use PDFs with actual text content.
- **Complex Layouts**: Some complex PDF layouts may not preserve exact text formatting.

## Supported File Types

| Extension | MIME Type |
|-----------|-----------|
| .pdf | application/pdf |

