# Workstream UX: views, scripts and admin experience

Part of the [November 2026 review plan](README.md). Paths are relative to `src/Modules/CrestApps.OrchardCore.` (written `Module/...`) unless they start with `src/` or `tests/`.

This file covers convention and experience problems in Razor views and script sources, and front-end code that should be shared. Front-end *security* findings (script injection, secret fields, the service-worker cache) are in `01-security-findings.md` under SEC-FE; where a fix here also closes one of those, the item says so.

Sources are audited, not build output: script sources live under each module's `Assets/`. Files copied from CrestApps.Core or a third party (listed at the end) belong to another repository and are not edited here.

Each item is one pull request, small enough to review in one sitting. UI changes are checked in the running site with Playwright against the pages listed (see `AGENTS.md`, *Validation Scenarios*), in both List and Grid admin layouts where a list is touched.

## UX-1. Confirmations that never appear (High, S)

Three separate causes; one fix pattern.

**a. An apostrophe breaks the inline handler.** `@T["..."]` writes the resource text as it is (only its arguments are encoded), so an apostrophe in the text ends the JavaScript string inside the attribute, and a double quote ends the attribute. The handler is a syntax error, so the form submits with no question asked.

| View | Text or value |
| --- | --- |
| `Orders/Views/OrdersAdmin/_OrderActions.cshtml:55` | "Cancel this order? This can't be undone." Broken today in English: **Cancel order submits without confirmation** |
| `Subscriptions/Views/AccountSubscriptions.cshtml:65` | `subscription.Title` inside the string (also SEC-FE: a title with an apostrophe injects script) |
| `Customers/Views/AccountAddresses.cshtml:49`, `Scheduling/Views/BookingPages/Manage.cshtml:31`, `ContactCenter/Views/MyVoicemailGreeting/Index.cshtml:58` | Break in any language whose translation has an apostrophe (French, Italian) |
| `AI/Views/Items/SpeechVoice.Buttons.SummaryAdmin.cshtml:10` | `writeText('@Model.Value.Id')`, same class |

**b. The confirm text is declared but TheAdmin never shows it.** TheAdmin binds its dialog only to `[data-url-af~="RemoveUrl"]`. These eight buttons carry `data-title`/`data-message` without it:

| View | Action |
| --- | --- |
| `Orders/Views/ReturnsAdmin/Detail.cshtml:158` | Refund and close |
| `Shipping/Views/ShipmentLabelsAdmin/Label.cshtml:88` | Buy label (the carrier account is charged) |
| `Shipping/Views/OrderShipments.DetailAdmin.cshtml:104` | Shipment action |
| `Inventory/Views/PurchaseOrdersAdmin/Edit.cshtml:41,45,245` | Purchase order actions |
| `Workforce/Views/TimeClockKiosks/Edit.cshtml:45` | Kiosk action |
| `Workforce/Views/Timesheets/Index.cshtml:33` | Lock to payroll |

**c. Money-moving admin buttons with neither a confirmation nor a double-submit guard.** `no-multisubmit` works only in the admin theme, and these do not have it:

| Button | View | Double-submit guard | Confirm |
| --- | --- | --- | --- |
| Order refund | `Orders/Views/OrdersAdmin/_OrderActions.cshtml:24,49` | No | No |
| Record payment, Adjust, Mark paid | `Transactions/Views/Admin/Detail.cshtml:200,226,256` | No | No |
| Vendor pay out, Release | `Marketplace/Views/VendorsAdmin/Detail.cshtml:19,49` | No | No |
| Charge booking balance | `Bookings/Views/BookingsAdmin/Detail.cshtml:110` | No | No |
| Issue or adjust gift card | `Orders/Views/GiftCardsAdmin/Create.cshtml:10`, `Edit.cshtml:96` | No | No |
| Store credit, loyalty change | `Customers/Views/CustomerStoreCredit.DetailAdmin.cshtml:33`, `CustomerLoyalty.DetailAdmin.cshtml:29` | No | No |
| Payment refund | `Transactions/Views/PaymentsAdmin/Refund.cshtml:37,60` | No | Yes |
| Charge installment | `Subscriptions/Views/InstallmentPlans/Detail.cshtml:212` | No | Yes |
| Lock timesheets | `Workforce/Views/Timesheets/Index.cshtml:30-33` | No | Declared, not shown |

Already guarded (leave): checkout pay (`Checkout/Assets/js/checkout-payment.js:155-185`), installment pay, return refund, buy label, buy phone number.

**Change, in two parts.**

1. **Admin views, now (phase 1a; no new script needed):** add `data-url-af="RemoveUrl"` beside `data-title`/`data-message` on each button (126 views already use `data-url-af`; TheAdmin then shows its dialog and submits the button's closest form), and `class="no-multisubmit"` on every form in the table in (c). Replace the inline `confirm('@T[...]')` in admin views the same way.
2. **Storefront and portal views (no TheAdmin script), with UX-8:** `data-confirm="@T[...]"` on the form and one delegated listener in the shared browser helpers that calls `confirm(form.dataset.confirm)`; `Workforce/Views/Schedules/Shift.cshtml:110` already does this inline, and the views in (a) can copy that inline form until the helper exists. The same listener disables the submit button after the first submit for forms marked `data-once`.
3. Never put localized or model text inside a JavaScript string in an attribute.

Server-side protection against a repeated refund or payout is workstream SEC's concern (idempotency); this item is about the person at the screen.

**Tests.** New lint tests in `tests/js/lint/`: `no-inline-confirm-with-razor.test.js` (bans `confirm('@` and `onclick="...'@`), `confirm-message-needs-binding.test.js` (a `data-message` without `data-url-af` or `data-confirm` fails), `money-forms-guarded.test.js` (the forms in (c) by action name). Playwright: cancel an order and see the dialog; double-click Refund and see one refund.

## UX-2. Admin forms whose buttons are outside the action bar (Medium, S)

`CLAUDE.md` requires `<div class="action-bar-bottom">` inside the form. The lint test (`tests/js/lint/admin-forms-use-action-bar.test.js`) passes because it matches only `ocat-wrapper`/`ocat-end-offset`; these 16 end with a `<div class="mb-3">` or `d-flex gap-2` button row:

- Assets: `Views/AssetTypes/Edit.cshtml:73`
- Geo: `Views/Territories/Edit.cshtml:93`
- TimeTracker: `Views/TimeEntries/Edit.cshtml:130`, `TimeSettings/EditCategory.cshtml`, `TimeSettings/EditStage.cshtml`
- Workforce: `Views/PayrollExport/Columns.cshtml:64`
- Workforce.ContactCenter: `Views/AdherenceExceptions/Create.cshtml`, `AdherenceRuleSets/Edit.cshtml`, `CapacityPlans/Edit.cshtml:115`, `ForecastEvents/Edit.cshtml`, `Forecasts/Create.cshtml`, `PlanningGroups/Edit.cshtml`
- WorkOrders: `Views/WorkOrderSettings/EditChecklist.cshtml`, `EditPolicy.cshtml`, `EditSubStatus.cshtml`, `EditType.cshtml:201`

**Change.** Move the buttons into the action bar, following `.agents/skills/crestapps-admin-edit-views/SKILL.md`. Extend the lint test to fail on any trailing button row directly before `</form>` in a view that belongs to an `[Admin]` controller.

## UX-3. Admin lists that are not on `<admin-list>` (Medium, M)

149 views use it. Still hand-written:

| View | Note |
| --- | --- |
| `TenantHierarchy/Views/Access/Index.cshtml:50` | Table; open since the 2026-11-03 audit |
| `TenantHierarchy/Views/Platform/Index.cshtml` | Nine `list-group` uses; open since the audit |
| `TenantHierarchy/Views/Platform/Parent.cshtml`, `ChildTenants/Features.cshtml` | New |
| `Inventory/Views/InventoryAdmin/Item.cshtml:228-284` | Stock movement table with its own pager |
| `Workforce.ContactCenter/Views/MyOffers/Index.cshtml:26`, `MyAdherence/Index.cshtml:21` | New |
| `Workforce/Views/WorkerTimeClock/Index.cshtml:72` | New |

Not in scope: `AI.Chat/Views/Admin/Index.cshtml` is a chat page with a history panel, not a record list (drop it from the audit's list), and 51 views hold line-item tables inside a detail page, which the rule does not cover.

**Change.** Follow `.agents/skills/crestapps-admin-index-views/SKILL.md`: an `AdminListContext<TModel>`, a display driver with `SummaryAdmin` and `SummaryAdminGrid` locations, search in the action bar, filters through `ToolbarActions`.

## UX-4. Dates and times (Medium, M)

No `ToLocalTime()` and no "(UTC)" label remain. What is left:

1. **A stored UTC value formatted directly:** `Subscriptions/Views/InstallmentPlans/Detail.cshtml:163` (`@T["Due {0:d}", payment.DueUtc]`); `Carts/Drivers/AddToCartDisplayDriver.cs:296` and `GroupedProductsDisplayDriver.cs:166` (`ReleaseDateUtc` shown to shoppers); `TenantHierarchy/Services/ChildTenantManager.cs:424` (a `"u"` string in audit text). Convert with `ILocalClock`.
2. **Hard-coded formats that ignore the user's culture** (`"ddd d MMM HH:mm"`, `"d MMM yyyy"`, `"HH:mm"`): Workforce 43 lines, Dispatch 6, TimeTracker 5. Examples: `Workforce/Views/Items/ScheduleChange.Fields.SummaryAdmin.cshtml:6`, `PredictabilityPayEntry.Fields.SummaryAdmin.cshtml:13`, `BlackoutPeriod.Fields.SummaryAdmin.cshtml:4`, `TimeTracker/Views/MyTime/Index.cshtml:31,108,133`, `ContactCenter/Views/IntervalStatistics/Index.cshtml:7`. Use standard format specifiers (`"g"`, `"d"`, `"t"`) or the `DateTime` shape.
3. **Six ways to convert in a view:** the `DateTime` shape, `LocalAsync(...).ToString("g")` (about 75 lines in 25 modules), `BookingEmailSender.ToLocal`, `CalendarTime.ToLocal`, `TimeTrackerTimeZone.Format`, `DocumentDates.ToSite...`. All produce local time, but the output differs. Add one tag helper in `C/Core` (`<local-time utc="@value" format="g" />`, built on `ILocalClock`), document it in `AGENTS.md` as the only way to show a time in a view, and move the views over module by module. The contact's-own-time-zone and pure-date exceptions in `AGENTS.md` stay.
4. **Browser-zone formatting in scripts** (`toLocaleString`/`Intl` use the device's zone, which can differ from the user's Orchard time zone): `AI.Chat.Interactions/Assets/js/profile-builder.js`, `Calendars/Assets/js/calendar-reminders.js`, `ContactCenter/Assets/js/agent-workspace.js`, `supervisor-dashboard.js`, `Dispatch/Assets/js/field-app.js` (4), `Reports/Assets/js/report-designer/designer-history.js`, `designer-sharing.js`, `Telephony/Assets/js/soft-phone.js` (2); views `Omnichannel.Managements/Views/OmnichannelActivityBatchFields.Edit.cshtml`, `Subscriptions/Views/InstallmentPlans/Create.cshtml`, `Workforce/Views/TimeClockKiosk/Index.cshtml`, `Workforce.ContactCenter/Views/Adherence/Index.cshtml`. Pass the user's IANA zone from the server (one `data-time-zone` attribute on `<body>` from a Resources shape) and use a shared `formatDateTime(value, options)` helper (UX-8) that sets `timeZone`. Live clocks and countdowns (soft phone timers) keep device time.
5. **"UTC" offered as the default in a time-zone picker:** `ContactCenter/Views/BusinessHoursCalendarGeneral.Edit.cshtml:27`, `WorkOrders/Views/ServiceAgreements/Edit.cshtml:31`, `WorkOrders/Views/WorkOrderSettings/EditPolicy.cshtml:27`. Default to the site time zone through `ITimeZoneSelectListProvider`.

**Tests.** Extend `TimeZonePickerArchitectureTests`; add a view lint test that fails on `Utc.ToString(` and on `Utc]` inside `T[...]`; a unit test for the tag helper in a zone with a daylight-saving change.

## UX-5. Accessibility (Medium, M)

| Problem | Count and where | Fix |
| --- | --- | --- |
| Search boxes named only by a placeholder | 64 (`id="search-box"`; none has `aria-label`). Omnichannel.Managements 11, AI 10, Taxation 5, AI.Mcp 3, one or two each in 27 more modules | `aria-label="@T["Search"]"`. `ContactCenter/Views/Shared/_CatalogList.cshtml` fixes seven lists at once; prefer the `<admin-list>` search slot, which carries the label |
| Icon-only buttons with no name | 10: `AI.Chat/Views/Admin/Test.cshtml:60`, `AIChatAdminWidget.cshtml:189,235`, `AIChatSessionChat.cshtml:208,229`, `Widget-AIChat.cshtml:261,309`, `AI.Chat.Copilot/Views/AIProfileCopilotConfig.Edit.cshtml:33`, `AIProfileTemplateCopilotConfig.Edit.cshtml:33`, `AI.Chat.Interactions/Views/ChatInteractionChat.cshtml:126` | `aria-label` and `title`, localized |
| `<th>` without `scope` | 69 in 16 files: `Orders/Views/OrderInvoice.cshtml` (18), `Marketplace/Views/VendorDashboard.cshtml` (10), `Bookings/Views/BookingsAdmin/Dates.cshtml` (6), `ContactCenter/Views/ContactCenterExternalTransferSettings.Edit.cshtml` (5), `ContentTransfer/Views/Admin/Import.cshtml` (5), `Workforce/Views/TimeOff/Worker.cshtml` (5), `Create.cshtml` (4), `Fundraising/Views/AccountDonations/Index.cshtml` (4), `ContactCenter/Views/AgentEntitlements/_EntitlementFields.cshtml` (3), `Receipts/Views/ReceiptDocument.cshtml` (2), `Reports/Views/Shared/_ReportSection.cshtml` (2), `_ReportWidgetSections.cshtml`, `Bookings/Views/BookingsAdmin/Calendar.cshtml`, `Inventory/Views/PurchaseOrdersAdmin/Edit.cshtml`, `Orders/Views/DraftOrdersAdmin/Edit.cshtml`, `Subscriptions/Views/InstallmentPlans/Create.cshtml` | `scope="col"` or `scope="row"` |
| Modals without `aria-labelledby` | `Calendars/Views/Calendar/Index.cshtml:134,152,171`, `CalendarEvents/_ChooseCalendar.cshtml:17` | Point at the modal title's id |
| Inputs with no associated label | About 200 heuristic hits (some false): Omnichannel.Managements 34, AI.Chat 31, AI 25, Taxation 13, AI.Mcp 10; worst `AI/Views/AIToolInstanceParameters.Edit.cshtml` (20), `Omnichannel.Managements/Views/BulkActivityActions.cshtml` (9) | `<label for>` or `aria-label`; confirm each hit |
| New messages not announced | `Omnichannel.Messaging/Views/Admin/_Thread.cshtml` has no live region (cart, soft phone, agent bar and checkout errors do) | `aria-live="polite"` on the thread container |

Sound and staying: no `<img>` without `alt`; no clickable `div`.

**Tests.** A view lint test for `id="search-box"` without `aria-label`, `<th` without `scope`, and `class="modal"` without `aria-labelledby`. Run axe through Playwright on six representative pages (an admin list, an admin form, checkout, cart, the agent workspace, the messaging workspace) and record the baseline.

## UX-6. Consistency (Low to Medium, S each)

| Item | Evidence | Change |
| --- | --- | --- |
| Status colors differ for the same meaning | Failed: `text-bg-warning` (PhoneNumbers.Verifications ×3, `Subscriptions/Views/Items/TenantProvisioningJob.Status.SummaryAdmin.cshtml:12`), `bg-danger-subtle` (DataConnectors ×2, `AI.FileSources/Views/Items/FileSource.LastRun.SummaryAdmin.cshtml:20`), `text-bg-danger` (`Transactions/Views/Items/PaymentAttempt.State.SummaryAdmin.cshtml:10`), `bg-danger` (`Transactions/Views/Shared/_TransactionStatusBadge.cshtml:12`). Canceled: `text-bg-danger` (Bookings ×2), `bg-warning-subtle` (DataConnectors ×2), `bg-dark` (Transactions) | One `StatusBadge` shape in Resources with a fixed tone vocabulary (`success`, `danger`, `warning`, `info`, `neutral`) and an icon so color is not the only signal; each module maps its statuses to a tone |
| Empty states | 122 lists say "Nothing here!"; 12 use other wording; 104 of 153 single-line `<admin-list>` tags pass no `empty-message`; about 59 hand-written "No ..." blocks (20 `alert`, 39 muted text) | Every `<admin-list>` passes an `empty-message` that names the record and the next step ("No coupons yet. Create one to offer a discount."); hand-written blocks inside detail pages use one `EmptyState` shape |
| Validation summary | 136 views use `@Html.ValidationSummary()`; 41 use the tag helper (Assets, Customers, Geo, Inventory, Marketing, Orders gift cards, TimeTracker, WorkOrders settings, Workforce.ContactCenter and others) | Both render the same summary, so converting 41 views is churn. State the rule for new views in the admin-edit skill (`@Html.ValidationSummary()` at the top of the form) and convert a view only when it is edited for another reason |
| Other confirm patterns | 18 views and 3 scripts call `window.confirm` (AI.Chat.Copilot ×3, AI.Documents ×3, `Calendars/Views/CalendarEvents/_EditEventForm.cshtml:81`, `ContactCenter/Views/SupervisorDashboard/Index.cshtml:415`, `Omnichannel.Managements/Views/ActivityBatches/_SaveAndLoadButton.cshtml:53`, `Stripe/Views/StripeSettings.Edit.cshtml:329`, `Telnyx/Views/TelnyxSettings.Edit.cshtml:334`, `TenantHierarchy/Views/Platform/Parent.cshtml:123,152`, `AI.Chat.Interactions/Assets/js/profile-builder.js:787`, `Reports/Assets/js/report-designer/designer-app.js:630`, `designer-history.js:356,464`) | Admin pages use the TheAdmin dialog through one `confirmAction(title, message)` helper in Resources (UX-8) |
| Unlocalized strings | 18: `aria-label="Close"` in 12 index views (AI ×4, AI.DataSources, AI.FileSources, AI.Mcp ×2, AI.WebCrawlers, Omnichannel.Managements ×2, Taxation); `AI.Chat/Views/Items/AIProfile.DeleteChatHistoryActionsMenu.SummaryAdmin.cshtml:13`; `AI.Chat.Interactions/Views/ChatInteractionHeader.cshtml:22`; `AI/Views/AIToolInstanceParameters.Edit.cshtml:106,244`; `Wizard/Views/WizardPartSettings.Edit.cshtml:42`; `Calendars/Views/Shared/_CalendarEventActivityFields.cshtml:30`; scripts `Omnichannel.Managements/Assets/js/activity-query-builder.js:143`, `Telephony/Assets/js/phone-field-dialer.js:42` | `T[...]`; scripts take strings from a server-rendered `data-*` or JSON block |
| Bootstrap 4 classes | 8 lines: `text-right` in `Omnichannel.Managements/Views/ContactNavigationAdmin.cshtml:55`, `ListPartNavigationAdmin-Account.cshtml:43`, `ListPartNavigationAdmin-OmnichannelContact.cshtml:47`; `font-weight-normal` in `OmnichannelChannelEndpoint.Link.cshtml:27`, `OmnichannelContactPart.Edit.cshtml:47,64,81` | `text-end`, `fw-normal` |
| jQuery in a jQuery-free admin | `Wizard/Views/WizardPart.Edit.cshtml:20` (jQuery UI sortable, 13 calls), `AI/Views/AIProfileTemplateFields.Edit.cshtml:71-72`, unused dependency in `ContactCenter/Views/MyVoicemailGreeting/Index.cshtml:66`, `Taxation/Views/TaxTableFields.Edit.cshtml:91`, `Users/AvatarResourceManagementOptionsConfiguration.cs:20` | Remove the unused dependencies; replace sortable with SortableJS (already in Orchard's admin); `bootstrap-select` stays (`AGENTS.md` requires it) |
| Dead script | `Stripe/Assets/js/stripe-subscription-payment-processing.js` is registered as the resource `stripe-subscription-processor` (`Stripe/Services/ResourceManagementOptionsConfiguration.cs:23-26`, `Stripe/Assets.json:4`) and used by no view in this repository. Its POSTs send no antiforgery token | The resource name is public: a site's own theme may require it. Mark it deprecated in the changelog for one release, then delete the file, the resource definition and the asset entry |

## UX-7. Inline scripts and styles (Low, L, incremental)

179 inline `<script>` blocks in 168 views (blocks with a body; `<script src>` and `<script asp-name>` tags are not counted), 51 inline `<style>` blocks, 74 inline `on*=` handlers, and two scripts written from C# (`Telephony/Services/PhoneFieldDialerShapeTableProvider.cs:87`, `Omnichannel.Messaging.Sms/Services/SmsPhoneFieldButtonShapeTableProvider.cs:84`). They cannot be linted, tested, cached or covered by a strict Content Security Policy, and they are where the string-interpolation problems in UX-1 and SEC-FE come from.

By script lines: AI 1,291 (26 files), Omnichannel.Managements 1,000 (18), AI.Documents 782, AI.Prompting 701, ContactCenter 669, AI.Chat 650, AI.Chat.Copilot 435, TenantHierarchy 348, AI.Chat.Interactions 290, Workforce 220.

Start with the largest, each becoming a file under the module's `Assets/js/` registered as a resource and configured through `data-*` attributes or one JSON block (`@Html.Raw(JsonSerializer.Serialize(...))`, the form about 70 views already use):

1. `AI.Documents/Views/ChatInteractionDocuments.Edit.cshtml` (343 lines)
2. `AI.Chat/Views/AIProfilePostSession.Edit.cshtml` (311)
3. `AI.Prompting/Views/PromptTemplateSelection.Edit.cshtml` and `PromptTemplateChatInteractionSelection.Edit.cshtml` (285 each and nearly identical: one script)
4. `ContactCenter/Views/SupervisorDashboard/Index.cshtml` (252)
5. `Omnichannel.Managements/Views/OmnichannelActivityBatchFields.Edit.cshtml` (204)
6. `AI.Documents/Views/AICompletionWithConfigDocuments.Edit.cshtml` and `AIProfileDocuments.Edit.cshtml` (203 each: one script)
7. `AI.Chat/Views/AIProfileChatMode.Edit.cshtml` (171)
8. `AI/Views/AIDeploymentPricing.Edit.cshtml` (156)

Then stop localized text reaching JavaScript string literals: 59 lines in 22 views use `'@T["..."]'` inside `<script>` (an apostrophe shows to the user as `&#x27;`). Worst: `AI.Documents/Views/ChatInteractionDocuments.Edit.cshtml` (13), `ContactCenter/Views/MyVoicemailGreeting/Index.cshtml` (10), `AI/Views/AIModelParameters.Edit.cshtml` (6), `Omnichannel.Managements/Views/CadenceSteps.Edit.cshtml` (5), `Checkout/Views/CheckoutPaymentMethods.cshtml` (3), `Subscriptions/Views/InstallmentPlans/Pay.cshtml` (3).

**Guard.** A lint test that counts inline `<script>` lines per view with today's numbers as a ceiling that only drops, and bans `'@T[` and `"@T[` inside `<script>`.

## UX-8. One front-end toolkit in Resources (Medium, M to L)

The same small helpers are written in many modules, with different behavior:

| Helper | Copies | Note |
| --- | --- | --- |
| `escapeHtml` | 9 to 13 definitions in modules, by three counts (named functions and other forms; among them `AI/Views/AIDeploymentPricing.Edit.cshtml:125`, `AIPersonaFields.Edit.cshtml:146`, AI.Documents ×3, AI.Prompting ×2) and more in sample hosts | Two behaviors: the `textContent` form (`Telephony/Assets/js/telephony-client.js:20-25`) does not escape quotes, so it is unsafe in an attribute (SEC-FE); `ContactCenter/Assets/js/shared/supervisor-actions.js:40-47` escapes all five characters |
| `format('{0}')` | 14 (`Telephony/Assets/js/soft-phone/` ×5, ContactCenter ×5, Dispatch ×2, `Calendars/Assets/js/calendar-reminders.js:26`, `ContentTransfer/Assets/scripts/content-transfer-import.js:19`, `AI.Chat.Interactions/Assets/js/profile-builder.js:54`) | |
| Antiforgery token lookup and fetch wrapper | 14 files look the token up; 9 wrappers (`post`, `postJson`, `request`); 83 raw `fetch(` calls in 50 files | Some POSTs send no token (SEC-FE) |
| Debounce | 1 helper (`Reports/Assets/js/report-designer/designer-ui.js:132`) and 18 hand-rolled `clearTimeout` | |
| Toast | `Omnichannel.Messaging/Assets/js/messaging-notifications.js:70` and `messaging-workspace.js:737` | Copies of each other |
| `formatFileSize`, `formatDuration` | 3 (AI.Documents views); `ContactCenter/Views/SupervisorDashboard/Index.cshtml:382` disagrees with `Telephony/Assets/js/shared/call-timer.js:15` past one hour | A supervisor and an agent see different durations for the same call |
| Type-ahead pickers | `Resources/Assets/js/item-selector.js` is the shared picker; own implementations in `Omnichannel.Messaging/Assets/js/messaging-contact-picker.js:113`, `Reports/Assets/js/report-designer/designer-sharing.js:142`, `Addresses/Assets/js/address-editor.js:433` | |
| Map labels | `Resources/Assets/js/crestapps-map.js:71,92` treats labels as HTML (SEC-FE) | |

**Change.** A `crestapps-browser` script resource in `Resources/Assets/js/browser/` (not "crestapps-core": that name would be confused with the vendored CrestApps.Core assets), one ES module per helper, exposed as `window.crestapps`:

```js
crestapps.html.escape(value)            // & < > " '
crestapps.text.format(template, ...args)
crestapps.http.request(url, { method, json, form, signal })  // adds the antiforgery header, throws a typed error on a non-2xx, reads Retry-After
crestapps.timing.debounce(fn, wait)
crestapps.notify.toast(message, tone)
crestapps.confirm(title, message)       // TheAdmin dialog in admin, window.confirm elsewhere; also binds [data-confirm] and [data-once]
crestapps.format.fileSize(bytes), .duration(seconds), .dateTime(value, options)  // dateTime uses the user's zone from <body data-time-zone>
```

Rules: `Telephony` keeps `window.telephonyClient.escapeHtml` as an alias to the shared helper for one release (Contact Center calls it across modules today); every module that uses a helper declares the resource dependency; Vitest covers each helper, including a quote in `escape` and one hour in `duration`.

Order: SEC-FE1, SEC-FE2 and the admin half of UX-1 are fixed locally first and do not wait for this item. Then ship the toolkit with `escape`, `request` and `confirm`, fold those local fixes into it, move Telephony, Contact Center and Omnichannel.Managements, then the rest as UX-7 extracts each inline script. The three hand-written type-ahead pickers move to `item-selector.js` when their views are next touched.

**Guard.** Lint tests: no `function escapeHtml` outside `Resources/Assets/js/browser/`; no `RequestVerificationToken` lookup outside it; a ceiling on raw `fetch(` calls that only drops. Add the toolkit to `building-blocks.md` (Platform infrastructure: "Browser helpers") and to `AGENTS.md` *Frontend Best Practices*.

## Vendored files (not edited in this repository)

`AI.Chat/wwwroot` `ai-chat*.js` and `.css`, `AI.Chat.Interactions/wwwroot/scripts/chat-interaction*.js`, `AI.WebMcp/wwwroot/scripts/webmcp*.js`, and `ai-chat-ui-components` in Resources come from `@crestapps/ai-chat-ui` and `@crestapps/webmcp` (CrestApps.Core). Third-party copies in Resources: Chart.js, DOMPurify, EasyMDE, flatpickr, highlight.js, intl-tel-input, medium-zoom, web-noise-suppressor. Check `Assets.json` for a `copy` entry before editing any file under `wwwroot`. Problems found in these are reported to the owning repository.

## Checked and sound

- All 84 `Html.Raw` uses were read. The JSON ones use the default System.Text.Json encoder, which escapes `<`; `JsonLd.ToScriptJson`, `ReportDesignerJson.Options` and the embedded checkout configuration are safe.
- No Liquid templates render user text raw; SMS bodies, AI call transcripts, offers, history and supervisor messages are encoded or set with `textContent`. The vendored chat UI sanitizes Markdown with DOMPurify.
- `postMessage` handlers check the origin; no tokens in `localStorage`; no `eval`, `new Function` or `document.write`.
- Checkout card fields are Stripe Elements loaded from `js.stripe.com`; other CDN resources carry integrity hashes, except the one in SEC-FE.
- Password inputs and breadcrumbs are covered by existing lint tests.
