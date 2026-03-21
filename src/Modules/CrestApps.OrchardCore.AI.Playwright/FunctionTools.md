# OrchardCore Playwright Skills and Function Tools

This file defines the OrchardCore-specific Playwright tool surface for `CrestApps.OrchardCore.AI.Playwright`.

Keep the layers separate:

- Skill: the AI playbook for an OrchardCore workflow.
- Tool: the deterministic OrchardCore action exposed to the AI.
- Service: the reusable C# implementation behind the tool.
- Tenant or app: the orchestrator that loads the right OrchardCore skill and tool set for the current session.

## Operating Principles

- Treat OrchardCore admin as a structured CMS, not a generic website.
- Expose OrchardCore-specific tools only.
- Keep generic browser click and fill behavior inside services, not in the public AI tool surface.
- Verify every mutating step with page state, toast, heading, URL, status, or screenshot evidence.
- Scope actions to OrchardCore structures such as content rows, editor tabs, fieldsets, cards, summaries, and widget containers.
- Continue from the current page whenever possible.

## Current Tool Architecture

These are the OrchardCore tools that should remain exposed to the AI.

| Tool | Primary C# service | Use for | Verify after call |
| --- | --- | --- | --- |
| `playwright_capture_state` | `IOrchardAdminPlaywrightService.CaptureStateAsync()` | Ground the next step from the live page before risky actions | URL, title, heading, toast, validation messages, visible buttons |
| `playwright_open_admin_home` | `IOrchardAdminPlaywrightService.OpenAdminHomeAsync()` | Ensure the session is inside the correct Orchard admin shell | Admin URL, authenticated state, admin heading |
| `playwright_open_content_items` | `IOrchardAdminPlaywrightService.OpenContentItemsAsync()` | Move to the content items list without guessing routes | Content list heading and URL |
| `playwright_list_content_items` | `IOrchardAdminPlaywrightService.ListVisibleContentItemsAsync()` | Inspect visible content rows, titles, types, status, and edit availability | Returned titles, status text, content type, item count |
| `playwright_open_content_item_editor` | `IOrchardAdminPlaywrightService.OpenContentItemEditorAsync()` | Open the correct content item by title using row-scoped actions | Matched title, editor URL, editor heading, post-open state |
| `playwright_open_new_content_item` | `IOrchardAdminPlaywrightService.OpenNewContentItemAsync()` | Start the create flow for a content type from the admin list | Editor heading and title field visibility |
| `playwright_open_editor_tab` | `IOrchardAdminPlaywrightService.OpenEditorTabAsync()` | Open Orchard editor tabs, accordion sections, summaries, and visible editor panels by name | Matched tab or section and resulting editor state |
| `playwright_set_content_title` | `IOrchardAdminPlaywrightService.SetContentTitleAsync()` | Set the Orchard title field reliably | Updated editor state and visible title input |
| `playwright_set_field_value` | `IOrchardAdminPlaywrightService.SetFieldValueAsync()` | Update typed Orchard fields such as text, textarea, select, and checkbox | Field result plus updated observation |
| `playwright_set_body_field` | `IOrchardAdminPlaywrightService.SetBodyFieldAsync()` | Update body-like Orchard fields with append or replace semantics | Resolved editor type and updated observation |
| `playwright_save_draft` | `IOrchardAdminPlaywrightService.SaveDraftAsync()` | Save without publishing | Toast, URL, heading, status message |
| `playwright_publish_content` | `IOrchardAdminPlaywrightService.PublishContentAsync()` | Publish the current content item when the user asked for publish | Toast, URL, heading, and post-publish page state |
| `playwright_publish_and_verify` | `IOrchardAdminPlaywrightService.PublishAndVerifyAsync()` | Publish the current content item and return structured Orchard verification evidence | Verification signals and final observation |
| `playwright_get_page_content` | `IPlaywrightPageInspectionService.GetPageContentAsync()` | Read the visible page when answering live UI questions | Visible content and main heading |
| `playwright_find_element` | `IPlaywrightPageInspectionService.FindElementsAsync()` | Locate a control, field, widget, tab, or text snippet from the current page | Match list with text, role, label, selector hints |
| `playwright_check_element_exists` | `IPlaywrightPageInspectionService.CheckElementExistsAsync()` | Confirm whether a target widget, field, or control is visible | Boolean existence and closest matches |
| `playwright_get_visible_widgets` | `IPlaywrightPageInspectionService.GetVisibleWidgetsAsync()` | List visible widget-like cards, headings, legends, and sections | Widget names and source hints |
| `playwright_take_screenshot` | `IPlaywrightPageInspectionService.TakeScreenshotAsync()` | Capture evidence for validation, debugging, and user confirmation | Saved path, timestamp, page URL, screenshot scope |
| `playwright_diagnose_orchard_action` | `IOrchardEvidenceService.FindOrchardElementWithEvidenceAsync()` | Locate a named OrchardCore action using 5-tier priority locator strategies; captures full evidence on failure | Found, MatchedLocator, PageScreenshotPath, ContainerScreenshotPath, PageHtmlPath, Attempts |

## OrchardCore Skill Catalog

The AI should reason in Orchard skills, then call deterministic Orchard tools.

| Skill name | Covers | Current tool entry points | Recommended next C# surface |
| --- | --- | --- | --- |
| `orchard-admin-navigation` | Admin shell navigation, tenant-aware routes, admin menu traversal, and safe return-to-context behavior | `playwright_capture_state`, `playwright_open_admin_home`, `playwright_open_content_items` | `IOrchardAdminNavigationService.OpenAreaAsync()`, `OpenSectionAsync()` |
| `orchard-content-items` | Content item lists, row actions, search, title matching, status inspection, and item opening | `playwright_open_content_items`, `playwright_list_content_items`, `playwright_open_content_item_editor` | `IOrchardContentListService.OpenListAsync()`, `FindRowAsync()`, `InvokeRowActionAsync()` |
| `orchard-content-editor` | Title editing, typed field editing, editor tab switching, body-field handling, and pause-before-save behavior | `playwright_open_editor_tab`, `playwright_set_content_title`, `playwright_set_field_value`, `playwright_set_body_field` | `IOrchardContentEditorService.OpenTabAsync()`, `SetFieldAsync()`, `SetBodyFieldAsync()` |
| `orchard-publish-verification` | Save, publish, status confirmation, validation handling, and final evidence | `playwright_save_draft`, `playwright_publish_content`, `playwright_publish_and_verify`, `playwright_take_screenshot` | `IOrchardContentWorkflowService.SaveAndVerifyAsync()`, `PublishAndVerifyAsync()` |
| `orchard-widget-tree` | FlowPart, BagPart, widgets, nested cards, expanders, repeated components, and scoped edits inside widget containers | `playwright_get_visible_widgets`, `playwright_find_element`, `playwright_check_element_exists` | `IOrchardWidgetEditorService.FindWidgetAsync()`, `OpenWidgetAsync()`, `AddWidgetAsync()`, `EditWidgetFieldAsync()`, `MoveWidgetAsync()` |
| `orchard-content-definitions` | Content types, parts, fields, definitions screens, and admin metadata workflows | `playwright_capture_state`, `playwright_open_admin_home`, `playwright_find_element`, `playwright_take_screenshot` | `IOrchardDefinitionAdminService.OpenContentTypesAsync()`, `OpenTypeAsync()`, `OpenPartAsync()`, `OpenFieldAsync()` |
| `orchard-screenshot-diagnostics` | Screenshots, page-state confirmation, evidence capture, UI debugging, and failure reporting | `playwright_capture_state`, `playwright_get_page_content`, `playwright_take_screenshot`, `playwright_find_element`, `playwright_diagnose_orchard_action` | `IOrchardEvidenceService` ✓ implemented |

## Selector Strategy for OrchardCore

Use selectors in this order:

1. Use a dedicated OrchardCore workflow tool.
2. Use OrchardCore structural scoping inside the service layer.
3. Use accessible names and labels inside the service layer.
4. Do not expose raw browser selectors to the AI.

### Stable OrchardCore selector tiers

| Tier | Strategy | OrchardCore examples |
| --- | --- | --- |
| Tier 1 | Route-aware task tools | Open admin home, open content list, open item by title, open editor tab, save draft, publish |
| Tier 2 | OrchardCore structural selectors | `tbody tr`, `[data-content-item-id]`, `.content-item`, `.list-group-item`, `.card`, `.card-header`, `.card-title`, `fieldset > legend`, `details > summary` |
| Tier 3 | OrchardCore field fallbacks | `input[name='TitlePart.Title']`, `input[id*='TitlePart_Title']`, visible `textarea`, `select`, `input[type='checkbox']`, `[contenteditable='true']`, rich editor surface, iframe body |
| Tier 4 | Internal service heuristics only | Accessible-name and label matching, then Orchard-aware related-field discovery |

### OrchardCore-specific selector guidance

- Content item list rows:
  - Scope to the row or container that contains the requested title before clicking `Edit`, `Publish`, `Preview`, `Delete`, or `Clone`.
  - Use title, then content type, then status to disambiguate repeated names.
- Editor tabs and sections:
  - Prefer real tabs first, then buttons, links, summaries, card headers, card titles, and legends.
  - Treat already-visible sections as valid matches instead of forcing an unnecessary click.
- Title field:
  - Prefer label `Title`.
  - Fallback to `input[name='TitlePart.Title']`, `input[id*='TitlePart_Title']`, or Orchard title input variants.
- Typed fields:
  - Text, textarea, checkbox, and select should be handled by Orchard-specific field logic instead of a generic fill tool.
- Body-like fields:
  - Detect textarea, TinyMCE, contenteditable, and iframe editors.
  - Support append and replace modes explicitly.
- Validation and status:
  - Check toast messages, alerts, validation summaries, field validation messages, and visible status badges before claiming success.

## Reusable OrchardCore Workflow Patterns

### Find the Edit button for a specific content item

1. Call `playwright_open_content_items`.
2. Call `playwright_list_content_items` if the visible list is needed for grounding or matching.
3. Call `playwright_open_content_item_editor` with the requested title.
4. If the match is weak, report the closest titles instead of editing the wrong row.

### Open a content item and update fields

1. Open the editor by exact or best visible title match.
2. Call `playwright_open_editor_tab` if the field lives inside another tab or collapsible section.
3. Use `playwright_set_content_title`, `playwright_set_field_value`, and `playwright_set_body_field` for Orchard-specific edits.
4. Stop after the edit unless the user also asked to save or publish.

### Publish or save and verify state

1. Execute `playwright_save_draft`, `playwright_publish_content`, or `playwright_publish_and_verify`.
2. Capture state or screenshot immediately if verification is ambiguous.
3. Confirm success from the latest URL, heading, toast, status text, or structured verification signals.
4. If evidence is weak, report that verification is incomplete instead of guessing.

### Work with FlowPart, BagPart, and nested widgets

1. Capture state and list visible widgets before editing.
2. Identify the parent editor section, then the target widget card or repeated item.
3. Expand collapsed cards or summaries before searching for fields.
4. Scope every action to the current widget container.
5. If the current tool layer cannot target the widget deterministically, stop and use the recommended widget-specific next-wave tools instead of guessing.

### Confirm final UI state

1. Use `playwright_capture_state` for structured confirmation.
2. Use `playwright_get_page_content` when the user needs textual proof.
3. Use `playwright_take_screenshot` for visual proof or ambiguous UI.
4. Report the verified final state only, not intermediate guesses.

## Recommended Next-Wave OrchardCore Tools

These should be added as OrchardCore-specific tools instead of reintroducing generic browser actions.

| Proposed tool | Recommended service call | Why it matters |
| --- | --- | --- |
| `playwright_open_content_definition` | `IOrchardDefinitionAdminService.OpenContentTypesAsync()` | Definitions are a different admin workflow than content items |
| `playwright_open_content_type_editor` | `IOrchardDefinitionAdminService.OpenTypeAsync(string typeName)` | Lets the AI work with type definitions safely |
| `playwright_find_widget` | `IOrchardWidgetEditorService.FindWidgetAsync(string widgetName, string widgetType, string parentPath)` | Required for FlowPart and BagPart depth |
| `playwright_open_widget_editor` | `IOrchardWidgetEditorService.OpenWidgetAsync(...)` | Stable entry point for nested widget editing |
| `playwright_add_widget` | `IOrchardWidgetEditorService.AddWidgetAsync(string containerName, string widgetType)` | Supports real Orchard widget authoring workflows |
| `playwright_validate_content_status` | `IOrchardContentWorkflowService.VerifyStatusAsync(string expectedStatus)` | Needed for production-grade status checking |
| ~~`playwright_capture_evidence`~~ | Implemented as `playwright_diagnose_orchard_action` / `IOrchardEvidenceService` | Now ships as a production tool — see Current Tool Architecture above |

## Production-Ready Service Structure

Keep the Orchard admin service as the current Orchard automation facade, but split deeper logic behind focused services as the tool set grows:

- `IOrchardPageClassifier`
- `OrchardAdminSelectorCatalog`
- `IOrchardAdminNavigationService`
- `IOrchardContentListService`
- `IOrchardContentEditorService`
- `IOrchardContentWorkflowService`
- `IOrchardWidgetEditorService`
- `IOrchardDefinitionAdminService`
- `IOrchardEvidenceService` ✓ (`OrchardEvidenceService` — `FindOrchardElementWithEvidenceAsync`)
- `IPlaywrightPageInspectionService`

This keeps the AI tool layer Orchard-specific while preventing the service layer from collapsing back into generic browser automation.
