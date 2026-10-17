## Git Commit Rules

- Never add Claude as a commit author or co-author.
- Never add `Co-authored-by: Claude` or any Claude-related co-author trailer to commits.
- Commits must use only the configured human Git author.

## Admin Index (CRUD List) Pages

- Every admin page that lists records renders through Orchard Core's `<admin-list>` tag helper and `AdminList` shape, so it honors the List/Grid layout and Buttons/Menu row actions from **Configuration → Settings → Admin**. Never hand-write a `<ul class="list-group">`/`<table>` listing, toolbar, select-all, item count, empty state or pager.
- Follow `.agents/skills/crestapps-admin-index-views/SKILL.md` and the "Admin Index (CRUD List) Pages" rules in `AGENTS.md`: records in an `AdminListContext<TModel>`, rows from a display driver with both `SummaryAdmin` and `SummaryAdminGrid` locations, search in the action bar, filters in the list toolbar via `ToolbarActions`.
