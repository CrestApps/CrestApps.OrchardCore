# AI Documents (OpenXml) Support

This module extends the AI Documents feature with Microsoft Office document support (Word, Excel, PowerPoint).

## Features

- **Word Document Extraction**: Extract text from .docx files
- **Excel Spreadsheet Extraction**: Extract data from .xlsx files
- **PowerPoint Presentation Extraction**: Extract text from .pptx files
- **Full Content Parsing**: Extracts text from all paragraphs, cells, and slides

## Getting Started

1. Enable the `AI Documents (OpenXml)` feature in Orchard Core admin
2. Upload Office documents in the Documents tab of your chat interactions
3. Text content will be automatically extracted and used for RAG

## Dependencies

This module requires:
- `CrestApps.OrchardCore.AI.Documents`

## Technical Details

This module uses the [DocumentFormat.OpenXml](https://github.com/OfficeDev/Open-XML-SDK) library from Microsoft for Office document parsing. This is the official SDK for reading and writing Open XML documents.

## Supported File Types

| Format | Extension | Description |
|--------|-----------|-------------|
| Word | .docx | Microsoft Word documents (Office 2007+) |
| Excel | .xlsx | Microsoft Excel spreadsheets (Office 2007+) |
| PowerPoint | .pptx | Microsoft PowerPoint presentations (Office 2007+) |

## Limitations

### Legacy Formats Not Supported

The following legacy formats are **not supported** because they use the older binary format:
- `.doc` (Word 97-2003)
- `.xls` (Excel 97-2003)
- `.ppt` (PowerPoint 97-2003)

To use these files, please convert them to the newer formats (.docx, .xlsx, .pptx) first.

### Content Extraction Notes

- **Word**: Extracts text from all paragraphs in the main document body
- **Excel**: Extracts data row-by-row, with cells separated by tabs
- **PowerPoint**: Extracts text from all text elements across all slides
