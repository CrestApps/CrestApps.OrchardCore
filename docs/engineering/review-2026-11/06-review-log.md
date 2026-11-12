# Review log: the independent challenge of this plan

Part of the [November 2026 review plan](README.md). This file records who challenged the plan, what they checked, what changed as a result, and what is still unverified. It refers to security items by id; the detail is in `01-security-findings.md`.

## Who reviewed, and how

The first draft of files 01 to 05 was challenged on 2026-11-11 by two reviewers working separately, read-only, against the code at `a12102c7a`. Neither wrote the plan, and both were told to assume any item could be wrong.

| Reviewer | Brief | What it opened |
| --- | --- | --- |
| Claude Opus 5.5, high effort | For each item: is it real, is the severity right, is the fix sound and proportionate, is it worth doing, can it be implemented as written. A reasoned opinion on every new building block and on the order of work. Up to a quarter of the effort on hunting for omissions | Every Critical and High security finding, every rate-limit finding and the build-settings item, read in the code; a sample of the Medium items; the Low tables sampled |
| Claude Haiku 5.5, high effort | For each item, in order: open the cited lines and quote the line that proves or disproves the claim; look for a guard the plan missed; check that every named type exists; check cross-references, the phase table and counts | One row per item id, each marked evidence checked, partly checked or not opened; it opened every Critical and High security finding except parts of SEC-A3 and SEC-A5, every rate-limit finding except RL-F3 and RL-F10, and sampled the Low tables; it recounted a set of code counts |

Before either reviewer saw the plan, the author had confirmed the Critical and High findings and the rate-limit findings a second time in the code.

## Outcome in one paragraph

No item was dropped as untrue. Every Critical and High finding that a reviewer opened was confirmed in the code. The reviewers changed how several things are fixed, lowered seven severities, made three more depend on the kind of host, raised none, added three findings, removed one work item and one new module, deferred one building block to a design note, and corrected a set of counts, citations and cross-references. The largest changes: the fix for SEC-C1 was redesigned, the rate-limit building block became platform infrastructure instead of a new module with a hard dependency, and a first phase of small fixes was split out so that nothing urgent waits for shared code.

## What changed

### Fix designs

| Item | Change | Raised by |
| --- | --- | --- |
| SEC-C1 | The amount check at completion ships first and alone, and now also covers providers that report no authoritative amount. "Freeze the invoice and add a release API" was replaced by "an edit cancels and supersedes the live payment attempt", which needs no new engine API and behaves correctly for wallets and redirects | Opus |
| SEC-C2 | The host-name case is stated first, because it needs no assumption about tenant matching order. Moving provisioning onto the tenant broker was split out. A migration keeps custom domains on for sites that already sell them. The fix extends the existing settings class and goes through TenantHierarchy for validation, because Orchard's tenant validator is a module type | Opus, Haiku |
| SEC-C4, SEC-C5, SEC-A7, SEC-A8 | Each fix was cut down to its essential part first (a verified flag; grants that wait for payment; a content-type check and kept ownership; host identity on the Default tenant only), with the stricter options behind a setting or later | Opus |
| SEC-C13, SEC-C15 | The wrong-key throttle and the refund approval cap were removed: the key is 256 bits and compared in fixed time, and refunds are already capped by the sale. GET sign-in becomes an opt-in per account, because OCI "link" mode needs it | Opus, Haiku |
| SEC-P1 | Fixed now, in place, with a distributed lock and account lockout. Moving it onto the shared attempt limiter comes later | Opus |
| SEC-P2 and CQ-1 | The fail-closed step now also relays the keys to the node that owns the call, checks the realtime runner's key path, and clears the capture on hang-up. Without the relay the capture could never finish | Opus |
| SEC-A1 | Split into a small first step (`DefaultTenantOnly`, a run-time check for tenants that already have the feature, a security-critical permission) and optional hardening (a command allowlist) | Opus |
| SEC-A3 | Staged: filtering in the drivers and the API first; per-tool required permissions as a separate, larger item. The tenant tools on the Default shell were added to it | Opus |
| SEC-A5 | The proposed replacement for the visitor resolver was dropped: it would have added a client-chosen header to the key. The Opus reviewer and the first audit disagree about what the packaged resolver reads; the item now says so and asks for the source to be read first | Opus |
| SEC-A6 | Done in phase 1 with the egress helper that exists, instead of waiting for DRY-7. A host option for loopback and private model hosts is required | Opus |
| SEC-FE1 | The stated exploit path was wrong: the AI tool and API need a work-order permission. The real outside path is the contact name and AI-written notes. The fix keeps the dispatch board's deliberate link by accepting a DOM node | Opus |
| UX-1 | The mechanism was corrected (`@T[...]` writes resource text unencoded). The admin fix is one attribute and ships in phase 1a; the storefront listener ships with the shared helpers | Opus, Haiku |
| CQ-8 | The evidence was corrected: CI already builds with warnings as errors and the SDK analyzers are on, so the first-build risk is `EnforceCodeStyleInBuild`, which is now measured first. Banned APIs apply to `src` projects only; `System.Random` is not banned; the central package version entry was added | Opus, Haiku |
| CQ-3 | Adding a parameter to an extension contract breaks outside implementers: internal services may change at any time, extension contracts in one announced release | Opus |
| DRY-7 | Joining the Telnyx typed client to the named client would stack two retry layers; the transport retry is removed for it | Opus |

### Building blocks and structure

| Item | Change | Raised by |
| --- | --- | --- |
| RL-1, DRY-1 | No new `RateLimits` module. The attempt limiter is platform infrastructure (`Abstractions` and `Core`), beside `SignedTokenService` and `EgressNetwork` | Opus |
| RL-2 | No hard dependency of Checkout on `OrchardCore.RateLimits`: it would turn the limiter middleware and Orchard's default global policy on for every existing store at upgrade, and throttle whole sites that sit behind an unconfigured proxy. The request layer is a "Payment Rate Limits" feature that depends on `OrchardCore.RateLimits`; the card-testing gate is always on | Opus |
| RL-0 (new) | A two-line hotfix for the throttle key ships before anything else | Opus |
| RL-3 | The `Cart` to `cart` rename and its data migration were removed as cosmetic and risky. Store credit, points and the terms account left the code group: they spend the caller's own balance | Opus |
| RL-4 | The customer decline policy is keyed by customer id only (an e-mail key would let anyone lock a customer out). Per-network limits were loosened for shared addresses. A store-wide decline signal was added for testing spread across many networks | Opus |
| RL-6 | The setup step uses the existing `IStoreSetupStep`, not the future setup-guide block | Opus |
| Former RL-8 | The distributed limiter source for stored policies was dropped, without a spike | Opus |
| DRY-2 | Unify token generation only; each store keeps its hash format. The shared one-time code service waits for a second consumer | Opus |
| DRY-4 | The options-driven provider selector became a small lookup helper with one matching rule | Opus |
| DRY-5 | Party notifications became a design note; it is not built until a second module needs it | Opus |
| DRY-6 | Share the timeline shape over a projection; stored event and note types do not get a base class | Opus |
| DRY-8, CQ-11 | The API helpers move to `Core`, not a new project; the two items were merged | Opus |
| UX-8 | The script resource is named `crestapps-browser`, to avoid confusion with the vendored CrestApps.Core assets | Opus |
| UX-6 | The unused Stripe script is a public resource name, so it is deprecated before it is deleted. Converting 41 validation summaries was dropped as churn | Opus, Haiku |
| README | A phase 1a of small, independent fixes was added. The Critical AI findings, which the first phase table named only by implication, are listed. Every item id now sits in a phase | Opus, Haiku |

### Severities

All changes were downward, or added a condition.

| Item | Was | Now | Why |
| --- | --- | --- | --- |
| SEC-C6 | High | Medium | The client is already egress-guarded and a timeout bounds the read |
| SEC-FE1 | High | Medium | Needs an outside path to a contact name or AI notes |
| SEC-FE3 | High | Medium (owner decision) | Outside Development the default sink refuses every value |
| SEC-A4 | High | Medium | The session-start limiter does run on this path |
| SEC-A5 | High | Medium | The routes need an API scheme; the gap is a quota per key |
| SEC-A14 | Medium | Low by default | MCP server access is security-critical and administrator-only by default |
| CQ-2 | High | Low to Medium | Today's only subscriber catches and logs its own faults |
| SEC-A1, SEC-A2 | Critical | Critical on a multi-tenant host, High on a single-tenant site | Who can set the values differs |
| SEC-A6 | High | High on a multi-tenant host, Medium on a single-tenant site | Same reason |
| SEC-A15 | Medium confidence | High confidence | The handler was read: nothing denies the other-user case |

### Added findings

| Item | Finding | Raised by |
| --- | --- | --- |
| SEC-C18 | A sold tenant is provisioned with no feature profile, which chains SEC-C2 into SEC-A1 | Opus |
| SEC-A3 (addition) | The tenant tools on the Default shell check no permission | Opus |
| RL-4 (addition) | No store-wide signal for card testing spread across networks | Opus |

### Citations and counts corrected

- `PaymentAttemptLimiter.cs:50` (was `:49`); the dead Stripe script's resource name; SEC-C13's wording (the account is checked, the user is not); SEC-C17o's guard (an admin-route check, not a permission); SEC-A18 (the permission check exists; the Anonymous role holds the permission); the `wizard` rate-limit group is defined and never applied; `IRateLimitGroupMetadata` is a package type, now named as such; "DRY-NP" was a broken reference to DRY-4; "the ten largest" inline scripts listed eight.
- Counts that two readers measured differently are now given as ranges or marked for a recount: `ListAsync` calls, `AddHttpClient` registrations, `CreateProtector` call sites, problem-details results, `escapeHtml` definitions. Four line counts were off by one.
- Counts both readers agree on: 3 `AddGroupRateLimit` calls, 1 `ConfigurationSchema.json`, 101 background tasks, 14 `Task.Run` in 11 files, 726 `catch (Exception`, 21 files over 1,000 lines, 149 `<admin-list>` views, 59 `LocalAsync` copies, 64 unlabeled search boxes.

## Reviewer points that were not adopted

| Point | Why not |
| --- | --- |
| "`GetObligationAmounts` does not exist" (Opus, on SEC-C1) | It exists: a private static method at `DefaultCheckoutEngine.cs:1142`, called at `:974` and `:1116`. The fix keeps using it |
| Put the rate-limit request layer entirely under `[RequireFeatures]` with no named feature (Opus) | The owner asked for payment rate limits that depend on `OrchardCore.RateLimits` and extend it. A named "Payment Rate Limits" feature with that dependency does this, gives administrators one thing to enable, and keeps the reviewer's point that Checkout itself must not hard-depend on the middleware. Recorded as an owner decision |
| Rate SEC-C13, SEC-C15 and SEC-C16 as Low to Medium (Opus) | Kept at Medium with the simplified fixes; they are S-sized either way and the order of work does not change |

## Still unverified

Neither reviewer opened these; they rest on the first audit's reading. Confirm each in the code before changing anything.

- Security, Medium: SEC-C9, SEC-C11, SEC-C14; SEC-P3 to SEC-P9 in depth (the reviewers checked the cited lines but did not trace the logic for SEC-P4 to SEC-P7); SEC-A11, SEC-A12, SEC-A16, SEC-A17, SEC-A19.
- Security, Low: most rows of SEC-C17, SEC-P10 and SEC-A20 (sampled only).
- SEC-A5 (c): what the packaged visitor resolver reads.
- SEC-C2: the prefix and wildcard cases depend on Orchard's tenant matching order, which was reasoned and not run. The host-name case does not.
- Reuse and layering: the counts in LAY-1 to LAY-3, DRY-3's backoff count, DRY-4's provider-pick count, DRY-11's direct-read count.
- Code quality: the interface-method count in CQ-3 and the module count in CQ-9.
- Views: the inline-script count in UX-7.

## Reviewers' bottom lines

- **Opus:** "The plan is well evidenced: every Critical and High item I or the helpers opened is real. It is not ready to hand over as written." It listed six changes to make first. All six were made, except the one point recorded above.
- **Haiku:** "The plan is largely sound: most Critical and High citations hold in code, and several rows are exact." It found no item to drop and listed five groups of corrections, all of which were made.
