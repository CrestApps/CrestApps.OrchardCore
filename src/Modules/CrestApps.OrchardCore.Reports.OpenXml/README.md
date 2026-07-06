# CrestApps.OrchardCore.Reports.OpenXml

Adds Excel workbook (`.xlsx`) exports to the CrestApps OrchardCore Reports framework by registering an `IReportExportFormat` implementation backed by `DocumentFormat.OpenXml`.

## Features

- Registers an Excel (`.xlsx`) export format for the shared Reports area
- Reuses the Open XML SDK instead of introducing a separate spreadsheet stack
- Keeps the base `CrestApps.OrchardCore.Reports` module provider-agnostic and lightweight

## Dependencies

- `CrestApps.OrchardCore.Reports`

## Usage

Enable both features when you want CSV and Excel exports in the Reports UI:

```json
{
  "steps": [
    {
      "name": "Feature",
      "enable": [
        "CrestApps.OrchardCore.Reports",
        "CrestApps.OrchardCore.Reports.OpenXml"
      ]
    }
  ]
}
```
