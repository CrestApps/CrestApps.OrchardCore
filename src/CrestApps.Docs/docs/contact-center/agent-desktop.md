---
sidebar_label: Agent Desktop & Dashboard
sidebar_position: 2
title: Agent Desktop and Supervisor Dashboard
description: How contact center agents work inbound and outbound interactions in the Agent Workspace, and how managers set up and monitor operations with queues, campaigns, and the live supervisor dashboard.
user_manual:
  - user-manual/agent-workspace
  - user-manual/calls
  - user-manual/voicemail
  - user-manual/live-dashboard
  - user-manual/entry-points-and-ivr
---

This guide covers the two day-to-day Contact Center surfaces:

- The **Agent Workspace** - the full-screen desktop where a **contact center agent** spends the shift: it presents work, connects calls, shows customer context, and captures the outcome.
- The **Supervisor Dashboard** - the live wallboard a **contact center manager** uses to monitor queue health and agent presence in real time.

Both build on the [real-time SignalR layer](index.md#real-time-experience) and the [Telephony](../telephony/index.md) soft phone. The CRM still owns the work (activities, contacts, subjects, dispositions), the Contact Center orchestrates it, and Telephony executes the media.

:::tip[Looking for step-by-step instructions?]
This page explains how the surfaces work and how to enable them. The click-by-click tasks (sign in, accept a call, request a break, wrap up, record a greeting, monitor and take over a call, build an IVR menu) are in the User Manual: [Agent Workspace](../user-manual/agent-workspace.md), [Placing and Handling Calls](../user-manual/calls.md), [Voicemail](../user-manual/voicemail.md), [Live Dashboard](../user-manual/live-dashboard.md) and [Inbound Entry Points and IVR Menus](../user-manual/entry-points-and-ivr.md).
:::

## Choosing the agent experience

There are two agent tiers, so enable the one that matches how your agents work:

- **Soft-phone agents** - the Contact Center soft-phone projection is integration glue rather than a standalone feature: it activates automatically whenever **Contact Center Voice**, **Contact Center Real-Time**, and the shared **Telephony Soft Phone Core** client (`CrestApps.OrchardCore.Telephony.SoftPhone.Core`) are all enabled. Soft Phone Core is enabled by dependency: turning on the **Telephony Soft Phone** widget or the **Telephony Soft Phone Extension** feature switches it on. Agents then get live Contact Center call state, presence, and work offers inside the Telephony soft phone, without the full-screen workspace.
- **Full-desktop agents** - the CRM-integrated Agent Workspace is integration glue rather than a standalone feature. It activates automatically whenever **Contact Center Agents**, **Contact Center Voice**, **Contact Center Real-Time**, and **Telephony Soft Phone Core** are all enabled, so it also surfaces the soft-phone projection automatically. There is no separate Agent Desktop feature to enable.

## Enabling the surfaces

The **My workspace** Agent Workspace activates on its own once **Contact Center Agents**, the **Telephony soft phone**, and a Contact Center voice capability — such as **Contact Center Inbound Voice** or the **Outbound Dialer**, which turn on **Contact Center Voice** and, with it, **Contact Center Real-Time** — are all enabled. It is gated on exactly those capabilities so the workspace cannot activate with missing services. Enable **Contact Center Supervision & Live Dashboard** (`CrestApps.OrchardCore.ContactCenter.Supervision`) for the **Live dashboard**; it explicitly composes Real-Time and Voice. Configure a voice provider such as [Telnyx](../telephony/telnyx.md) or [Asterisk](../telephony/asterisk.md) for voice work.

The corresponding entries appear independently under **Interaction Center**:

- **My workspace** and **My voicemail greeting** - the Agent Workspace and the agent's own greeting, available to anyone with the `ContactCenterSignIntoQueues` permission.
- **Live dashboard** - the Supervisor Dashboard, available to anyone with the `MonitorContactCenter` permission (granted to the built-in **Supervisor** role).
- **Shared voicemail** - a queue's shared voicemail box, registered by **Contact Center Inbound Voice** and available to anyone with `AccessContactCenterSharedVoicemail`.

## The docked agent bar

Agents do not have to keep the Agent Workspace (or even the soft phone) focused to receive work. When the full-desktop agent experience is active — that is, whenever **Contact Center Agents**, **Contact Center Real-Time**, **Contact Center Voice**, and **Telephony Soft Phone Core** are all enabled — a persistent **docked agent bar** is injected into the **admin** chrome (the layout `Footer` zone) of **every** admin page for any signed-in user who has the `ContactCenterSignIntoQueues` permission. It is admin chrome, not a placeable widget, so no page can accidentally omit it. The bar is always there as a small collapsed tab showing your presence; click it to open the bar. A new phone offer opens it on its own, and it tucks itself away again when that work is finished or when you click elsewhere on the page.

The bar is the **CRM-side bridge to the call router**. It holds its own live Contact Center hub connection *outside* the soft phone, so a work assignment reaches the agent wherever they are in the CRM even when the soft phone is running in its own window or the [browser extension](../telephony/index.md). When work is assigned, the bar pops the matched activity's **Complete activity** page, unless the current page has a form with unsaved changes. It shows the ringing offer with **Accept** and **Decline** (a preview offer shows **Dial** and **Skip**), then the active call with a link to the activity. Presence on the bar is read-only: change your status from the soft phone. The bar has no disposition controls of its own; you disposition the work on the activity's **Complete activity** page. What the agent sees on the bar is described in the User Manual's [docked agent bar](../user-manual/agent-workspace.md#the-docked-agent-bar) section.

The bar is deliberately **not** injected on:

- the standalone soft-phone page (`/softphone`) — that page *is* the phone, and a second hub connection there would pop the matched record over the live call and navigate the phone away; and
- non-admin (front-end) pages, and any non-view response (JSON, files, redirects) that has no layout to inject into.

Because it rides the soft phone's capability model, a provider without in-browser audio still gets the provider-neutral bar and workspace.

## For contact center managers: preparing the environment

Agents can only receive work once the routing environment exists. Each screen is described step by step in the User Manual; the technical reference for each is in [Agents, Queues & Dialer](agents-queues-dialer.md).

1. **Skills** - the competencies routing can require. See [Skills and Entitlements](../user-manual/skills-and-entitlements.md).
2. **Queues** - one per line of business, with the routing strategy, sticky-agent preference, SLA threshold, reservation timeout, required skills and overflow. See [Queues](../user-manual/queues.md) and [Queues, reservations, and assignment](agents-queues-dialer.md#queues-reservations-and-assignment).
3. **Business hours** - calendars that pause routing or overflow a queue or entry point when closed. See [Business Hours](../user-manual/business-hours.md).
4. **Inbound entry points** (Entry Points feature) - map an inbound number to a queue, a specific agent or an AI voice agent, with priority, business-hours gating, a closed-hours action (hold, voicemail, overflow, or reject), voicemail delivery and an optional IVR menu. See [Inbound Entry Points and IVR Menus](../user-manual/entry-points-and-ivr.md) and [Voice Routing](voice-routing.md).
5. **Agent state reason codes** - the not-ready reasons agents choose from the presence menu. See [Agent States](../user-manual/agent-states.md).
6. **Agent entitlements** (optional Agent Entitlements feature; without it any agent may sign in to any queue or campaign) - the queues and campaigns each user may join. See [Skills and Entitlements](../user-manual/skills-and-entitlements.md). The soft phone lists only these choices, sign-in rejects requests with no authorized membership, routing ignores stale or imported live memberships that are not also entitled, and removing an entitlement immediately prunes the corresponding live session membership, removes connected clients from revoked queue groups, and refreshes their membership snapshot.
7. **Campaigns and dispositions** - campaigns and dispositions live in the [Omnichannel](../omnichannel/index.md) **Interaction Center**. Every activity carries a **Subject** whose **Subject Flow** is the single decision controller: it defines the dispositions an agent can choose and the follow-up actions each disposition triggers. See [Subject Flow is the single decision controller](index.md#subject-flow-is-the-single-decision-controller), and the User Manual's [Campaigns](../user-manual/campaigns.md), [Dispositions](../user-manual/dispositions.md) and [Subject Flows](../user-manual/subject-flows.md).
8. **Dialer profiles** (Dialer feature) - a reusable dialing mode (preview, power, or progressive), pacing, caller ID and compliance rules. The campaign is picked when activities are loaded, not on the profile. See [Dialer Profiles](../user-manual/dialer-profiles.md) and [Dialer](agents-queues-dialer.md#dialer).
9. **Callbacks** - use the callback service or workflow bridge to schedule callback requests against a contact, destination, due window, and optional queue. Due callbacks are promoted into outbound callback activities and, when a queue is set, enter the same routing path as other work.

Grant agents the `ContactCenterSignIntoQueues` permission (or a role that includes it), and grant supervisors the built-in **Supervisor** role (or the `MonitorContactCenter` permission).

## For contact center managers: the Live Dashboard

**Interaction Center → Live dashboard** shows summary metrics, a tile per queue (waiting, signed-in, available, busy/reserved/wrap-up and other not-ready agents, the longest wait and SLA breaches, amber near the SLA and red once breached) and an agent board with filters. It connects to the real-time hub and also re-reads its state every 10 seconds, so it can be left open on a wallboard. Reading the dashboard and every supervisor action, step by step, is in the User Manual's [Live Dashboard](../user-manual/live-dashboard.md).

The dashboard shows only the queues the supervisor is authorized for (the queues and campaigns on the supervisor's own agent entitlement record), and every action is limited to agents and calls in those queues. A supervisor without an entitlement record sees an empty dashboard.

When an agent has a live interaction, the agent card shows only the **Listen** (`Monitor`), **Whisper**, or **Barge** actions for which the active provider both advertises the matching capability and implements the executable monitoring contract. Each action invokes the provider first; the audited Contact Center event is published only after the provider confirms success. Missing contracts, provider failures, and unknown outcomes stay hidden or return failure without recording a successful engagement. While an agent has paused recording for a sensitive-data capture, all three actions fail closed on the server, so a supervisor can never listen in on the secured segment.

While engaged, the card shows the active mode pressed and a **Stop**; the other modes switch on the same leg (a provider that cannot, like Asterisk, stops and engages again). The supervisor hears the call on their own soft phone: the server tells the phone the token of the leg it is about to ring, the phone answers that leg by itself, and it shows a **Monitoring** banner with the same switcher and Stop instead of a call row. For how Telnyx does it, see [Telnyx supervisor monitoring](../telephony/telnyx.md#supervisor-monitoring).

The agent's **⋮** menu (*More actions for …*) and **Take over** are offered per call from what the supervisor's permissions and the provider allow:

| Action | Shown when |
| --- | --- |
| **Take over** | The supervisor holds `ContactCenterInterveneInCalls` and the call's provider implements `IContactCenterVoiceSupervisorInterventionProvider`. The supervisor needs an agent profile of their own. |
| **End call…** | The supervisor holds `ContactCenterInterveneInCalls` and the call has a provider call id. |
| **Transfer…** | As End call, and the provider advertises the `CallTransfer` capability. Every monitoring engagement on the call ends before it moves. |
| **Start recording** / **Stop recording** | As End call, a Contact Center recording service is registered, and the provider advertises the `Recording` capability. Refused while the agent has recording paused for a sensitive-data capture. |
| **Set Available**, **Set Not ready** (`Away`), **Set Break**, **Sign out of queues** | The supervisor holds `ContactCenterInterveneInCalls`. An agent on a call gets the state when their work ends, as if they had asked for it themselves. |
| **Message…** | Any supervisor who can open the dashboard (`MonitorContactCenter`). Up to 500 characters. |

A takeover moves the media first (the customer hears the supervisor before the agent's leg is released) and only then hands the interaction, its talk time from that moment and its after-call work to the supervisor; the released agent goes to wrap-up for queue-routed work or back to ready for a direct call, and a ready supervisor becomes busy so routing does not offer them another call.

An agent on a phone call of their own (a number dialed from the keypad, or an extension call) has no interaction; the board shows the call, and the modes come from the call's provider. Such a call can be monitored and ended, and a keypad call (not an extension call, which ends when either colleague is released) can be taken over; it cannot be transferred or recorded from the dashboard. An agent whose call cannot be monitored shows **Cannot be monitored** with the reason in a tooltip: a sensitive-data capture in progress, a provider without monitoring, or a phone call the provider cannot let a supervisor join.

Every intervention is audited under the supervisor's name: `SupervisorTookOver`, `SupervisorEndedCall`, `SupervisorTransferredCall`, `SupervisorChangedRecording`, `SupervisorSetAgentState`, `SupervisorMessagedAgent` and `SupervisorMonitorModeChanged`.

## For contact center managers: inbound routing runbook

Before publishing a new inbound line, follow the User Manual's [Test a new number before you publish it](../user-manual/entry-points-and-ivr.md#test-a-new-number-before-you-publish-it) checklist: the channel endpoint, the queue and its business hours, the inbound entry point, then a test call with a signed-in agent while watching the Live dashboard. In addition:

- Configure the Subject Flow for the endpoint so inbound activities get the right subject, campaign, disposition list, required-disposition policy, and follow-up subject actions.
- The entry point's **Welcome message** is spoken while the entry point is open, before the IVR menu or, without one, before the caller is put through to the target; its **Closed message** is spoken while it is closed, before the closed action. See [Voice Routing → Welcome and closed messages](voice-routing.md#welcome-and-closed-messages). The queue's own **Welcome message** is separate and is spoken when the caller starts waiting in the queue.
- The expected path of the test call is **provider webhook → entry point → queue → reservation → Agent Workspace offer → soft-phone media**. On the Live dashboard, the queue's waiting count increases before assignment, then the selected agent moves from available to reserved/busy/wrap-up as the call progresses.

### Building an IVR menu

An entry point can play a phone menu ("press 1 for sales, 2 for support") before the caller is routed. The **IVR menu** field on the entry point editor is a visual editor; building a menu with it is described step by step in the User Manual's [Build an IVR menu](../user-manual/entry-points-and-ivr.md#build-an-ivr-menu).

Each key's action is stored as one of these kinds:

| Action | Stored kind | Target |
| --- | --- | --- |
| Send to a queue | `RouteToQueue` | A queue, picked from the tenant's queues. |
| Send to an agent | `RouteToAgent` | An agent, picked from the tenant's agents. |
| Open a submenu | `SubMenu` | One of the menus defined here. A key that jumps to a menu drawn under another key (for example back to the main menu) shows as **Go to another menu**. |
| Send to voicemail | `Voicemail` | None. |
| Transfer to an approved external number | `ExternalTransfer` | An approved destination from the **External transfer destinations** section of *Settings → Contact Center*. |
| Repeat this menu | `Repeat` | None. |

The editor blocks saving on a missing or unknown first menu, a menu with no name or a duplicate name, a silent menu, a menu with no keys, a repeated or invalid key, a key with no action, an action with no target, and a key that opens a menu that does not exist. Two warnings do not block saving: a menu no key or fallback leads to (callers never hear it), and a queue, agent or destination that is no longer in the lists. The server checks the menu again on save and refuses one that cannot run.

**Edit as JSON** shows the menu as JSON; the two views stay in step, and JSON that cannot be read keeps the editor in JSON mode, with the reason, until it is fixed or cleared. The JSON is the same shape a deployment plan carries:

```json
{
  "RootNodeId": "main",
  "MaxRetries": 3,
  "FallbackAction": { "Kind": "RouteToQueue", "TargetId": "QUEUE-ID" },
  "Nodes": [
    {
      "NodeId": "main",
      "Prompt": "Press 1 for sales or 2 for support.",
      "PromptMediaId": null,
      "Options": [
        { "Digit": "1", "Action": { "Kind": "RouteToQueue", "TargetId": "QUEUE-ID" } },
        { "Digit": "2", "Action": { "Kind": "SubMenu", "TargetId": "support" } }
      ]
    }
  ]
}
```

An empty field means no menu. `TargetId` is `null` for `Voicemail` and `Repeat`. A `null` `FallbackAction` routes the caller the way the entry point would with no menu (**Route to the entry point target** in the editor). `PromptMediaId` is the identifier of a voice media item played instead of `Prompt`.

The menu plays only while the entry point is open; a closed entry point applies its closed action instead. What each action does to the caller at run time, how retries and the fallback work, and what the call's history records are described in [Voice Routing → Entry-point phone menus](voice-routing.md#entry-point-phone-menus-ivr).

## For contact center managers: outbound and callback runbook

Use CRM campaigns and activities as the source of outbound work; the dialer profile only controls execution. The screens are in the User Manual: [Dialer Profiles](../user-manual/dialer-profiles.md), [Load Activities](../user-manual/load-inventory.md) and [Campaigns](../user-manual/campaigns.md).

1. Create the campaign and Subject Flow in Omnichannel. Configure dispositions and subject actions first so every outcome has a business result.
2. Load activities with the **Dialer** source, so they are loaded unassigned and available for reservation, picking the dialer profile and the campaign on the load.
3. Confirm do-not-call, retry delay, calling window, and national registry settings before enabling an automated mode.
4. For callbacks, schedule a callback request with the destination, due time, queue, and notes. The callback dispatcher promotes due callbacks into outbound callback activities and enqueues them when a queue is set.
5. Agents receive preview work, or automated power/progressive work, from the campaigns they are signed in to, then complete it with the same disposition flow used for inbound work.

## For contact center managers: workflow automation

The Subject Flow is the primary business workflow for work completion. Use it for required dispositions and disposition-driven actions such as finish, retry, new activity, or communication-preference updates. Enable `OrchardCore.Workflows` alongside Contact Center only when you need Orchard workflow automation from Contact Center domain events such as routing decisions, offer acceptance, call connected/ended, callback scheduled/promoted, or SLA/analytics events; the Contact Center workflow activities then become available automatically. Workflow automation should enrich or react to activity state; it should not bypass queues, reservations, or the source-neutral disposition service.

## For contact center agents: the Agent Workspace

**Interaction Center → My workspace** is the screen an agent keeps open for the whole shift, next to the [Telephony soft phone](../telephony/index.md), which carries the call audio and device controls. What the agent does there, step by step, is in the User Manual's [Agent Workspace](../user-manual/agent-workspace.md) and [Placing and Handling Calls](../user-manual/calls.md). The sections below describe how the workspace behaves underneath.

### 1. Sign in and set your presence

Agents sign in to queues and campaigns from the soft phone's **Work** tab and set their presence from the soft phone or the workspace; see [Sign in to queues and campaigns](../user-manual/agent-workspace.md#start-your-shift-sign-in-to-queues-and-campaigns) and [Set your presence](../user-manual/agent-workspace.md#set-your-presence).

- The **Work** tab signs the agent in and out over the live Contact Center connection without reloading the page, so the queue membership updates in place and the browser joins or leaves the live queue groups at once. The live session membership follows too, so signing out removes this browser session from the queue and campaign state immediately.
- If inbound voice work is already waiting in one of the agent's queues, signing in or switching back to **Available** immediately asks routing to offer the next queued call instead of waiting for another inbound event. Signing in stays responsive even when the Voice feature is enabled, because the re-offer of waiting calls runs separately from the sign-in request.
- If the browser refreshes or the soft phone reconnects while the agent is signed in and available, the queues are re-checked as soon as the soft phone reconnects, so calls already waiting are offered rather than parked until the next inbound call.
- If a ringing offer was already assigned when the page refreshed, the soft phone restores that same offer and keeps the ringing modal visible until the agent accepts it, declines it, or the reservation timeout sends it back to routing. A new inbound offer opens the soft-phone ringing modal as soon as routing assigns it.
- Sign-in, sign-out, and reconnect all run the same self-healing pass before routing resumes. Leftovers such as a half-cleared offer from a restart, a pending reservation without a live ringing call, a stale ringing offer with no active reservation, or an available agent still holding assigned voice work are reclaimed and re-queued, so they cannot block the next offer or leave the agent counted as busy.
- When a timed-out offer goes back to the queue, its ringing assignment is cleared first, so the agent is not left at capacity for the next offer.
- Once the agent accepts a call, the soft phone ignores any repeat of that same ringing offer and never shows a new inbound modal over the active call. If the offer is revoked at the same moment the accept finishes, the accepted call stays active instead of snapping back to **Ready**.
- A break is system-approved: it is granted at once when nothing is being routed to the agent, and otherwise kept as `RequestBreak` until the current work ends. A reason code mapped to `Break` submits a break request; other reason codes set the state they are mapped to and record the reason on the agent profile and in the audit trail. With no reason codes configured, the soft phone menu falls back to the built-in not-ready states.

### 2. Receive and answer an offer

The offer card, **Accept** and **Decline** are described in [Answer or decline an offer](../user-manual/agent-workspace.md#answer-or-decline-an-offer).

- **Accept** accepts the reservation, connects the media, and moves the work into the active panel. For providers that ring the agent's own device, the device rings and the agent answers there. The workspace and the incoming-call modal re-check the provider's current call state before accepting, and the interaction is marked connected only when the provider reports it connected, so the agent never gets stuck on a call the server already ended while the offer was in flight. For server-side queue delivery (for example Asterisk), Contact Center answers the live provider call during the accept, so the connected call stays visible and controllable.
- **Decline** releases the offer back to the queue and records the agent as having turned it down, so routing offers it to somebody else first and comes back to the agent only when nobody else can take it. The incoming modal does not send a separate telephony reject for the same call. An offer left to ring out follows the queue's unanswered-offer action.

Dialer work is distinguished from inbound queue offers by its activity source. When a Preview, Power, Progressive, or generic dialer activity is assigned, the browser opens the assigned activity's shared **Complete activity** page automatically. Inbound work continues to show the ringing offer instead, so it is never redirected before the agent chooses **Accept** or **Decline**.

### 3. Handle the active interaction

The active interaction panel and the soft phone's call controls are described in [My workspace](../user-manual/agent-workspace.md#my-workspace) and [Placing and Handling Calls](../user-manual/calls.md).

Soft-phone controls are shown from the provider's advertised Telephony capabilities, and the server repeats the same capability check before invoking the provider. The soft phone conferences two selected calls without requiring a provider call id, and can disconnect all active calls. The workspace reflects call state in real time.

A Contact Center call is transferred through the Contact Center rather than the phone provider: the soft phone's transfer panel lists the other agents with their presence, the queues with who is waiting, and the approved outside numbers, and a warm transfer runs as a consult the agent completes or cancels from the panel (see [Placing and Handling Calls](../user-manual/calls.md)). Consultative transfer depends on the provider: Telnyx supports it; Asterisk supports blind transfer and two-call conference but rejects warm transfer.

When Contact Center owns the assigned voice interaction, server-side call-session changes flow back into the Telephony soft phone in real time, so provider-side disconnects, failed calls, transfers, hold/resume, mute/unmute, and other normalized call-state changes update the live call card and the persisted **Recent** history at once.

For an answered queue or campaign call, a terminal provider event moves the agent from **Busy** to **Wrap-up** immediately. (A direct call, such as an extension call, skips wrap-up and returns the agent to work.) Wrap-up is not a timed auto-return: completion records the wrap-up end time and returns the agent to a previously requested break when one is pending; otherwise it returns the signed-in agent to **Available** and routing can offer the next call. This avoids sending another call while after-call work is unfinished. The platform caps wrap-up at 15 minutes (`MaximumWrapUpDuration`): after that the agent is released automatically, while the activity stays open and no disposition is recorded for it.

Presence changes and queued-call recovery run as separate operations, so a presence change never waits on voice routing and the presence control cannot be left spinning.

Contact Center also runs a provider-truth reconciliation pass when the tenant activates and on a periodic safety cadence. If Orchard Core restarts during busy hours, persisted ringing or active interactions are revalidated against the telephony server before routing resumes, and a pre-connect offer that already ended on the provider side is removed from the queue instead of being re-offered as a ghost call.

If a prior terminal provider event was already recorded in the call session but another recovery path left the interaction nonterminal, reconciliation repairs the interaction from the terminal call session before capacity is evaluated, then clears stale queue, reservation, and agent state. This prevents an ended call from consuming the agent's `MaxConcurrentInteractions` slot indefinitely.

For inbound server-side calls, a provider may report the caller leg as connected before an agent accepts the Contact Center offer. Ended-offer cleanup therefore uses the accepted reservation or assigned queue item—not the provider leg's answered timestamp—to decide whether the work reached an agent. A terminal call that was only waiting or reserved is removed and releases the agent so routing can continue to the next live call.

#### Secure pause for sensitive-data capture

When a customer must read out a card number, a national identity number, or another piece of sensitive data, the agent can suppress recording for that segment so the value never enters the recording, and resume it as soon as the customer finishes. This mirrors the "pause and resume" descoping control used by state-of-the-art contact center platforms to keep sensitive input out of recorded media. The agent's steps are in [Protect card details on a recorded call](../user-manual/agent-workspace.md#protect-card-details-on-a-recorded-call).

The **Pause recording** control appears on the active interaction only when every one of these is true:

- The **Recording** feature is enabled and the tenant has turned on **Allow agents to pause recording** on the **Recording governance** tab of *Settings → Contact Center*.
- The agent holds the `ContactCenterSecurePauseRecording` permission (granted to the built-in **Agent** stereotype).
- The active voice provider advertises the **RecordingPause** capability and implements the executable recording contract for the live call (for example, the Asterisk provider).

When the agent pauses, the server re-checks the setting, the reason policy, the provider capability, and—most importantly—that the agent owns the live interaction before it asks the provider to pause. If the tenant requires a reason, the agent must supply one; the reason is stored on the interaction for the audit trail but never appears in the recording. A paused recording shows a clear **Recording paused** badge to the agent, and the control switches to **Resume recording**.

Two safeguards protect the customer even if the agent forgets to resume:

- **Supervisor monitoring is blocked while recording is paused.** A supervisor cannot start Monitor, Whisper, or Barge on an interaction whose recording is paused, so a coach can never listen in on the secured segment. The block is enforced on the server before the provider is ever contacted. If a supervisor was already engaged when the agent starts the pause, that live engagement is force-stopped as part of the pause, so an in-progress coach is evicted along with the recording rather than only being kept out afterward.
- **Automatic resume** returns recording to the active state after the tenant's configured maximum pause window elapses, so a pause can never silently outlive its purpose. The automatic resume is published as a distinct audit event so a safety-net resume is always distinguishable from an agent-driven resume. Setting the maximum window to `0` disables the automatic guard and lets a pause persist until it is explicitly resumed.

The agent desktop and the Supervisor Dashboard both reflect the pause and resume in real time through the hub, so every audience sees the same recording state without refreshing.

#### Hosted secure data capture

Pausing recording keeps a spoken value out of the recorded media, but the agent still hears the customer read it out. **Hosted secure data capture** removes the agent from the exchange entirely: the customer enters the sensitive value on a dedicated secure page, the value is tokenized the moment it is submitted, and only a masked representation (such as the last four digits of a card) and a durable token reference are ever stored. Values that must never be retained in any form, such as a card security code, are validated and then discarded - no token and no mask are kept for them. The agent, the supervisor, and the recording never see the raw value. This mirrors the hosted-page tokenization pattern that state-of-the-art contact center platforms use to keep cardholder data out of agent scope and out of the recorded media path.

The **Collect data securely** control appears on the active interaction only when every one of these is true:

- The **Secure Data Capture** feature is enabled and the tenant has turned on **Enable agent-assisted secure data capture** in *Interaction Center → Settings → Secure Data Capture*.
- The agent holds the `ContactCenterInitiateSecureCapture` permission (granted to the built-in **Agent** stereotype).

When the agent starts a capture, the server re-checks the setting and confirms the agent owns the live interaction before it mints a one-time access token. Only one secure capture may be in progress for an interaction at a time, so a second request is refused while one is still collecting. The token is returned to the agent exactly once as a short-lived secure link to share with the customer over the existing channel; only its SHA-256 hash is stored, so a leaked datastore can never reconstruct a usable link. The secure page is served with `Cache-Control: no-store` and `Referrer-Policy: no-referrer` so the token in the link is never cached or leaked through a referrer header. Starting a capture also pauses recording as defense in depth when the tenant leaves **Pause recording during capture** enabled, so a provider that records the whole media path cannot retain the segment either. If the capture cannot be persisted after recording was paused, recording is resumed immediately so a failed start never leaves recording suppressed.

The customer opens the link on their own device and enters each requested value on the secure page. On submission every requested field is tokenized before anything is persisted, so a single invalid value cannot leave a half-completed capture; recording resumes automatically, and a **secure capture completed** audit event records only the masked values. Resuming recording is self-healing: if the provider cannot resume at the moment a capture settles, a background recovery pass retries it, so a transient provider failure never leaves recording paused permanently. If the customer never finishes, the one-time link expires after the tenant's configured window (30 seconds to one hour, five minutes by default), a background safety net settles the abandoned capture, and recording is resumed. The agent can also cancel an in-progress capture.

The default tokenization sink validates and masks the value locally and returns an opaque surrogate token. It is registered **only** in the Development environment and is intended for development and evaluation only: it is **not** a PCI-DSS-compliant sink, because the surrogate is not backed by a compliant vault. Outside Development, no sink is registered by default, so secure capture fails closed until an operator wires one up - a misconfigured production deployment can never silently fall back to the non-compliant developer sink. A production deployment that captures cardholder data must replace the `ISecureCaptureTokenSink` implementation with one that forwards the raw value to a PCI-DSS-compliant tokenization provider and returns that provider's token. The sink receives a stable per-capture, per-field idempotency key and a production implementation **must** honor it as an idempotency contract: the same key with the same value returns the original token without minting a second vault token, and the same key with a different value fails safely instead of tokenizing the new value. This makes a retried or replayed submission exactly-once at the vault even though the capture service runs inside an ambient unit of work whose commit is evaluated after the call returns.

**PCI-DSS scope.** In this model the raw value is submitted to the application, which hands it straight to the sink and never persists or logs it. That keeps the value out of agent, supervisor, and recording scope, but because the application receives and transmits the cardholder data it is **in scope** for PCI-DSS (part of the cardholder data environment). Do not treat the server-side model as SAQ A / SAQ A-EP eligible; determine the applicable assessment with your QSA. A deployment that wants the raw value to never reach the application - the prerequisite for the reduced SAQ A / SAQ A-EP scopes - should host the entry fields directly from the tokenization provider (for example provider-hosted iframe fields or direct-to-provider submission). The pluggable `ISecureCaptureTokenSink` seam and the browser-side secure page are the substitution points that make that hosted-field variant possible without changing the orchestration.

### 4. Complete the activity in the CRM

**Complete activity** in the active panel opens the shared Omnichannel completion page for the assigned activity, so contact-center work follows the same CRM experience as manual activities. The agent's steps are in [After the call: wrap-up](../user-manual/agent-workspace.md#after-the-call-wrap-up) and [Complete an activity](../user-manual/activities.md#complete-an-activity).

Completing routes through the shared disposition path, which applies the disposition, marks the activity completed, and runs the subject flow's follow-up actions - the same path used everywhere in the CRM, so inbound, outbound, and manual work all behave consistently. If the subject flow **requires** a disposition, completion is blocked until the agent picks one and the completion page shows why.

An activity that is already finished cannot be completed a second time, and the completion page says so instead of opening a form it will not accept. This is ordinary traffic rather than a stale link: an automated call that hands off to a live agent, or a caller who reaches voicemail on the queue's maximum wait, can close its own activity while the agent still has the wrap-up open. The agent is returned where they came from with an explanation, and the outcome already on record is kept rather than overwritten.

The workflow preview renders subject- and action-derived titles with DOM text nodes rather than HTML injection. Stored CRM text is displayed literally and cannot create markup or execute script in the agent's browser.

Completion links opened from Contact Center include a local return location. After dialer work is completed or cancelled, the agent returns to **My workspace**; manual activity completion keeps the default **Activities** destination. Return locations are accepted only when they are local application URLs.

### 5. Review recent activity

The workspace's **Recent activity** panel and the soft phone's **Recent** and **Voicemail** tabs are described in [Review your recent activity](../user-manual/agent-workspace.md#review-your-recent-activity) and [Listen to your voicemail](../user-manual/voicemail.md#listen-to-your-voicemail). Both soft-phone tabs update in real time as calls end, with no page refresh.

### 6. Manage your voicemails

Playing and deleting voicemails in the soft phone's **Voicemail** tab is described in [Voicemail](../user-manual/voicemail.md#listen-to-your-voicemail).

- Playback is governed and audited like any other recording, and the audit names the agent as the person who listened.
- Deleting removes the entry from the inbox and erases the stored recording. The voicemails are deleted one at a time; each one that fails stays selected with the reason on its row (for example, the recording is under legal hold, or the session has ended). A voicemail whose caller hung up before anything was recorded is removed from the inbox without an erasure entry in the audit, because there was no recording to erase.

#### Who owns a voicemail

One rule decides which voicemails an agent sees, plays and deletes: a voicemail is theirs when it is in **their** soft-phone inbox. An agent can play and delete exactly the voicemails in their list, and nobody can play or delete a voicemail that is in another user's inbox, whatever identifier they send. The same rule is explained for agents in [Whose inbox a voicemail goes to](../user-manual/voicemail.md#whose-inbox-a-voicemail-goes-to).

The platform puts a voicemail in an inbox once, when the call reaches voicemail:

| How the call reached voicemail | Whose inbox |
| --- | --- |
| A direct call to an agent (their extension, or an entry point that targets them) that was not answered | The agent the call was for. |
| An agent pressed **Voicemail** on a ringing call | That agent. |
| An offer that rang out on a queue whose unanswered-offer action is voicemail | The agent the offer rang, whatever the entry point's delivery setting. |
| A queued call that reached the queue's voicemail on its maximum wait, after an offer to an agent expired | The agent the call was last offered to. |
| A call on a queue line that reached voicemail with no agent of its own (the caller chose voicemail from the phone menu, the queue was full, or they waited too long before anybody was offered the call) | The entry point's **Voicemail inbox** agent. With none set, the message is recorded but is in nobody's inbox. When the entry point delivers to **The queue's shared voicemail box**, the message is in no agent's inbox: it is on the **Shared voicemail** page for the queue, including the case above where an offer had expired earlier (see [The queue's shared voicemail box](voice-routing.md#the-queues-shared-voicemail-box)). |

Unless the entry point delivers to the queue's shared voicemail box, a queue voicemail is not shared among the queue's members and is not listed for supervisors. To have a supervisor hear the messages left on a queue line, set the entry point's **Voicemail inbox** to the supervisor's agent profile; the messages then appear in that person's **Voicemail** tab like their own. A supervisor who must remove a recording uses recording erasure, which is audited as the supervisor's action.

The voicemail endpoints answer a refusal with a status code and a problem body (`401` signed out, `403` not in your inbox, `404` not found, `409` legal hold). They never redirect to the sign-in or access-denied page, so the soft phone can always tell a refused delete from a completed one.

### 7. Record your voicemail greeting

**Interaction Center → My voicemail greeting** lets an agent record a greeting with the microphone or upload an audio file (MP3, WAV, OGG or WebM, up to 20 MB); see [Record your voicemail greeting](../user-manual/voicemail.md#record-your-voicemail-greeting).

A saved greeting is uploaded to the telephony provider's own media storage (for Telnyx, Media Storage) and played back from there, so no publicly reachable URL of your own is required. When the agent has no recorded greeting, the caller hears the entry point's **Default voicemail greeting** (configured per dialed number), and if that is also empty, a built-in system greeting. See [Voice routing](voice-routing.md#greeting-then-beep-then-record).

## How it works

- The workspace loads a **state snapshot** from the server and then keeps itself current from the real-time hub's presence, offer, and queue events. It re-reads the authoritative state after you act, so what you see always matches the server.
- Contact Center domain events are persisted immediately and the handler fan-out runs as deferred outbox work, so slow workflow or real-time projections do not block the soft-phone sign-in or sign-out postback.
- **Accept** calls a single server-side command that accepts the reservation, revalidates the provider's current call state, tells the voice provider to connect the call when needed, and advances the interaction and call session only when the provider truth supports that transition.
- **Complete** goes through the source-neutral `IActivityDispositionService`, so dispositions, required-disposition rules, and subject-flow actions behave identically across every channel and source.

## Permissions and roles

| Permission | Grants |
| --- | --- |
| `ContactCenterSignIntoQueues` | Sign in to queues/campaigns, change own presence, and use the Agent Workspace (accept/decline offers, complete work) and **My voicemail greeting**. |
| `ContactCenterSecurePauseRecording` | Pause and resume recording on the agent's own live interaction to keep sensitive customer data out of the recording. Included in the **Agent** stereotype. |
| `ContactCenterInitiateSecureCapture` | Start an agent-assisted hosted secure data capture on the agent's own live interaction, so the customer enters sensitive data on a secure page instead of reading it to the agent. Included in the **Agent** stereotype. |
| `MonitorContactCenter` | Open the Supervisor Dashboard, watch queues in real time, listen to, whisper to and barge into live calls, and message agents. Included in the **Supervisor** role. |
| `ContactCenterInterveneInCalls` | Take over, end, transfer and record live calls, and set an agent's state or sign them out, from the Supervisor Dashboard. Included in the **Supervisor** role. |
| `AccessContactCenterSharedVoicemail`, `ManageContactCenterSharedVoicemail` | Work a queue's shared voicemail box for entitled queues; the manage permission also deletes messages and takes over other users' claims. Both are in the **Supervisor** role. |
| `ManageContactCenterQueues`, `ManageContactCenterAgents`, `ManageContactCenterSkills`, `ManageContactCenterDialer` | Configure the routing environment (queues and entry points, agents, skills, dialer). See [Agents, Queues & Dialer](agents-queues-dialer.md). |
