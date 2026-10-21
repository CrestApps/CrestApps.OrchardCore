---
name: crestapps-admin-index-views
description: >
  Builds or converts a CrestApps.OrchardCore admin index (list) page so it renders through Orchard Core's
  AdminList shape and the admin-list tag helpers, in the List or Grid layout the site and the user pick, with
  row actions in the Buttons or Menu layout. Covers the row display driver (SummaryAdmin and SummaryAdminGrid
  locations, checkbox shape, row attributes), the List-layout row template, the controller's
  AdminListContext, the index view (action bar, admin-list, columns), the list-management-ui script, and the
  pitfalls specific to this repository (ListCatalogEntryViewModel, li-wrapped menu items, empty states).
  Use for every new admin page that lists records and when touching an existing one.
---

# CrestApps admin index views

Orchard Core renders every admin list through the `AdminList` shape (OrchardCore PR #19845, package
`4.0.0-preview-19191` and later). The shape renders the toolbar (count, select-all, bulk actions), the rows,
the empty state, the pager and a List/Grid layout selector. A page never hand-writes a `<ul class="list-group">`,
a toolbar row, a select-all checkbox, an item count or an empty-state `<li>`.

The upstream reference is the Orchard Core skill `orchardcore-admin-index-views` (in the Orchard Core repo under
`.agents/skills/`). This skill is the same recipe applied to this repository's conventions.

**Reference implementation in this repo: AI Profiles.**

| Piece | File |
| --- | --- |
| Driver | `src/Modules/CrestApps.OrchardCore.AI/Drivers/AIProfileDisplayDriver.cs` (`DisplayAsync`) |
| Row template (List layout) | `src/Modules/CrestApps.OrchardCore.AI/Views/AIProfile.SummaryAdmin.cshtml` |
| Row pieces | `src/Modules/CrestApps.OrchardCore.AI/Views/Items/AIProfile.*.SummaryAdmin.cshtml` |
| Controller | `src/Modules/CrestApps.OrchardCore.AI/Controllers/ProfilesController.cs` (`Index`) |
| View | `src/Modules/CrestApps.OrchardCore.AI/Views/Profiles/Index.cshtml` |
| View imports | `src/Modules/CrestApps.OrchardCore.AI/Views/_ViewImports.cshtml` |

## Layouts in one paragraph

`List` renders each row as its `{Model}_SummaryAdmin` shape, through the row template. `Grid` builds each row
with the `SummaryAdminGrid` display type and renders, for each column the view declares, the row zone named after
the column. Display types do **not** share placement: a shape placed only for `SummaryAdmin` is missing from the
grid. Zones named `Actions` and `ActionsMenu` are rendered by the Actions column, so a location such as
`.Location("Actions:5")` works for both layouts.

## 1. Row display driver

The row is the shape `IDisplayManager<TModel>` builds for the item; its type is `typeof(TModel).Name` +
`_SummaryAdmin`. Register the driver against the domain model (never a `*ViewModel`) in the `Startup` of the
feature that owns the list.

```csharp
public override Task<IDisplayResult> DisplayAsync(AIProfile profile, BuildDisplayContext context)
{
    // The row's classes and attributes are rendered on its row element, in both admin list layouts.
    if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
        && context.Shape is Shape rowShape)
    {
        rowShape.Classes.Add("item");
        rowShape.Attributes["data-filter-value"] = profile.Name?.ToLowerInvariant() ?? string.Empty;
    }

    return CombineAsync(
        View("AIProfile_Checkbox_SummaryAdmin", profile)
            .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Checkbox:1")
            .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Select:1"),
        View("AIProfile_Fields_SummaryAdmin", profile)
            .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
            .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
        View("AIProfile_Buttons_SummaryAdmin", profile).Location("Actions:5"),
        View("AIProfile_DefaultTags_SummaryAdmin", profile)
            .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
            .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Type:5"),
        View("AIProfile_DefaultMeta_SummaryAdmin", profile)
            .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
            .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Modified:5"),
        View("AIProfile_ActionsMenu_SummaryAdmin", profile).Location("ActionsMenu:10"));
}
```

Usings: `using OrchardCore;` (for `OrchardCoreConstants`; a top-level using resolves to the global namespace even
inside `CrestApps.OrchardCore.*`) and `using OrchardCore.DisplayManagement.Shapes;` (for `Shape`).

Rules:

- Every visible piece of a row (checkbox, title, badges, dates, buttons, menu items) is its own shape in a zone.
- Pair every `Checkbox`, `Content`, `Tags`, `Meta` and `Description` location with a `SummaryAdminGrid` location
  in the zone of a column. A `Description` usually goes in the title column after the title: `"Name:5"`.
- Only add the checkbox shape when the list has bulk actions. Its input is named `itemIds` with the item id as value.
- `data-filter-value` is only needed when the page uses client-side search (`data-client-side-search="true"`), but
  is harmless otherwise. Keep any other row attribute a script reads (e.g. `data-type-name`).
- If the model's display manager is also used for other display types (front end, detail pages), return
  `null` from `DisplayAsync` unless `context.DisplayType` is `SummaryAdmin` or `SummaryAdminGrid`.
- Other modules that add shapes to the same row (e.g. `AI.Chat` adds `ActionsMenu` items to `AIProfile`) need the
  same treatment: `Actions`/`ActionsMenu` work as is; any other zone needs a `SummaryAdminGrid` location.

### Row pieces

| Zone | Renders as | Never |
| --- | --- | --- |
| `Checkbox` | `<div class="form-check"><input type="checkbox" class="form-check-input" value="@id" name="itemIds" id="itemIds-@id"><label class="form-check-label" for="itemIds-@id">&nbsp;</label></div>` | |
| `Content` | The title as a **plain link** to the edit page (or plain text when there is no edit page) | `<h5>`/`<h6>`/`<strong>` titles |
| `Tags`, `Meta` | One `<span class="badge ta-badge fw-normal">` per fact, leading `<i>` icon, `title` tooltip naming the fact | `font-weight-normal` (Bootstrap 4), bare text, `<span class="hint">` |
| `Description` | `<div class="admin-list-secondary">…</div>` | |
| `Actions` | `btn btn-sm` buttons/links | A hand-written button group or dropdown |
| `ActionsMenu` | **Bare** `<a class="dropdown-item">` / `<button class="dropdown-item">` | Wrapping items in `<li>`: the parser hoists them out of the row and the menu renders empty |

## 2. Row template for the List layout

`Views/{Model}.SummaryAdmin.cshtml` is identical for every list (copy it from `AIProfile.SummaryAdmin.cshtml`).
It renders `Checkbox`, `Content`, `Tags`, `Meta`, `Description`, and the actions through
`@await DisplayAsync(await Factory.CreateAdminListActionsAsync((IShape)Model))`, so the Buttons/Menu setting
applies. Never render `Model.Actions` or `Model.ActionsMenu` yourself, and never use `float-end`/`float-start`.

## 3. Controller

The controller passes the **items**, not shapes. Every list view model deriving from
`ListCatalogEntryViewModel` already has `public AdminListContext List { get; set; }`; other view models add it.

```csharp
var pagerShape = await shapeFactory.PagerAsync(pager, result.Count, routeData);

viewModel.List = new AdminListContext<AIProfile>
{
    Source = result.Entries,
    Pager = pagerShape,
};
```

- Remove the `BuildDisplayAsync(model, updater, "SummaryAdmin")` loop. Keep `Models` (without `Shape`) only if
  the view still needs the records, e.g. to decide on an onboarding empty state.
- `AdminListContext<T>` lives in `OrchardCore.Admin` (package `OrchardCore.Admin.Abstractions`, referenced by
  `CrestApps.OrchardCore.Core`).
- Optional: `Header` (an options editor shape with `Summary`/`Actions` zones, e.g. filters built by a display
  driver), `ToolbarActions` (a shape at the end of the toolbar, e.g. filter selects), `Toolbar` (a toolbar of the
  page's own), `ShowToolbar = false`, `Data`, `Layout` (force one layout), `ShowLayoutSelector = false`.
- A page with **two lists** gives each its own `AdminListContext` and a distinct list name.

## 4. Index view

`Views/_ViewImports.cshtml` of the module needs, once:

```cshtml
@using OrchardCore.Admin
@addTagHelper *, OrchardCore.Admin.Abstractions
```

The view keeps the form, its hidden inputs and the action bar, and renders the list with `<admin-list>`:

```cshtml
<form asp-action="Index" method="post" class="no-multisubmit" data-list-management data-selected-label="@T["selected"]" data-client-side-search="false">
    <input type="submit" name="submit.Filter" id="submitFilter" class="visually-hidden" />
    <input asp-for="Options.BulkAction" type="hidden" />
    <input type="submit" name="submit.BulkAction" class="visually-hidden" />

    <div class="card text-bg-theme mb-3 position-sticky action-bar">
        <div class="card-body">
            <div class="row gx-2">
                <div class="col">
                    <div class="input-group">
                        <input id="search-box" asp-for="Options.Search" class="form-control" placeholder="@T["Search"]" type="search" autofocus autocomplete="off" />
                        <button type="submit" name="submit.Filter" class="btn btn-outline-secondary" title="@T["Search"]" aria-label="@T["Search"]"><i class="fa-solid fa-magnifying-glass" aria-hidden="true"></i></button>
                    </div>
                </div>
                <div class="col-auto">
                    <a asp-action="Create" class="btn btn-secondary create">@T["Add Thing"]</a>
                </div>
            </div>
        </div>
    </div>

    <admin-list name="AIProfiles" context="Model.List"
                bulk-actions="Model.Options.BulkActions"
                empty-message="@T["<strong>Nothing here!</strong> There are no profiles at the moment."]">
        <admin-list-list item-css-class="list-group-item" />
        <admin-list-grid>
            <admin-list-column type="Select" />
            <admin-list-column name="Name" position="20" title="@T["Name"]" />
            <admin-list-column name="Type" position="30" title="@T["Type"]" width="auto" />
            <admin-list-column name="Modified" position="40" title="@T["Last modified"]" width="auto" />
            <admin-list-column type="Actions" title="@T["Actions"]" />
        </admin-list-grid>
    </admin-list>

    <div id="list-alert" class="alert alert-info my-3 d-none text-center" role="alert">@T["Your search returned no results."]</div>
</form>

<script asp-name="list-management-ui" at="Foot"></script>
```

- `name` is a unique, stable PascalCase list name (e.g. `AIProfiles`, `ContactCenterQueues`, `Orders`). It keys the
  user's layout cookie, column providers and the `AdminList-{Name}` alternates.
- The action bar is `row gx-2`. The search is an `input-group` ending with the magnifying-glass button; no icon
  inside the input (`has-search`, `form-control-feedback`), no "Go" text, no `btn-primary` search button.
- The hidden `submit.Filter` input stays first in the form (Enter in the search box triggers it).
- A list **without** bulk actions: `show-select-all="false"`, no `bulk-actions`, no checkbox shape, no `Select` column.
- A list with bulk actions must load `list-management-ui` (it wires `#select-all`, `#items`, `#selected-items`,
  `#actions` and the `itemIds` checkboxes the toolbar and rows render). Client-side search uses the same script
  with `data-client-side-search="true"` and the rows' `data-filter-value`.
- Filters (status, type, "Show" selects) go in the list's toolbar, never in the action bar beside the **Add**
  button. The controller creates a small filter shape and passes it as `AdminListContext.ToolbarActions`:

  ```csharp
  viewModel.List.ToolbarActions = await _shapeFactory.CreateAdminListFiltersAsync("Customers", "customers-filter",
      new AdminListFilter("Options.Filter", options.Filters, S["Show"], S["Show"]));
  ```

  A commerce list uses the shared `CommerceAdminListFilters` shape (Commerce module, model `AdminListFiltersViewModel`
  in `CrestApps.OrchardCore.Core.Models`). It renders the selects in a `<div class="btn-group filter">`, each with
  `data-style="btn-sm"`, `data-commerce-list-filter` and `form="<the filter form's id>"`, so it submits the filter form
  wherever the list is rendered. A theme overrides one list's filters with `CommerceAdminListFilters-{ListName}.cshtml`.
  A list whose filters need other markup keeps its own small template (e.g. `OrdersAdminListFilters`). Reference:
  Customers, Coupons, Inventory and Transactions.
- An onboarding empty state (a card with a call to action) may be shown **instead of** `<admin-list>` when there are
  no records and no search; otherwise use `empty-message`.
- Remove leftovers: `int startIndex = ...` computations, `id="rewrite-rules-sortable-list"` on lists that are not
  sortable, `ignore-elements`, hand-written `<li class="list-group-item text-bg-theme">` toolbars.

## 5. Columns

| Attribute | Meaning |
| --- | --- |
| `type` | `Select` (renders the `Select` zone at position 10) or `Actions` (renders `Actions` + `ActionsMenu` at `end`) |
| `name` | Column and zone name, unique ignoring case. Required for a regular column |
| `title` | Localized header, `@T["…"]` |
| `position` | Placement syntax: `20`, `30`, … |
| `width` | Any CSS width, or `auto` to hug the content. No width shares the remaining space |
| `alignment`, `no-wrap`, `css-class`, `zones` | Cell presentation; `zones="A B"` renders several zones in one cell |

The first data column is the title (`Name`/`Title`), without a width. Badge and date columns use `width="auto"`.

## 6. Validate

Razor views are compiled at runtime: a clean build proves nothing about a page. Open the page and check:

- `?layout=List` and `?layout=Grid` (with **Let users choose the layout of a list** on in Settings → Admin);
  every grid column has content on every row that should have it;
- Settings → Admin → List actions layout = `Menu` and `Buttons`: every row action is present in both;
- select-all shows the count and the bulk Actions dropdown; search filters (Enter and the button);
- no stray links under the list (a `<li>`-wrapped menu item), no console errors, no server exception.

## Not admin lists

Pages that arrange rather than list stay as they are: trees (tenant hierarchy platform view), live inboxes
refreshed by script (Messaging workspace), dashboards, report tables and editors with inline rows.
