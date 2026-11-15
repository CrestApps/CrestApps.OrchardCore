# Ticket intake: categories and questions that follow the answers

Status: approved 2026-11-15 with the recommended option of every decision in section 9. Building on branch `ma/pensive-jackson-c56dcb` (on top of `ma/tickets-phase-2`); progress in section 11.

Builds on the Tickets module ([`ticket-support-plan.md`](ticket-support-plan.md), branch `ma/tickets-phase-2`, phases 1 to 4 done) on Orchard Core 4 preview-19225 and .NET 11.

## 1. Goal

A help desk owner sets up how a ticket is opened, without code:

- **Categories with sub-categories** (Hardware > Laptop > Won't start). The category a requester or agent picks sends the ticket to the right desk and team, with the right priority, service-level policy and skills.
- **Questions that follow the answers.** "Which laptop model?" shows only for Hardware > Laptop; "Is anyone else affected?" shows only when the answer to "Is this an outage?" is Yes. A wizard-like experience, where each answer can bring up the next questions.
- The same setup works in the agents' **New ticket** page, the **portal**, the **web form widget**, the API, recipes and deployment, and reports.

## 2. What exists today

| Piece | Today | Gap |
| --- | --- | --- |
| Ticket types | Any content type with `TicketPart`; the owner adds Orchard content fields per type. `TicketPartSettings` gives a new ticket its desk, team, priority, policy and skills | The type is the only routing hub. One type per problem is the only way to route by problem |
| Customer fields | `TicketFieldCustomerSettings.CustomerEditable` per field. `TicketCustomerPlacementProvider` hides the other editors in the portal and `TicketCustomerEditor.KeepOnlyCustomerValues` puts them back after the post, so a forged value is dropped | Nothing hides a field because of another answer |
| Portal | `account/requests/new`: service catalog of types marked **Customers can submit it**, then the type's customer editor | No category step |
| Web form widget | `TicketFormPart` fixes the type and desk; the form asks only for name, e-mail, subject and body | The type's own fields never show on the widget |
| Categories | Orchard Taxonomies (`TaxonomyField` with `Unique`, `LeavesOnly`, `Open`; terms are content items of a term type, nested) | Not used by Tickets; nothing routes on a term |
| Wizard | `WizardPart`, `IWizardEngine`, `WizardSession`: ordered steps fixed when the session starts, each content step collects **its own** response content items, completion under a lock, resume by cookie, public host at `Wizard/...` | No conditional steps; no "one content item filled across steps" mode |
| Conditions | Work Orders checklists: `ChecklistQuestion.ShowWhen` (`ChecklistCondition { QuestionId, EqualsValue }`), evaluated in `WorkOrderService` | A private copy. Tickets would be the second one |

The Work Orders `ShowWhen` matters for where the new rules live (section 5): by the reuse ladder, the second copy is the trigger to extract a building block.

## 3. Categories

### 3.1 Options

| | A. One ticket type per problem | B. A category on the Ticket part, from a taxonomy (recommended) | C. A plain `TaxonomyField` the owner adds | D. A Tickets category record |
| --- | --- | --- | --- | --- |
| How | Keep today's model: "Laptop problem", "Access request"... each with its defaults and fields | `TicketPart.CategoryId` holds a term of the taxonomy the type names (`TicketPartSettings.CategoryTaxonomyId`). Term types carry a `TicketCategoryPart` with the routing | The owner adds a `TaxonomyField` to the type; Tickets settings name which field is the category | A catalog tree of categories in Tickets |
| Hierarchy | None | The taxonomy tree | The taxonomy tree | Its own tree |
| Routing per category | Per type only | On the term, inherited by sub-categories | Needs the same part on terms; Tickets must find the field by name on every type | Its own editor |
| API, workflows, e-mails, AI tools, index | Already there (the type) | One known place: `TicketPart` and `TicketIndex` | Each must look up "the category field" of the type | One place |
| Recategorize an open ticket | Impossible in practice (changing content type loses fields) | A property change, on the timeline | A field edit | A property change |
| Scales to 50 problems | No: 50 types, 50 forms, a long portal catalog, reports by type only | Yes | Yes | Yes |
| Reuse | Use | Use Taxonomies, extend with a term part | Use | Duplicates Taxonomies (the catalog says categories are Taxonomies) |

**Recommendation: B.** It keeps categories in Orchard Taxonomies (editable, translatable, deployable as content, with its own permissions) and gives every Tickets path one place to read and set the category. Types keep their role for really different requests (an IT request versus a customer complaint, with different fields and approvals), and categories sort the problems inside a type. Option A keeps working for owners who want it; nothing forces a category.

### 3.2 The model

- **The taxonomy.** `TicketPartSettings` gains **Category taxonomy** (`CategoryTaxonomyId`), **A category is required**, **Only the most specific category** (leaves only, like `TaxonomyFieldSettings.LeavesOnly`) and **Customers must choose a category**. Several types may share one taxonomy. The feature seeds a `TicketCategory` term type and an empty **Ticket categories** taxonomy using it; the owner may point a type at any taxonomy whose term type carries `TicketCategoryPart`.
- **`TicketPart.CategoryId`**: the term's content item id, or none. Set at creation (request, portal, web form, API, workflows, AI tools) and changed later by agents with Edit, through `ITicketService` like any other property, with change key `Category` (`TicketChangeKeys`) on the timeline, in workflows and webhooks.
- **`TicketCategoryPart`** (on the term type, edited in the taxonomy editor, every value optional):

| Value | Use |
| --- | --- |
| Desk | Where a new ticket of the category goes, checked against the desks the type is offered on and the requester's audience (a customer never lands on an internal desk through a category) |
| Team, priority, service-level policy, required skills | Defaults of a new ticket |
| Customers can choose it | Shown in the portal and web form; others are for agents only (Hardware > Server room) |
| Description for customers | Help text under the picker |

- **Inheritance.** A sub-category takes each value it leaves empty from its nearest ancestor that sets it (`ITicketCategoryResolver.ResolveAsync(termId)` returns the effective values and the path). "Customers can choose it" also inherits: hiding Hardware hides its sub-categories.
- **Order of defaults for a new ticket:** what the request says, then the category, then the ticket type, then the desk. **Service-level policy:** account, then category, then type, then desk (the account's policy is an entitlement, so it still wins). Both are decision 3.
- **Recategorizing later** changes the category only: desk, team, priority and timers stay. When the category's routing differs from the ticket's, the properties panel offers **Apply the category's routing**, which goes through the same checks as a manual change. Owners who want it automatic use a workflow on Ticket Updated with the `Category` change key. Decision 4.

### 3.3 The picker

Orchard's taxonomy editor shows the whole tree as radio buttons, which doesn't suit 50 categories or a customer. The picker is a **cascading select**: Category, then Sub-category, then the next level, each list filled from the chosen term's children, with search when a level is long. It is generic, so it belongs to the taxonomy owner's side, not Tickets:

- a **Cascading** editor for any `TaxonomyField` (a new editor option, in `CrestApps.OrchardCore.ContentFields`), and
- the same view component in `ContentFields.Core`, which the Ticket part's editor uses for `CategoryId`, filtered to terms customers can choose when the editor is the customer's.

No jQuery: plain JavaScript over a small JSON endpoint of the taxonomy's tree, checked with the taxonomy's view permission (anonymous for terms marked for customers, through the widget's own access).

## 4. Questions that follow the answers

### 4.1 Where a question lives

A question is a content field of the ticket type, as today. A field gains a **Show this field when** rule in its settings:

```text
Show when   [all | any] of:
  [Category]            [is or under]  [Hardware > Laptop]
  [Is this an outage?]  [is]           [Yes]
```

- A condition reads a **source**: another field of the same item (by part and field name), or a value a module provides. Tickets provides **Category** (with "is or under", so a rule on Hardware covers its sub-categories), **Desk** and **Channel**.
- Operators: is, is not, is one of, is none of, is empty, is not empty, contains, greater than, less than, and "is or under" for sources with a hierarchy. Values are kept as invariant strings, the way the report filters keep them.
- A field whose controlling field is hidden is hidden too, so a chain of questions closes when an early answer changes. The settings editor refuses a cycle (A shows when B, B shows when A) and lists the fields a rule depends on.
- A field's **Required** setting counts only while it shows.
- A rule pointing at a field that was removed or renamed is ignored (the field shows) and the type's editor warns about it, so a definition change never silently hides a required field.

Rejected: a question list per category stored on the term (answers in a `TicketPart` dictionary). That is a second custom-field engine, which the Tickets plan's decision 1 ruled out; answers would also miss the `Contents` data source, the field editors and the field indexes.

### 4.2 Evaluated on the server

The browser only makes the form pleasant. The server decides:

1. The editor is read as usual (`UpdateEditorAsync`).
2. The evaluator works out, in dependency order, which fields show for the values just read.
3. For each hidden field: its validation errors are removed and its value is put back as a new item has it (on create) or as the stored item has it (on edit). A posted value for a hidden field is never kept, the same guarantee as `KeepOnlyCustomerValues`.
4. Display shapes (the agents' ticket page, the portal's request page) skip hidden fields.

This runs in a content display handler that follows Orchard's display coordinator, so it works in **every** editor of the type, Orchard's own content editor included, not only in the pages Tickets calls. Phase 0 proves the ordering on OC 4 (section 8).

On an **edit**, a field hidden by a new answer keeps its stored value (decision 6): nothing is lost if the answer goes back, the display hides it meanwhile, and the timeline records the category or answer change that hid it.

### 4.3 In the browser

Fields with a rule get a wrapper (through placement, so no field view changes) carrying the rule as data attributes. A small script reads the controlling inputs and shows or hides the wrapper (`hidden` and `disabled`, so hidden inputs don't post). Reading a value needs to know each field type's inputs, so the script has a reader per field type (text, boolean, numeric, date, enumerations of text fields, taxonomy, content picker, multi-text, phone, address); a module adds a reader for its own field type. A field whose controlling input has no reader simply shows; the server still drops what it must. Without script, every candidate field shows and the server keeps only what applies.

### 4.4 One dynamic form or steps

| | One dynamic form (recommended now) | Wizard steps | Client-side pages of one form |
| --- | --- | --- | --- |
| Feel | Answer the category, the right questions appear underneath | One page per step, Next and Back, steps skipped by rules | Same look as the wizard, one post |
| Owner | The rule building block | **Wizard**, which owns flows | Nobody: a hand-rolled step flow, which the catalog forbids |
| Needs | Section 4.1 to 4.3 | Conditional steps in Wizard (a step's rule on the same building block), and a "one content item across steps" mode (the session holds the draft ticket; completion creates it through `ITicketService` under the engine's lock) | Paging script |
| Resume later | No | Yes (session cookie, signed-in sessions) | No |
| Admin editor, portal, web form | Same form everywhere | The portal and web form send the requester to the wizard host; agents keep the dynamic form | Portal and widget only |
| Recipes | Rules travel with the content definition | Plus the wizard definition | Rules plus page breaks |

**Recommendation:** ship the dynamic form first. It covers category then sub-category then the matching questions, which is most of the wizard feel, in every editor. Add Wizard steps later only if owners need long intake forms with resume (onboarding, equipment requests): then Wizard gets conditional steps and the one-item mode, on the same condition building block, and Tickets registers a wizard definition per type that asks for it (decision 7).

### 4.5 Admin editor, portal and web form

- **Agents' New ticket and Edit fields.** The type's whole editor, with the category picker at the top of the Ticket part's editor and the rules applied. Agents see internal fields as they do today; rules decide relevance, not audience.
- **Portal.** Service catalog of types as today (when there is only one, it is skipped), then the customer's editor: subject, the category picker (customer categories only), then the customer fields with their rules, then the description and files. `TicketCustomerEditor` adds the category to what it shows; the rule handler runs after `KeepOnlyCustomerValues`, so a value survives only if it is both a customer field and shown. Portal entry by categories across types instead of types first is decision 10.
- **Web form widget.** Renders the same customer editor for its type (category and customer fields with their rules) above name, e-mail and body, and the post goes through the same steps. The widget stays within its 64 KB post limit; it takes no files. Decision 8.
- **Field types customers may fill in.** Some editors call admin endpoints (content and user pickers). The field settings editor warns when such a field is marked **Customers fill this in**, and the plan's docs list the safe field types.
- **API, workflows, AI tools.** `POST tickets` and `PATCH tickets/{id}` take `categoryId`; the Create Ticket and Update Ticket tasks and the `createTicket` and `updateTicket` tools set it. Field values set through these paths are not checked against rules (they are trusted callers, as content fields are today); hidden fields simply don't display.

## 5. Reuse: who owns what

| Concept | Owner | Ladder step | Notes |
| --- | --- | --- | --- |
| Categories and their tree | Orchard Taxonomies | 1 Use | Taxonomy content items, term types, permissions, localization |
| Routing per category | Tickets, as `TicketCategoryPart` on the term type | 2 Extend | A content part on Orchard's term type; Tickets' own concept |
| Cascading taxonomy picker | **ContentFields** (a `TaxonomyField` editor) and `ContentFields.Core` (view component) | 3 Add to the owner | Any taxonomy field can use it |
| Condition model and evaluator | New, in the platform libraries: `Conditions` in `CrestApps.OrchardCore.Abstractions` (`Condition`, `ConditionGroup`, operators, `IConditionValueSource`) and `CrestApps.OrchardCore.Core` (`IConditionEvaluator`) | 4 Extract | Not tied to content: Work Orders checklists (a catalog record), content fields and Wizard steps all evaluate rules over named values. Platform namespace like `Records` and `Tabular`, since it carries no domain type |
| Work Orders `ChecklistCondition` | Moves onto the shared model | 4 (migrate the first copy) | Data migration of checklist templates: `QuestionId`/`EqualsValue` becomes a condition with "is"; the recipe step still reads the old shape. Same plan, as the rules require |
| Show-when rules on content fields | **ContentFields**: a **Conditional Fields** feature (field settings, settings editor, display handler, placement wrapper, script, field value readers) | 3 Add to the owner | Works for any content type; Tickets is the first consumer, not the owner |
| Ticket condition sources (category, desk, channel) | Tickets | 2 Extend | `IConditionValueSource` registrations |
| Conditional steps, one-item flows | **Wizard** | 3 Add to the owner | Only if decision 7 picks steps |
| Field value reading | Orchard indexing's content field handlers, if they give a usable value per field; otherwise readers per field type in ContentFields.Core | 1 or 3 | Checked in phase 0 |

Not reused on purpose: Orchard Core's `DataFilterOperator` (from Data Sources) would put a Data Pipelines dependency under every form; the condition operators are a small set of their own, named the same way. Orchard's Rules module (layer rules) evaluates request conditions (URL, role, culture), not item values.

`docs/engineering/building-blocks.md` gets rows for Conditions and Conditional Fields, the cascading taxonomy editor under Content fields, and, if built, Wizard's conditional steps.

## 6. Data

### 6.1 Storage and index

- `TicketPart.CategoryId` (nullable). No name copy: names come from the taxonomy (cached), so a rename shows everywhere at once.
- `TicketIndex`: `CategoryId` and `CategoryPath` (`/root/child/leaf/`, ids; at most 255 characters, so 9 levels of 26-character ids). `CategoryPath LIKE '/x/%'` finds a category and everything under it. Tickets migration v12 adds both columns with an index; the path is kept current by a handler when a ticket's category changes. A term moved inside the taxonomy changes the path of its tickets: a resumable step re-maps them (the pattern `TicketSettingsChangesStep` already uses).
- `TicketBacklogDay` gains a by-category breakdown from the day it ships (past days can't be rebuilt; they show without it).
- A deleted term: tickets keep its id and show "Removed category"; reports group them under that label. Orchard has no guard against deleting a term in use, so the taxonomy editor can't refuse it; a warning on the term type's part editor names how many open tickets use the term.

### 6.2 Filters, views, API

- Ticket list: a **Category** picker (the cascading picker) that includes sub-categories; `TicketQuery.CategoryId` with subtree matching; saved views keep it.
- API: `GET tickets?categoryId=` (with sub-categories); `TicketApiModel.category { id, name, path }`.
- Find Tickets task and the `searchTickets` tool take a category.

### 6.3 Workflows, webhooks, e-mails, AI

- `Workflow.Input` gains `CategoryId` and `CategoryPath`; every ticket event can be narrowed to categories (with sub-categories), as to desks today.
- Update Ticket task: **Category**; `TicketChangeKeys.Category` in Ticket Updated's changed-field filter.
- Webhooks carry the category in `TicketApiModel`.
- E-mail templates get `Ticket.Category` (the path's names, "Hardware > Laptop").
- `listTicketOptions` lists the categories a user may pick. AI triage suggesting a category (from existing terms only) is a later option, bounded like the priority suggestion.

### 6.4 Data sets and reports

- `Tickets` data set: `CategoryId`, `Category` (the leaf's name), `CategoryPath` (names), and `CategoryLevel1`, `CategoryLevel2`, `CategoryLevel3` for grouping.
- New `TicketCategories` data set: one row per term (id, name, parent, level, path, effective desk, team, priority and policy), so the Report Builder can join and group.
- Answers to questions are content fields, so they are already in the `Contents` data set (the `Tickets` set's `TicketId` references the type there). Because hidden fields are dropped on create, answers that don't apply are empty rather than stale.
- **Ticket Distribution** report: by category (top level, then the next level). The other ticket reports get a category filter.

### 6.5 Migration of existing tickets

None is needed. Existing tickets have no category ("No category" in lists and reports) and their fields have no rules, so every field shows as today. Owners who built one type per problem keep it; moving such types into categories is a manual choice (their tickets can't change content type), so the plan ships no converter. Index migration v12 is additive.

## 7. Recipes and deployment

- **Rules** are field settings, so they travel with Orchard's `ContentDefinition` step. CrestApps Recipes gets a schema for the conditional field settings.
- **Categories** are a taxonomy content item, so they travel with the `Content` step, with `TicketCategoryPart` values on the terms. Desk, team, priority and policy ids are the records' `ItemId`s, which the Tickets steps already keep stable across sites. A term naming an id the target site doesn't have keeps its other values; the value is ignored and logged on import, and the taxonomy editor shows it as missing.
- `TicketPartSettings.CategoryTaxonomyId` is the taxonomy's content item id, stable across sites when the taxonomy is deployed with its id.
- The **Help desk starter** recipe gains a sample **IT categories** taxonomy (Hardware > Laptop, Printer; Software > Access request; Network) with routing on Hardware, and an **IT request** type with a few conditional fields ("Laptop model" for Hardware > Laptop, "Who else is affected?" when "Is this an outage?" is Yes).
- Work Orders checklist templates keep importing old recipes (`ShowWhen` with `QuestionId`/`EqualsValue` is read and converted).

## 8. Phases

One branch and one pull request for the plan, a commit per slice (decision 9 says whether it is the Tickets PR or its own).

| Phase | Contents | Done when |
| --- | --- | --- |
| 0. Spike (short) | Prove on OC 4: a content display handler after the coordinator can remove a hidden field's validation errors and reset its value, in Orchard's content editor and in a controller's `UpdateEditorAsync`; whether Orchard's indexing field handlers can serve as value readers; a placement wrapper on field editors. Also check how the portal treats a **required** field customers don't fill in (it may block the post today) | A short note in this plan with the chosen hooks |
| 1. Conditions building block | `Conditions` in the platform Abstractions and Core, evaluator with dependency order, cycle detection and "is or under"; Work Orders checklists moved onto it (migration, recipe read of the old shape) | Unit tests; Work Orders checklist tests pass unchanged; checklist browser check |
| 2. Conditional Fields (ContentFields) | Field settings and their editor (source, operator, values pickers; warnings for missing sources), the display handler (validation and values), display filtering, the placement wrapper, the script with readers for Orchard's and CrestApps' field types, recipe schema, docs, catalog row | Unit tests of the handler (forged hidden value dropped, required only when shown, chain closing, edit keeps stored value); browser check in Orchard's content editor on a non-ticket type |
| 3. Ticket categories | `TicketPartSettings` values, `TicketPart.CategoryId`, `TicketCategoryPart` and term type, `ITicketCategoryResolver`, routing at creation, **Apply the category's routing**, the cascading picker (ContentFields editor and view component), the Tickets condition sources, index migration v12 and path re-map step, list filter and views, API, workflows, webhooks, e-mails, AI tools, data sets, reports, backlog breakdown, starter recipe, docs | Unit and SQLite tests; browser check of agent create and edit, portal create with a forged hidden field, list filter, a workflow narrowed to a category |
| 4. Web form | The widget renders the type's customer editor with category and rules; the post reads it like the portal | Browser check as a guest and signed in; trap and rate limits unchanged |
| 5. Wizard steps (only if decision 7 says so) | Wizard: step conditions on the building block, one-item mode; Tickets: a wizard definition per type that asks for steps, completion through `ITicketService` | Resume, Back and Next with skipped steps, completion once under the lock |

Each phase ends with the usual gates: Release build with warnings as errors, unit and architecture tests (layering, recipe schema coverage, additive migrations), JavaScript lint and vitest, docs build, and a browser pass in List and Grid layouts and both action modes for any list touched.

## 9. Decisions for Mike

1. **Where the category lives.** Recommended: B, `TicketPart.CategoryId` from a taxonomy the type names. Alternatives: C, a plain `TaxonomyField` the owner adds; A, keep one type per problem only.
2. **Where category routing lives.** Recommended: `TicketCategoryPart` on the taxonomy's term type, inherited by sub-categories. Alternative: a mapping table under Tickets settings (term to desk, team, priority, policy).
3. **Order of defaults.** Recommended: request, category, type, desk; policy: account, category, type, desk. Alternative: category before account for the policy (a severity category like "Outage" beats a premium plan).
4. **Recategorizing an open ticket.** Recommended: the category changes alone, with **Apply the category's routing** offered and workflows for automation. Alternatives: re-route automatically on every change; never offer it.
5. **Who owns the rules.** Recommended: a shared Conditions building block plus a Conditional Fields feature in ContentFields, with Work Orders checklists moved onto it in the same plan. Alternative: rules private to Tickets (breaks the second-copy rule, since Work Orders already has one).
6. **An answer hidden by a later edit.** Recommended: keep the stored value, hidden from display. Alternative: clear it, with the old value on the timeline.
7. **Steps.** Recommended: one dynamic form now; Wizard steps later if owners ask for long forms with resume. Alternatives: Wizard steps in this plan; client-side pages of one form (not recommended: a hand-rolled flow).
8. **Web form.** Recommended: the widget renders the type's customer fields with category and rules. Alternative: keep subject and body only.
9. **Pull request.** Recommended: its own PR after the Tickets PR merges, since that one is already large. Alternative: add it to the Tickets PR.
10. **Portal entry.** Recommended: types first (skipped when there is one), then the category. Alternative: categories across all customer types first, the type following from the category (each term would then also name its type).

## 10. Phase 0 findings (2026-11-15)

Read from Orchard Core's source (`ContentItemDisplayManager`, `ContentItemDisplayCoordinator`, `ContentFieldDisplayDriver`, the field editor views):

- `UpdateEditorAsync` runs the item-level display drivers (`IContentDisplayDriver`) first, then each part's drivers, then every field's drivers, inside the coordinator, then the other `IContentDisplayHandler`s in registration order. A feature that depends on Contents registers its handler after the coordinator. So the hidden-field step is two pieces: an item-level display driver takes a snapshot of the fields that have rules (before any field driver writes), and a display handler after the coordinator works out what is hidden, puts the snapshot back for those fields and removes their validation errors. The snapshot is the new item's values on create and the stored values on edit, with no second load.
- Field drivers always run their update, whatever the placement says (only a part hidden by placement skips its drivers). Their errors are keyed by the field's prefix, `{TypePartName}.{FieldName}` (with the editor's HTML prefix in front when there is one), such as `Ticket.Model.Text` for a required text field.
- Every Orchard field editor is wrapped in `<div class="ocat-wrapper field-wrapper field-wrapper-{part}-{field}">`, so the script toggles those wrappers and no placement wrapper is needed. Inputs are named under the same prefix (`Ticket.Model.Text`).
- **Bug found by reading, in the Tickets portal:** a required field customers don't fill in still runs its driver, which adds "A value is required" to the model state; `KeepOnlyCustomerValues` puts the value back but not the error, so the request is refused with an error the customer can't see. The hidden-field step fixes it: the customer editor declares its hidden fields through the same extension point (`IContentFieldVisibilityProvider`), so their errors are removed too.
- Value readers: Orchard's indexing field handlers build index documents, not plain values, so ContentFields.Core gets a small reader per field type (registered with an `AddContentFieldValueReader` helper) over the field's stored JSON.
- Work Orders keeps checklist copies on each work order. Instead of rewriting every work order, `ChecklistQuestion` gets the shared rule (`Rule`) and keeps `ShowWhen` (obsolete) for reading: templates are converted by a migration, and a work order's copy is read through the same conversion until it is next saved.

## 11. Progress

| Phase | State | Notes |
| --- | --- | --- |
| 0. Spike | Done | Section 10 |
| 1. Conditions building block | Done, unit tested | `CrestApps.OrchardCore.Conditions` in the platform Abstractions: `Condition`, `ConditionRule`, `ConditionOperator`, `ConditionValue` (with `Withheld` for a hidden source), `ConditionEvaluator`, `ConditionalVisibility` (`GetHidden`, cycle members show, `FindCycle`, `Sources`); `ConditionRuleSchema` in Recipes.Core. The evaluator is static and pure, like `TransitionTable`, so it lives in Abstractions and needs no registration. Work Orders: `ChecklistQuestion.Rule` with `GetRule()` reading the older `ShowWhen`; `WorkOrderService.GetHiddenQuestions` (a question under a hidden one is hidden, which the old check missed); the template handler saves the older form as a rule; migration v6 converts templates after the activation commits (`ChecklistRuleUpgrade`); recipe schema documents `Rule` and keeps `ShowWhen` as deprecated. A rule naming a question that no longer exists is now ignored (the question shows) instead of hiding the question. Not browser checked yet (checked with the Work Orders pass in phase 2) |
| 2. Conditional Fields (ContentFields) | Done, unit tested (C# and vitest); browser pass with phase 3 | Feature `CrestApps.OrchardCore.ContentFields.ConditionalFields` (Contents, ContentTypes). Abstractions: `ConditionalFieldSettings`, `IContentConditionSource` with `ContentConditionSourceDescriptor`, `IContentFieldValueReader`, `IContentFieldVisibilityProvider`, `TaxonomyTermTree`, `ContentFieldsConstants`. Module: `ConditionalFieldsService`, the snapshot driver and the restore handler (section 10), `ConditionalFieldsPlacementProvider` for display, `ConditionalFieldSettingsDisplayDriver` (rows of source, operator and values; lists for sources with options; cycle refused; a removed source named), readers for Orchard Core's and the CrestApps fields, `conditional-fields.js` (same rules as the server, cycle members show, hidden controls disabled), recipe schema fragments per field type. Choice: no `ContentFields.Core` project; Tickets reaches everything through ContentFields.Abstractions, and the cascading picker of phase 3 is a shape |

## 12. Not in this plan

Approval steps per category (today they are per type; a category could later pick them), AI suggesting a category, rules on agent-only values such as status ("show Root cause when solved"), required-when rules beyond "required while shown", calculated fields, and a converter from types-per-problem to categories.
