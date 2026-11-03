---
name: crestapps-admin-edit-views
description: >
  Builds or converts a CrestApps.OrchardCore admin create, edit, settings, import or other form page so its
  buttons (Save, Cancel, Delete, Save and test, Import...) sit in Orchard Core's sticky bottom action bar
  (<div class="action-bar-bottom">), with the less used ones marked action-bar-secondary so they go into the bar's
  ellipsis menu in the Menu actions layout and on small screens. Covers which forms use the bar and which keep
  their buttons inline (modals, card sub-forms, list filters, row actions, storefront pages), forms shared with a
  modal, button groups, hints that sat beside the buttons, script-hidden bars, and the lint test that guards it.
  Use for every new admin form page and when touching an existing one.
license: Apache-2.0
metadata:
  author: CrestApps Team
  version: "1.0"
---

# CrestApps admin edit views

Orchard Core keeps the buttons of every admin edit page in a sticky bar fixed to the bottom of the screen, so
**Save** and **Cancel** stay visible however long the form is (OrchardCore PR #19302, TheAdmin
`4.0.0-preview-19210` and later). This repository follows the same rule on every admin form page.

The bar comes from TheAdmin theme; a view only marks it up:

- `action-bar-bottom` on the `<div>` that holds the buttons. The theme fixes it to the bottom of the content area,
  reserves the space it covers, and keeps focused fields above it.
- `action-bar-secondary` on each less used button (**Delete**, **Preview**, **Deliveries**, "Receive everything").
  When **Configuration → Settings → Admin → Actions layout** is **Menu**, and always on small screens, the theme
  moves these into an ellipsis menu so only the main buttons stay in view.

The bar is `position: fixed`, so it can sit anywhere inside the `<form>`: the buttons stay in the form and submit
it as before. Content item editors and site settings pages already get the bar from Orchard Core
(`Content.Edit`, `SiteSettings.SaveButton`), so only the module's own views need it.

## The markup

The bar replaces the old `ocat-wrapper` / `ocat-end-offset` button row at the end of the form:

```cshtml
<form asp-action="Edit" method="post" class="no-multisubmit">
    @Html.ValidationSummary()
    @await DisplayAsync(Model.Editor)

    <div class="action-bar-bottom">
        <button class="btn btn-primary" type="submit">@T["Save"]</button>
        <a class="btn btn-secondary cancel" role="button" asp-action="Index">@T["Cancel"]</a>
    </div>
</form>
```

Not this:

```cshtml
    <div class="ocat-wrapper">
        <div class="ocat-end-offset">
            <button class="btn btn-primary" type="submit">@T["Save"]</button>
            <a class="btn btn-secondary cancel" role="button" asp-action="Index">@T["Cancel"]</a>
        </div>
    </div>
</form>
```

Rules:

- Put the buttons straight in the bar. Do not wrap them in `ocat-wrapper`, `ocat-end-offset`, `d-flex gap-2` or
  `mb-3` divs, and do not add `mt-*`/`ms-*`/`mb-1` spacing: the bar lays them out and clears their margins. A
  `btn-group` (a split **Save** button with "and continue") and an `input-group` (a date input with its button) are
  fine as direct children.
- Order the buttons as before: the main action first (`btn-primary`), then the other actions, then **Cancel** or
  **Back**.
- Mark destructive and rarely used actions with `action-bar-secondary`, as Orchard Core does for **Delete** and
  **Preview**. Never mark the main action or **Cancel**.
- Keep one bar per page. Razor branches (`@if (readOnly) { ... } else { ... }`) that each render one bar are fine.
- Text that sat under the buttons (a hint, a "not set up" warning) moves above the bar, in its own
  `ocat-wrapper` / `ocat-end-offset` row, because the bar only holds buttons.
- A bar that a script hides and shows (`element.style.display = 'none'`, then `''`) keeps working: the theme's
  `display: flex` comes back when the inline style is cleared. Keep the element's `id` when the script uses it.
- A view with no form, whose only button ends the page (a "Done" link after showing a new API key), uses the bar
  too.

## When a form keeps its buttons inline

The bar is for the page's own form. These keep their buttons where they are:

- **Modals.** A bar fixed to the bottom of the page is wrong inside a modal: use the `modal-footer`. When a partial
  renders both on a full page and in a modal (the calendar event forms), branch on the modal flag and render the
  bar only on the full page; a local Razor function renders the shared buttons once:

  ```cshtml
  @{
      void EventActions()
      {
          <button class="btn btn-primary" type="submit">@T["Save"]</button>
          <button class="btn btn-danger action-bar-secondary" type="submit" formaction="...">@T["Delete"]</button>
      }
  }

  @if (modal)
  {
      <div class="ocat-wrapper">
          <div class="ocat-end-offset">
              @{ EventActions(); }
              <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">@T["Close"]</button>
          </div>
      </div>
  }
  else
  {
      <div class="action-bar-bottom">
          @{ EventActions(); }
          <a class="btn btn-secondary cancel" role="button" asp-action="Index">@T["Back"]</a>
      </div>
  }
  ```

- **Sub-forms in a card, an accordion or a tab** that act on that part of the page only, such as "Generate codes"
  under a coupon or each step of Store Setup.
- **Admin list pages**: the search, the **Add** button and the list toolbar follow
  `.agents/skills/crestapps-admin-index-views`. Row actions stay in the row.
- **Filter and "Show" forms**, and status panels whose buttons act on a record at once (Confirm, Mark paid, Refund)
  on detail pages.
- **Storefront and customer pages** (cart, checkout, account, booking): the bar belongs to the admin theme only.

## Checks

- `tests/js/lint/admin-forms-use-action-bar.test.js` fails when a module view ends a form with an
  `ocat-wrapper` / `ocat-end-offset` row of buttons outside a card, an accordion or a modal. Run it with
  `npx vitest run tests/js/lint`.
- Open the page in the admin: the buttons sit in the bar at the bottom, and the last field scrolls clear of it.
  Switch **Actions layout** to **Menu** and check that the secondary buttons move into the ellipsis menu. Check a
  phone-width window too.
- Submit each button, including `formaction` ones and those that a script confirms first.

## Reference

- Orchard Core: `src/OrchardCore.Themes/TheAdmin/Assets/js/TheAdmin/actionBar.ts` and
  `Assets/scss/components/_action-bar.scss` (PR #19302); the "Edit page action bar" section of the Admin module
  docs.
- Simple page: `src/Modules/CrestApps.OrchardCore.AI/Views/Profiles/Edit.cshtml`.
- Secondary actions: `src/Modules/CrestApps.OrchardCore.Commerce.Webhooks/Views/WebhooksAdmin/Edit.cshtml`.
- Page and modal from one partial: `src/Modules/CrestApps.OrchardCore.Calendars/Views/CalendarEvents/_EditEventForm.cshtml`.
- Hint moved above the bar: `src/Modules/CrestApps.OrchardCore.AI/Views/ProfileWizard/Setup.cshtml`.
