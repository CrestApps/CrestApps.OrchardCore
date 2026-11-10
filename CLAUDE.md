## Git Commit Rules

- Never add Claude as a commit author or co-author.
- Never add `Co-authored-by: Claude` or any Claude-related co-author trailer to commits.
- Commits must use only the configured human Git author.

## Building Blocks and Reuse

- The solution is a set of building blocks: each module owns one concept (Accounts, Teams, Addresses, PhoneNumbers, Geo, Calendars, Skills, Money, Reports...) and other modules compose and extend it through shapes, display drivers, parts, handlers and provider interfaces. Never build the same concept twice.
- Before you add a module, a record type, a provider or a shared helper, read the "Building Blocks and Reuse" section of `AGENTS.md` and the catalog in `docs/engineering/building-blocks.md`, map every concept to its owner, and follow the reuse ladder: use, extend, add the missing extension point to the owner, extract a building block when a second module needs a concept, and only then keep it private.
- Never copy a pattern that `docs/engineering/reusability-audit-2026-11.md` lists as a duplicate. Reach other building blocks' services and models through their `.Abstractions` and `.Core` projects.
- A `.Core` or `.Abstractions` library never references a module project. A module may reference another module project only to carry its feature dependency (so the NuGet package comes along); module code, views included, never uses a type from another module assembly. Anything shared moves to `.Core` or `.Abstractions` first. No exceptions.

## Admin Index (CRUD List) Pages

- Every admin page that lists records renders through Orchard Core's `<admin-list>` tag helper and `AdminList` shape, so it honors the List/Grid layout and Buttons/Menu row actions from **Configuration → Settings → Admin**. Never hand-write a `<ul class="list-group">`/`<table>` listing, toolbar, select-all, item count, empty state or pager.
- Follow `.agents/skills/crestapps-admin-index-views/SKILL.md` and the "Admin Index (CRUD List) Pages" rules in `AGENTS.md`: records in an `AdminListContext<TModel>`, rows from a display driver with both `SummaryAdmin` and `SummaryAdminGrid` locations, search in the action bar, filters in the list toolbar via `ToolbarActions`.

## Admin Edit (Form) Page Buttons

- Every admin form page puts its buttons (Save, Cancel, Delete...) in Orchard Core's sticky bottom action bar: `<div class="action-bar-bottom">` inside the `<form>`, with less used ones marked `action-bar-secondary`. Never end a form with an `ocat-wrapper`/`ocat-end-offset` row of buttons.
- Follow `.agents/skills/crestapps-admin-edit-views/SKILL.md` and the "Admin Edit (Form) Page Buttons" rules in `AGENTS.md`. Modals, card sub-forms, list filters, row actions and storefront pages keep their buttons inline.

## Dates and Times

- Never show a user a UTC time unless they asked for one. Every date and time in the UI, reports, exports, emails and notices is converted with Orchard Core's `ILocalClock` (the user's or site's time zone); editors convert entered times back with `ILocalClock.ToUtcLenientAsync`. No `ToLocalTime()`, no formatting a stored UTC value directly, no "(UTC)" labels.
- Follow the "Showing Dates and Times (never UTC)" rules in `AGENTS.md`, including its exceptions (pure dates, a contact's own time zone, machine formats).
