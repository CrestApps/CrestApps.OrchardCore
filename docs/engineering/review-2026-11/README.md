# Security, reuse and code-quality review plan (November 2026)

Status: findings and plan recorded 2026-11-11 against `main` at `a12102c7a`, then challenged item by item by two independent reviewers and revised (see [`06-review-log.md`](06-review-log.md)). Nothing in this plan is implemented yet. It is written so that an engineer or a coding agent can carry it out item by item without having been part of the review.

This plan continues [`reusability-audit-2026-11.md`](../reusability-audit-2026-11.md) (2026-11-03), which covered duplication and layering. It adds a security review of the whole solution, a design for payment rate limits, a C# structure and reliability review, and a review of views, scripts and admin experience. The binding rules stay in `AGENTS.md`; the catalog of building blocks stays in [`building-blocks.md`](../building-blocks.md).

## Files

| File | Workstream | Item ids | What it holds |
| --- | --- | --- | --- |
| [`01-security-findings.md`](01-security-findings.md) | SEC | `SEC-C1` to `SEC-C18`, `SEC-P1` to `SEC-P10`, `SEC-FE1` to `SEC-FE4`, `SEC-A1` to `SEC-A20` | Security findings rated Critical to Low, each with evidence, exploit, fix, tests and size; tables of anonymous and money-moving endpoints; what was checked and found sound. **These are unfixed vulnerabilities: see its handling note** |
| [`02-payment-rate-limits.md`](02-payment-rate-limits.md) | RL | `RL-0` to `RL-8` | Payment rate limits and card-testing protection as an extension of `OrchardCore.RateLimits`: what exists, the design, the groups and limits, the work items |
| [`03-reuse-and-building-blocks.md`](03-reuse-and-building-blocks.md) | DRY | `DRY-1` to `DRY-12`, `LAY-1` to `LAY-7` | What the earlier audit left open, new duplication and what to extract, layering and coupling |
| [`04-code-quality.md`](04-code-quality.md) | CQ | `CQ-1` to `CQ-12` | C# structure, reliability, data access, error handling, build settings, tests |
| [`05-frontend-and-ux.md`](05-frontend-and-ux.md) | UX | `UX-1` to `UX-8` | Views, scripts, admin conventions, dates, accessibility, shared browser helpers |
| [`06-review-log.md`](06-review-log.md) | | | The independent challenge of this plan by two other models, and what changed as a result |

## How the review was done, and what that means for the implementer

- The solution was read in six slices in parallel: commerce security; AI and data security; communication, workforce and platform security; reuse and layering; C# quality; views and scripts. Payment rate limiting was examined separately. Everything was read-only: nothing was built, run or exploited.
- The Critical and High security findings, the payment rate-limit findings and a sample of the rest were confirmed a second time against the code by the author, and then a third time by two independent reviewers who were asked to disprove them. Every finding cites the file and lines it rests on.
- Findings marked "search pass", "one reading", "to confirm" or with Medium or Low confidence rest on one reading or on a count. **Confirm each one in the code before changing anything.** If a finding does not hold, say so in the pull request and close the item; do not "fix" code that is right.
- Counts are approximate. Where two counts of the same thing differed, the plan gives both. Recount when you start an item.
- Line numbers are for `a12102c7a`. If the file has moved on, find the code by the names given.

## Rules for carrying it out

These repeat the project's rules where they matter most here; `AGENTS.md` and `CLAUDE.md` win where they say more.

1. **Test first.** Every behavior change starts with the failing test named in the item. A security fix whose test cannot be made to fail is not a fix.
2. **One item, one commit; one theme, one pull request.** Keep pull requests reviewable: a building block and its first consumer together, further consumers in follow-ups. The phase 1a fixes are one small pull request each.
3. **Fix locally first, share second.** A security fix never waits for a building block. Where an item says "locally and now", make the small fix in place; the item that extracts the shared piece folds it in later.
4. **Reuse before you build.** Before adding a type, check `building-blocks.md` and section 2 of `03-reuse-and-building-blocks.md`. An item that extracts a building block migrates every listed copy, updates the catalog, and adds the guard that stops the copy from returning.
5. **Layering.** A `.Core` or `.Abstractions` library never references a module. Module code never uses a type from another module assembly. New contracts go in the owner's `.Abstractions`, or in the platform `Abstractions` when they are infrastructure.
6. **Guards are ratchets.** New architecture or lint tests record today's violations in an allowlist that may only shrink.
7. **Public API.** When a public contract changes, update its API baseline in the same commit, and keep an `[Obsolete]` adapter or a type-forward for one release where the plan says so.
8. **Stored data.** A change to a stored shape ships with a data migration and a note in the changelog. Additive first; remove the old shape one release later.
9. **Admin UI.** Lists go through `<admin-list>`; form buttons go in `action-bar-bottom`; times go through `ILocalClock`; strings are localized. Check every touched screen in the running site, in List and Grid layouts where a list changed.
10. **Build and test only what changed.** Build the changed projects and run the tests that cover them; the full build with warnings as errors and the full suite run before a pull request is opened.
11. **Documentation.** Each item updates the module `README.md`, the docs site page under `src/CrestApps.Docs/docs/` and the changelog where behavior, settings or contracts change.
12. **Commits** use the configured human Git author only, with no co-author trailer.

## Order of work

Work in this order. Within a phase, items are independent unless a dependency is named. Phase 1a has no dependencies at all and starts immediately; phase 0 runs beside it and does not block it.

| Phase | Goal | Items | Depends on |
| --- | --- | --- | --- |
| 1a | Small fixes that close the worst gaps, each alone | SEC-C1 commit 1 (amount check at completion); SEC-A1 step 1 (`DefaultTenantOnly`, the run-time check, the permission); SEC-A2 (per-tenant root); SEC-C2 (validation, reserved names, recipe from the plan); SEC-C18 (feature profile required); RL-0 (payment throttle key, closes most of SEC-C7); SEC-P1 (lock and lockout, in place); SEC-A3 step 1 (tool filtering, tenant tools); SEC-A9 step 1 (permission marked security-critical); SEC-FE1 and SEC-FE2 (local escaping); UX-1 part 1 (admin confirmations and double-submit guards) | Nothing |
| 0 | Guards, so nothing gets worse while the rest is done | CQ-8 (build settings, banned APIs for `src`, API baselines for commerce contracts); the ratchets named in CQ-1, CQ-2, CQ-4, CQ-5, CQ-10; DRY-12a, g, h; correct the 2026-11-03 audit's counts | Nothing |
| 1b | The rest of Critical and High | SEC-C1 commit 2 (an edit supersedes the payment); SEC-C3, SEC-C4, SEC-C5; SEC-P2 with CQ-1 step 1; SEC-A6 (egress guard, with the existing helper); SEC-A7; SEC-A8; RL-1, RL-2, RL-4 (the attempt limiter, Checkout's policies, the card-testing gate) | RL-1 before RL-2 and RL-4 |
| 2 | The shared pieces behind the fixes, and the rest of rate limiting | RL-3, RL-5, RL-6, RL-7, RL-8 (other consumers, which moves SEC-P1 onto the shared limiter); DRY-1 consumers; DRY-2 steps 1 to 3; DRY-7 (egress classifiers, HTTP clients, signature validators, the Telnyx client) | Phase 1b |
| 3 | Medium security findings and reliability | SEC-C6, SEC-C8 to SEC-C16; SEC-P3 to SEC-P9; SEC-FE3 (after the owner's decision); SEC-FE4; SEC-A3 step 2; SEC-A4, SEC-A5, SEC-A9 to SEC-A13, SEC-A15 to SEC-A19; CQ-1 steps 2 and 3, CQ-2, CQ-4, CQ-5, CQ-6; the Low items (SEC-A14, SEC-C17, SEC-P10, SEC-A20) | DRY-7 for the remaining egress items (SEC-P4, SEC-P5, SEC-P10b, SEC-P10f) |
| 4 | Admin experience and browser helpers | UX-8 (the shared helpers), UX-1 part 2, UX-2 to UX-6, then UX-7 as files are touched; DRY-10 | UX-8 before UX-7 |
| 5 | Platform DRY and re-layering | DRY-3 (low-risk half), DRY-4, DRY-6, DRY-8, DRY-9, DRY-11, DRY-12; LAY-1 to LAY-7; CQ-3 (internal services now; extension contracts in one announced breaking release), CQ-7, CQ-9, CQ-11, CQ-12 | LAY-1 before LAY-2 |
| 6 | Larger refactors | The rest of DRY-3, CQ-10 splits, the items marked L in section 1 of `03-reuse-and-building-blocks.md`, and DRY-5 once its design note is agreed | Live-call testing for the telephony items |

Items that touch the same code and should be planned together:

- SEC-C2 and SEC-C18 (tenant purchase) with each other, and later with "Tenant provisioning beside the broker" in DRY section 1.
- SEC-C3 with SEC-C17h (both trust an unverified e-mail address).
- SEC-C4 (reverse charge) with "Customer linked to Account" in DRY section 1.
- SEC-P4, SEC-P5, SEC-P10b, SEC-P10f and SEC-A6 with DRY-7 (egress).
- SEC-FE1, SEC-FE2 and UX-1 with UX-8 (the shared browser helpers fold the local fixes in).
- CQ-1 with SEC-P2 (one fix).
- CQ-6 (payment issue recorder) with LAY-5 and LAY-6.
- CQ-11's API error contract with DRY-8 (one item).

## Done means

An item is done when:

- its test failed before the change and passes after;
- every copy or call site the item lists is migrated, or the item says which are left and why;
- its guard is in place with a shrink-only allowlist;
- the changed projects build with warnings as errors, and the tests that cover them pass;
- touched screens were checked in the running site;
- documentation, catalog and changelog are updated;
- the item's line in the tracking table below is updated with the pull request.

## Tracking

Add a row per merged item (id, pull request, date, what was left). Keep this table as the single place that says what is done, as section 0 of the 2026-11-03 audit does for that plan.

| Item | Pull request | Date | Left open |
| --- | --- | --- | --- |
| | | | |

## Decisions the owner needs to make

1. **Urgency of the published findings.** `01-security-findings.md` was published with this plan while its Critical and High items are still open. Decide who takes phase 1a and how soon, and whether to tell operators about interim mitigations (give every sold or self-service tenant a feature profile that excludes "MCP Local Client", MCP resources and data connectors; do not sell tenant plans to buyers you do not trust until SEC-C2 is fixed).
2. **Rate limits and Checkout.** The plan recommends a "Payment Rate Limits" feature that depends on `OrchardCore.RateLimits`, enabled by the store recipes and flagged by a setup step, with the card-testing gate always on. The alternative is a hard dependency of Checkout on `OrchardCore.RateLimits`. See `02-payment-rate-limits.md`, section 6, for this and three smaller decisions.
3. **Secure Capture and card data** (SEC-FE3): provider-hosted fields, or accept and document card-data scope for integrators.
4. **Pay Later** (SEC-C5): whether goods that deliver instantly may be granted before payment, and to whom.
5. **Supervisor scope for AI calls** (SEC-P7): whether queue and campaign scoping is meant to apply to AI calls.
6. **Content Access Control** (SEC-P8): build the missing filters, or document the module as view-page protection only.
7. **Breaking contract changes** (CQ-3): which release adds `CancellationToken` to the extension contracts other packages implement.
8. **Customer and Account** (DRY section 1): the 2026-11-03 audit's largest open refactor; SEC-C4 makes it more pressing.
