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
| `date-range-picker` | `flatpickr`, `flatpickr-culture` | A reusable date-range dropdown described below. |

## Date range picker

`date-range-picker` enhances two machine-formatted date/time inputs (a "from" and a "to") with a single Bootstrap dropdown that offers common presets grouped into **Relative days** (Today, Yesterday, Last 7 Days, Last 30 Days, Last 90 Days), **Calendar periods** (This Week, Last Week, This Month, Last Month, This Quarter, Last Quarter, This Year, Last Year), and **Rolling months** (Last 3 Months, Last 6 Months, Last 12 Months), a **Custom Range** (two Flatpickr date-time inputs), and single **On or before** / **On or after** date-time bounds. The dropdown button always shows the current selection as readable text (for example, _From Jan 1, 2026 to Jan 31, 2026_).

When a user opens an empty custom or single-bound panel, the picker seeds it with today's local date. Lower-bound inputs default to `00:00`; upper-bound inputs default to `23:59`.

Although it was introduced for the [Reports](reports.md) module, it is a general-purpose resource and can be used anywhere. It is purely client-side: it reads and writes the two underlying inputs, so the surrounding form submits their values unchanged.

### Rendering with the view component (recommended)

The Resources feature ships a `DateRangePicker` view component that renders the full markup contract for you (including the required script/style resources), so you only supply the two field names and their current values:

```cshtml
@await Component.InvokeAsync("DateRangePicker", new
{
    fromName = Html.NameFor(m => m.From),
    toName = Html.NameFor(m => m.To),
    fromId = Html.IdFor(m => m.From),
    toId = Html.IdFor(m => m.To),
    from = Model.From,
    to = Model.To,
    selectedRangeName = Html.NameFor(m => m.Range),
    selectedRange = Model.Range,
    label = T["Date range"].Value,
})
```

| Parameter | Purpose |
| --- | --- |
| `fromName` / `toName` | **Required.** The form field names posted for the lower and upper bounds. |
| `fromId` / `toId` | Optional HTML ids. Default to values derived from the field names. |
| `from` / `to` | Optional `DateTime?` initial values. |
| `selectedRangeName` | Optional form field name used to persist the selected preset key (for example `last30` or `custom`). Bind it to a `string` property so the picker restores the same option after the form is submitted; when omitted the picker falls back to the Custom option whenever initial values are present. |
| `selectedRange` | Optional initial selected preset key (the current value of the `selectedRangeName` field). |
| `label` | Optional label rendered above the control. |
| `labelCssClass` | Optional CSS classes for the label element (defaults to `form-label`). Use `form-label form-label-sm mb-1` for dense filter forms. |
| `placeholder` | Optional toggle placeholder shown when nothing is selected (defaults to _Select range_). |
| `wrapperCssClass` | Optional CSS classes for the root element (defaults to `col p-1`). |
| `toggleCssClass` | Optional CSS classes for the dropdown toggle button (defaults to `form-select`). Use `form-select form-select-sm` for dense filter forms. |

The bound fields should be `DateTime?` so the machine format (`yyyy-MM-ddTHH:mm`) round-trips through model binding.

### Persisting the selected option

The picker enhances two raw from/to date inputs, so on its own it cannot tell whether a reloaded range came from a preset (for example _Last 30 Days_) or a hand-picked custom range — it would default to **Custom Range** every time the page reloads. To keep the originally chosen option selected, bind `selectedRangeName` to a `string` property and echo it back through `selectedRange`. The picker writes the current preset key into a hidden input on every change, the form submits it alongside the dates, and the option is restored on reload. For paginated list filters, also add the key to your driver's route values (next to the from/to values) so it survives page navigation. Named presets keep the exact stored range that was filtered — they are **not** recomputed on reload — while the label still shows the preset name.

### Requiring the resource manually

If you render the markup yourself instead of using the view component, require the resources:

```html
<style asp-name="flatpickr"></style>
<script asp-name="flatpickr" at="Foot"></script>
<script asp-name="flatpickr-culture" at="Foot"></script>
<script asp-name="date-range-picker" at="Foot"></script>
```

### Markup contract

The script auto-initializes every element with a `data-date-range-picker` attribute. The expected structure is:

| Selector | Purpose |
| --- | --- |
| `[data-date-range-picker]` | Root element. Reads `data-week-start` (0=Sunday .. 6=Saturday), `data-date-pattern`, `data-time-pattern`, the previously selected preset key `data-drp-initial`, and the localized `data-prior-label`, `data-after-label`, `data-from-word`, `data-to-word` words. |
| `[data-drp-toggle]` | The dropdown toggle button. |
| `[data-drp-label]` | A child of the toggle; its text is replaced with the current selection. Its `data-placeholder` is shown when nothing is selected. |
| `[data-drp-selected]` | Optional hidden input; the script writes the selected preset key into it so it round-trips with the form. |
| `input[type=radio][data-drp-range]` | One radio per option; the `value` is the preset key (`today`, `yesterday`, `last7`, `last30`, `last90`, `thisWeek`, `lastWeek`, `thisMonth`, `lastMonth`, `last3Months`, `last6Months`, `last12Months`, `thisQuarter`, `lastQuarter`, `thisYear`, `lastYear`, `custom`, `prior`, `after`). |
| `[data-drp-panel="custom"]` | Panel holding the custom inputs `[data-drp-from]` and `[data-drp-to]` — the real submit inputs. |
| `[data-drp-panel="prior"]` | Panel with a single `[data-drp-prior-date]` date-time input; choosing a value sets only the **to** input (on or before). |
| `[data-drp-panel="after"]` | Panel with a single `[data-drp-after-date]` date-time input; choosing a value sets only the **from** input (on or after). |

Presets are computed in the browser using the supplied first day of the week; the resulting range is written into the from/to inputs and the surrounding form submits them. The `DateRangePicker` view component renders this exact contract; see its `Views/Shared/Components/DateRangePicker/Default.cshtml` view for a complete, localized example.
