---
sidebar_label: Resources
sidebar_position: 5
title: Resources
description: Extends the Resources module with additional reusable scripts and stylesheets.
---

| | |
| --- | --- |
| **Feature Name** | CrestApps Resources |
| **Feature ID** | `CrestApps.OrchardCore.Resources` |

Provides shared resources and libraries used by various CrestApps modules.

## Overview

This module provides shared frontend resources (CSS and JavaScript) that are used by other CrestApps modules. It acts as a central resource library, ensuring consistent styling and behavior across the CrestApps module ecosystem.

Other CrestApps modules declare a dependency on this feature to leverage common scripts and stylesheets without duplicating assets.

## Shared libraries

This feature registers reusable Orchard resource-manager assets that can be consumed by other CrestApps modules.

Current shared libraries include:

- `intl-tel-input` script and stylesheet resources, backed by local copied assets with CDN fallbacks

## Named script resources

The feature also registers reusable scripts that other modules can require by name with the `<script asp-name="...">` tag helper (or the Liquid `{% script %}` tag). Because they are registered here, any module that depends on the **CrestApps Resources** feature can use them without shipping its own copy.

| Resource name | Depends on | Description |
| --- | --- | --- |
| `flatpickr` | – | The [Flatpickr](https://flatpickr.js.org/) date/time picker (script and matching `flatpickr` style). |
| `flatpickr-culture` | `flatpickr` | Exposes `flatpickrCulture.createLocalizedDateConfig` / `createLocalizedDateTimeConfig` helpers that translate .NET culture date/time patterns into Flatpickr configuration. |
| `list-management-ui` | – | Select-all / row-selection behavior for admin list tables. |
| `item-selector` | – | A reusable item selector widget (script and style). |
| `collapsible-panel` | – | Toggle behavior for collapsible admin panels. |
| `report-date-range-picker` | `flatpickr`, `flatpickr-culture` | A reusable date-range dropdown described below. |

## Date range picker

`report-date-range-picker` enhances two machine-formatted date inputs (a "from" and a "to") with a single Bootstrap dropdown that offers common presets (Today, Yesterday, This Week, Last Week, Last 7 Days, Last 30 Days, This Month, Last Month, This Quarter, Last Quarter, This Year, Last Year), a **Custom Range** (two Flatpickr date-time inputs), and single-date **On or before** / **On or after** bounds. The dropdown button always shows the current selection as readable text (for example, _From Jan 1, 2026 to Jan 31, 2026_).

Although it was introduced for the [Reports](reports.md) module, it is a general-purpose resource and can be used anywhere. It is purely client-side: it reads and writes the two underlying inputs, so the surrounding form submits their values unchanged.

### Requiring the resource

```html
<style asp-name="flatpickr"></style>
<script asp-name="flatpickr" at="Foot"></script>
<script asp-name="flatpickr-culture" at="Foot"></script>
<script asp-name="report-date-range-picker" at="Foot"></script>
```

### Markup contract

The script auto-initializes every element with a `data-date-range-picker` attribute. The expected structure is:

| Selector | Purpose |
| --- | --- |
| `[data-date-range-picker]` | Root element. Reads `data-week-start` (0=Sunday .. 6=Saturday), `data-date-pattern`, `data-time-pattern`, and the localized `data-prior-label`, `data-after-label`, `data-from-word`, `data-to-word` words. |
| `[data-drp-toggle]` | The dropdown toggle button. |
| `[data-drp-label]` | A child of the toggle; its text is replaced with the current selection. Its `data-placeholder` is shown when nothing is selected. |
| `input[type=radio][data-drp-range]` | One radio per option; the `value` is the preset key (`today`, `yesterday`, `thisWeek`, `lastWeek`, `last7`, `last30`, `thisMonth`, `lastMonth`, `thisQuarter`, `lastQuarter`, `thisYear`, `lastYear`, `custom`, `prior`, `after`). |
| `[data-drp-panel="custom"]` | Panel holding the custom inputs `[data-drp-from]` and `[data-drp-to]` — the real submit inputs. |
| `[data-drp-panel="prior"]` | Panel with a single `[data-drp-prior-date]` input; choosing a date sets only the **to** input (on or before). |
| `[data-drp-panel="after"]` | Panel with a single `[data-drp-after-date]` input; choosing a date sets only the **from** input (on or after). |

Presets are computed in the browser using the supplied first day of the week; the resulting range is written into the from/to inputs and the surrounding form submits them. See the Reports module's `ReportDateRangeFilter.Edit.cshtml` view for a complete, localized example.
