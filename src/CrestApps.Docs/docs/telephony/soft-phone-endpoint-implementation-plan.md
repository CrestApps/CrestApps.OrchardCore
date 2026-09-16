---
sidebar_label: Soft Phone /softphone Endpoint Plan
sidebar_position: 5
title: Soft Phone /softphone Endpoint — Implementation Plan
description: Server-side plan for a chromeless, authenticated /softphone page (and small config/answer contract) that the CrestApps Soft Phone browser extension hosts so WebRTC calls survive navigation and ring when closed.
---

# Soft Phone `/softphone` Endpoint — Implementation Plan

**Server-side (this repo). Self-contained — a session can build the server work from this document.**

This is the OrchardCore half of the soft-phone browser-extension effort. The extension (built separately in
`C:\Code\CrestApps\CrestApps.SoftPhone`) opens `https://{domain}/softphone` as a floating window so a live
WebRTC call survives the agent navigating any site, and keeps its own background connection so inbound calls
ring even when the phone is minimized or closed. **This document covers only what the server must build.**

> **Companion doc & shared seam.** The extension is planned in
> `C:\Code\CrestApps\CrestApps.SoftPhone\PROJECT-PLAN.md`. The **Integration contract** section below is the
> shared seam and is duplicated verbatim in both plans. If it changes, change it in both.

## Why (context)

A WebRTC session lives in a page's JS realm; reloading/navigating the page destroys it — unfixable in-page.
The extension hosts the phone in a browser context that navigation does not re-create. The server's job is to
serve the phone as a **standalone, chromeless, authenticated page** and to expose two tiny things the
extension needs (a config endpoint and an `?answerCallId` behavior). Everything telephony-related already
exists and is provider-agnostic.

## What already exists (reuse — do not rebuild)

- **The soft-phone UI + client** is a self-contained component `#telephony-soft-phone` in
  `src/Modules/CrestApps.OrchardCore.Telephony/Views/SoftPhoneWidget.cshtml`, driven by the
  `telephony-soft-phone` script (`Assets/js/soft-phone.js`) over `TelephonyHub`. It already includes the
  incoming-call modal, **Answer / Send-to-voicemail / Ignore**, **answer-and-open** (`answerAndOpen`), and
  matched-record cards. Contact Center layers extra behavior via the `contact-center-soft-phone` script
  (depends on `telephony-soft-phone`).
- **Inbound is server-mediated (Model B).** `DefaultIncomingCallDispatcher` pushes
  `ITelephonyClient.IncomingCall(call, context)` to the user's SignalR group; the ringing call is parked and
  restorable via `PendingIncomingCallOffer` + the `current-incoming-offer` endpoint
  (`AgentSoftPhoneEndpoints`).
- **Hub actions exist:** `TelephonyHub.Answer/Reject/Voicemail/Hangup` (permission-authorized).
- **Screen-pop is first-class:** `IncomingCallCard.Url` + `OpenInNewTab`, `IncomingCallContext.Heading`.
- **Hub URL helpers:** `Html.SignalRHubUrl<TelephonyHub>()` and
  `SignalRHubRoutes.GetTenantAwareHubUrl<TelephonyHub>(HttpContext)`.
- **Auth:** cookie-based, `[Authorize]`, `TelephonyPermissions.UseSoftPhone` /
  `ContactCenterPermissions.SignIntoQueues`. Unauthenticated requests already redirect to the OC login and
  back.

**The gap:** there is no standalone soft-phone page today — only the admin `SoftPhoneWidget` (injected into
admin pages by `SoftPhoneWidgetFilter`) and the `[Admin]` agent desktop at `/contact-center/workspace`
(`AgentWorkspaceController`). This plan adds the standalone page and the small extension seam — **behind a new
feature**, so tenants choose the widget, the extension endpoint, or both.

---

## Feature structure (build this first)

Today the Telephony module has two features: `CrestApps.OrchardCore.Telephony` (Area — base services + hub +
settings) and `CrestApps.OrchardCore.Telephony.SoftPhone` (the floating admin widget + front-end Soft Phone
widget). Split the soft phone into a shared services feature and two consumer features so a tenant enables
only what it wants:

| Feature id | Role | `EnabledByDependencyOnly` | Depends on |
| --- | --- | --- | --- |
| `CrestApps.OrchardCore.Telephony` | Base telephony services, SignalR hub, settings (unchanged) | no | Users, SignalR |
| **`CrestApps.OrchardCore.Telephony.SoftPhone.Core`** (new) | The **shared soft-phone client**: the `#telephony-soft-phone` component markup/partial, `soft-phone.js` + styles, the `telephony-soft-phone` resource manifest, the shared `data-config` builder / `ISoftPhoneWidgetPresenter`, countries, incoming modal — everything both consumers reuse | **yes** | `…Telephony` (Area), `CrestApps.OrchardCore.Resources` |
| `CrestApps.OrchardCore.Telephony.SoftPhone` (existing id, **now the widget feature**) | Shows the existing widget: `SoftPhoneWidgetFilter` (admin floating injection) + the front-end Soft Phone widget content type / part / drivers / migrations / widget settings | no | **`…SoftPhone.Core`**, `OrchardCore.Widgets` |
| **`CrestApps.OrchardCore.Telephony.SoftPhone.Extension`** (new) | Exposes the `/softphone` page + `extension-config` + `?answerCallId` for the browser extension. **No UI widget.** | no | **`…SoftPhone.Core`** |

Notes:
- The existing `…SoftPhone` **id and its user-facing meaning (the widget) are preserved**, so existing sites,
  recipes, and `RequireFeatures(TelephonyConstants.Feature.SoftPhone)` callers keep working. Only its
  *implementation* moves down: the shared client relocates into `…SoftPhone.Core`, which `…SoftPhone` now
  depends on and pulls in automatically.
- **Contact Center dependency — decide during the refactor.** `AgentWorkspaceController` /
  `AgentSoftPhoneController` currently `RequireFeatures(TelephonyConstants.Feature.SoftPhone)`. If the agent
  desktop needs the shared soft-phone client but not the front-end widget, **retarget those to
  `…SoftPhone.Core`** so enabling Contact Center does not force the widget feature on. If the workspace
  genuinely relies on the widget, leave them on `…SoftPhone`. Prefer retargeting to `…SoftPhone.Core`.
- The `/softphone` extension feature does **not** depend on Contact Center. Its offer/answer capabilities
  light up only when CC is also enabled, using the same route-name-resolves-to-null-when-absent pattern the
  widget already uses for voicemail routes (`ContactCenterVoicemailMedia`). `extension-config` includes
  `currentIncomingOfferUrl` only when that route resolves.
- Add feature-id constants (e.g. `TelephonyConstants.Feature.SoftPhoneCore`,
  `…Feature.SoftPhoneExtension`). Update `ContactCenterFeatureDependencyArchitectureTests` and the PublicApi
  baselines for the Telephony/Telephony.Abstractions assemblies to cover the new features.

---

## Integration contract (shared with the extension plan — keep identical in both docs)

Everything the extension needs from the server. **The server builds A, C, and the `?answerCallId` behavior;**
B already exists.

### A. Standalone `/softphone` page
- `GET https://{domain}/softphone` — tenant-aware, cookie-authenticated (`UseSoftPhone`, plus `SignIntoQueues`
  when Contact Center voice is enabled). Unauthenticated → 302 to the OC login, returns to `/softphone` after
  login.
- Renders the existing soft-phone component full-window (chromeless, no admin shell, no floating toggle — the
  window *is* the phone).
- Query params the page honors:
  - `?host=extension` — the page is embedded by the extension: suppress the floating/close chrome; render the
    panel expanded to fill the viewport; (optionally) post `phone:*` bridge messages (see "Optional bridge").
  - `?answerCallId={callId}` — on load, if this matches the agent's current pending inbound offer, the page
    **auto-answers** it (same path as clicking Answer in the incoming modal) and runs its existing
    answer-and-open. This is how the extension answers a call from a notification when the window was closed.

### B. Hub + endpoints the extension consumes (exist today; no server change)
- **Hub:** `TelephonyHub` (SignalR), tenant-aware URL.
  - Server → client events: `IncomingCall(call, context)`, `CallStateChanged(call)`.
  - Client → server invocations the extension calls on its own background connection:
    `Reject(CallReference{ CallId })`, `Voicemail(CallReference{ CallId })` (act on a ringing parked call — no
    media leg). `Answer` is done by the page, not the background.
- **Pending offer:** `GET {adminPrefix}/contact-center/agent/current-incoming-offer` → `PendingIncomingCallOffer`
  or 404.
- **Payload shapes the extension reads:** `call { CallId, From, To, State, Direction, ProviderName }`;
  `context { Heading, Cards:[{ Id, Title, Subtitle, Url, OpenInNewTab, Badges }], Properties }`.

### C. Extension config endpoint (server builds this small addition)
- `GET https://{domain}/softphone/extension-config` — cookie-authenticated, JSON:
  ```json
  {
    "hubUrl": "https://{domain}/<tenant-aware TelephonyHub path>",
    "currentIncomingOfferUrl": "https://{domain}/<adminPrefix>/contact-center/agent/current-incoming-offer",
    "softPhoneUrl": "https://{domain}/softphone",
    "displayName": "…",
    "userId": "…"
  }
  ```
  Lets the extension discover tenant-aware paths instead of hardcoding OC route conventions.

### D. Auth model
- Cookie-based; no OIDC/PKCE, no tokens. The extension's background requests to `{domain}` carry the session
  cookie via its host permission. Opening `/softphone` establishes/refreshes that session.

---

## Server work items

> Placement: work item 0 lands in the new `…SoftPhone.Core` feature; items 1–5 land in the new
> `…SoftPhone.Extension` feature (all in the Telephony module, gated by their features' `[RequireFeatures]` /
> feature-scoped `Startup`).

### 0. Extract the shared client into `…SoftPhone.Core`
- Move the `#telephony-soft-phone` markup into a **partial** shared by the widget and the standalone page, and
  factor the `data-config` construction (currently inline in `SoftPhoneWidget.cshtml`) into
  `ISoftPhoneWidgetPresenter` (or a new shared helper) so there is one source of truth. Move the
  `telephony-soft-phone` resource manifest and styles here. No behavior change — the widget renders exactly as
  before, now sourcing markup/config/resources from the Core feature.

### 1. `/softphone` route + controller (Extension feature)
- Add a controller (Telephony module, `…SoftPhone.Extension` feature) — e.g. `SoftPhoneController` — mapping
  **`GET /softphone`** (not
  `[Admin]`; a normal front-end route so the page has no admin chrome). Tenant prefix/PathBase apply
  automatically.
- `[Authorize]`; authorize `TelephonyPermissions.UseSoftPhone`. When Contact Center voice features are active,
  also require/allow `ContactCenterPermissions.SignIntoQueues` (mirror `AgentWorkspaceController`). Anonymous →
  OC login redirect (returnUrl set automatically) → back to `/softphone`.
- Read the same values `SoftPhoneWidget.cshtml` needs (provider capabilities, audio mode/adapter, default
  country, diagnostics flag, antiforgery token, hub URL) — factor the config-building out of the widget view
  into a shared presenter/helper so the standalone page and the widget stay in sync rather than duplicating
  the `data-config` block. `ISoftPhoneWidgetPresenter` already exists — extend/reuse it.

### 2. Chromeless full-viewport view + layout
- A view that renders the `#telephony-soft-phone` component full-window: no admin nav, no theme chrome, the
  panel expanded to 100% width/height (not the floating `__toggle`/`__panel` popover). Reuse the existing
  markup from `SoftPhoneWidget.cshtml` (extract the panel body into a partial shared by the widget and the
  page so there is one source of truth).
- Register the required resources with the resource manager: `telephony-soft-phone` +
  `contact-center-soft-phone` scripts and the soft-phone styles. Add a small **full-window** stylesheet that
  overrides the floating panel positioning so it fills the viewport and resizes with the window.
- Honor `?host=extension`: hide the close/minimize/toggle chrome and any "open in workspace" affordances; the
  window itself provides those.

### 3. `?answerCallId` auto-answer
- On page load, when `answerCallId` is present, the client should fetch the current pending offer
  (`current-incoming-offer`), and **iff** it matches `answerCallId`, invoke the same Answer path the incoming
  modal uses (hub `Answer` + answer-and-open). Implement this in the client script guarded to run only for the
  standalone page (e.g. a `data-auto-answer-call-id` attribute the view sets from the query). If it does not
  match (offer expired/changed), fall through to the normal ringing/idle UI — never answer a stale call.

### 4. `GET /softphone/extension-config`
- A minimal-API endpoint or controller action, cookie-authenticated (`UseSoftPhone`), returning the JSON in
  contract §C. Build `hubUrl` with `SignalRHubRoutes.GetTenantAwareHubUrl<TelephonyHub>(HttpContext)` and the
  offer URL with `Url.RouteUrl(AgentSoftPhoneEndpoints.CurrentIncomingOfferRouteName)` made absolute. Include
  `displayName`/`userId` for the extension's UI.

### 5. Optional bridge (defer unless needed)
- If the extension asks for screen-pop into the agent's *main browser* (not the phone window) or in-call
  state sync, add a tiny `postMessage` emitter to the standalone page under `?host=extension`
  (`phone:incoming` / `phone:state` / `phone:screenpop`, origin-validated with a handshake nonce). Not
  required for the B2 flow (the extension has its own connection and answers via `?answerCallId`), so keep it
  out of v1 unless the extension session requests it.

### 6. No CORS / framing changes
- The page is loaded first-party in the extension's window (not iframed), so **no `frame-ancestors` and no
  `SameSite=None`** are needed. The extension's background reaches the hub/config with its host permission;
  cross-origin `fetch` is permitted by that permission and WebSocket is not CORS-gated — **no server CORS
  change required.** (If the Phase-0 spike finds the session cookie is not attached to the extension's
  background SignalR connection, the fallback is a small authenticated **short-lived token** endpoint the page
  mints and hands to the extension; add it only if the spike requires it.)

### 7. Tests
- Extend the existing Playwright infra (`tests/CrestApps.OrchardCore.Telephony.PlaywrightTests`,
  `SoftPhoneWidgetTests`, `WebRtcAudioProof`) with a standalone `/softphone` test: unauthenticated →
  login redirect; authenticated → full-window phone renders and connects to the hub; `?answerCallId` answers a
  simulated parked offer and ignores a stale one. Add a unit/endpoint test for `extension-config` (shape +
  auth). The Asterisk inbound simulator (`src/Startup/CrestApps.OrchardCore.Asterisk.Web`) can drive a
  simulated inbound offer for the answer test.

---

## Acceptance criteria
1. `GET /softphone` while signed out → OC login → returns to a **full-window, chromeless** working soft phone.
2. The phone on `/softphone` behaves identically to the widget (dial, in-call controls, incoming modal,
   voicemail, diagnostics) — because it reuses the same component and scripts.
3. `GET /softphone/extension-config` returns the documented JSON for an authorized user and 401 otherwise.
4. `GET /softphone?answerCallId=X` answers the matching pending offer on load and screen-pops; a
   non-matching/expired X does not answer anything.
5. No new framing/cookie/CORS configuration is required for the dedicated-window extension.

## Phasing (server)
1. **Feature split:** add `…SoftPhone.Core` (`EnabledByDependencyOnly`) and `…SoftPhone.Extension`;
   retarget `…SoftPhone` (widget) onto Core; extract shared markup/config/resources into Core (no
   behavior change); update the architecture test + PublicApi baselines.
2. `/softphone` controller + chromeless view + full-window stylesheet + `?host=extension` handling (Extension
   feature).
3. `?answerCallId` auto-answer in the client script.
4. `extension-config` endpoint.
5. Playwright + endpoint tests (including: widget still works with only `…SoftPhone` enabled; `/softphone`
   returns 404/feature-off when only the widget is enabled and works when `…SoftPhone.Extension` is enabled).
6. (Optional) `postMessage` bridge emitter — only if the extension session requests it.
