---
sidebar_label: Agent Desktop & Dashboard
sidebar_position: 2
title: Agent Desktop and Supervisor Dashboard
description: How contact center agents work inbound and outbound interactions in the Agent Workspace, and how managers set up and monitor operations with queues, campaigns, and the live supervisor dashboard.
---

This guide covers the two day-to-day Contact Center surfaces:

- The **Agent Workspace** - the full-screen desktop where a **contact center agent** spends the shift: it presents work, connects calls, shows customer context, and captures the outcome.
- The **Supervisor Dashboard** - the live wallboard a **contact center manager** uses to monitor queue health and agent presence in real time.

Both build on the [real-time SignalR layer](index.md#real-time-experience) and the [Telephony](../telephony/index.md) soft phone. The CRM still owns the work (activities, contacts, subjects, dispositions), the Contact Center orchestrates it, and Telephony executes the media.

## Enabling the surfaces

Enable **Contact Center Agent Desktop** (`CrestApps.OrchardCore.ContactCenter.AgentDesktop`) for **My workspace**. It explicitly composes Availability, Real-Time, Voice Soft Phone, and Omnichannel Management so the workspace cannot activate with missing services. Enable **Contact Center Supervision** (`CrestApps.OrchardCore.ContactCenter.Supervision`) for the **Live dashboard**; it explicitly composes Real-Time and Voice. Configure a voice provider such as [DialPad](../telephony/dialpad.md) for voice work.

The corresponding entries appear independently under **Interaction Center**:

- **My workspace** - the Agent Workspace, available to anyone with the `ContactCenterSignIntoQueues` permission.
- **Live dashboard** - the Supervisor Dashboard, available to anyone with the `MonitorContactCenter` permission (granted to the built-in **Supervisor** role).

## For contact center managers: preparing the environment

Agents can only receive work once the routing environment exists. Configure these in the **Interaction Center** before your team signs in. Each item links to its detailed reference in [Agents, Queues & Dialer](agents-queues-dialer.md).

1. **Skills** (*Interaction Center → Management → Skills*) - define the competencies routing can require, for example `Spanish`, `Billing`, or `Tier2`.
2. **Queues** (*Interaction Center → Management → Queues*) - create a queue per line of business. Choose the routing strategy (longest-idle, round-robin, or least-busy), optional sticky-agent preference, the SLA threshold, the reservation timeout, required skills, and - for inbound voice - the dialed number (DID) that feeds the queue. See [Queues, reservations, and assignment](agents-queues-dialer.md#queues-reservations-and-assignment).
3. **Business hours** (*Interaction Center → Management → Business hours*) - attach a calendar to a queue so it pauses routing (or overflows) when closed.
4. **Inbound entry points** (*Interaction Center → Inbound entry points*, Entry Points feature) - map an inbound DID to a queue with a priority, business-hours gating, and a closed-hours action (hold, voicemail, overflow, or reject).
5. **Agent state reason codes** (*Interaction Center → Management → Agent states*) - define the not-ready presence reasons agents can choose (for example `Lunch`, `Coaching`, `Admin`). These appear in the agent presence menu.
6. **Agent entitlements** (*Interaction Center → Management → Agent entitlements*) - select an Orchard user and grant the queues and campaigns that user may join. The soft phone lists only these choices, sign-in rejects requests with no authorized membership, routing ignores stale or imported live memberships that are not also entitled, and removing an entitlement immediately prunes the corresponding live session membership, removes connected clients from revoked queue groups, and refreshes their membership snapshot.
7. **Campaigns and dispositions** - campaigns and dispositions live in the [Omnichannel](../omnichannel/index.md) **Interaction Center**. Every activity carries a **Subject** whose **Subject Flow** is the single decision controller: it defines the dispositions an agent can choose and the follow-up actions each disposition triggers. See [Subject Flow is the single decision controller](index.md#subject-flow-is-the-single-decision-controller).
8. **Dialer profiles** (*Interaction Center → Management → Dialer Profiles*, Dialer feature) - for outbound work, tie a campaign's activities to a queue, a dialing mode (manual, preview, power, or progressive), pacing, and compliance rules. See [Dialer](agents-queues-dialer.md#dialer).
9. **Callbacks** - use the callback service or workflow bridge to schedule callback requests against a contact, destination, due window, and optional queue. Due callbacks are promoted into outbound callback activities and, when a queue is set, enter the same routing path as other work.

Grant agents the `ContactCenterSignIntoQueues` permission (or a role that includes it), and grant supervisors the built-in **Supervisor** role (or the `MonitorContactCenter` permission).

## For contact center managers: the Live Dashboard

Open **Interaction Center → Live dashboard**. The dashboard connects to the real-time hub and refreshes automatically as work and presence change (with a periodic safety refresh), so it can be left open on a wallboard.

It shows three sections:

- **Summary metrics** - total items waiting across all queues, the number of available agents, the total agent count, and the queue count.
- **Queue tiles** - one tile per enabled queue showing the waiting count, signed-in agents, available agents, busy/reserved/wrap-up agents, other not-ready agents, the longest current wait, and the number of items that have breached the queue's SLA threshold. Tiles turn amber as waits approach the SLA and red once items breach it, so managers can compare demand and staffing at a glance.
- **Agent board** - every agent with a live presence dot (available, busy, wrap-up, break, and so on), their current reason, and how many interactions they are handling.

Use it to spot a backing-up queue, an SLA breach, or too few available agents, and then rebalance staffing, adjust queue priorities, or open a campaign.

When an agent has a live interaction and the voice provider advertises the matching capability, the agent card shows **Monitor**, **Whisper**, **Barge**, and **Take over** actions. Each action posts to the audited monitoring service, which refuses unsupported modes and records the engagement as a Contact Center domain event. Providers still own the media execution, so buttons are effective only when the active provider implements the requested capability.

## For contact center managers: inbound routing runbook

Use this checklist before publishing a new inbound line:

1. Create or confirm the Omnichannel **channel endpoint** for the dialed number.
2. Configure the Subject Flow for that endpoint so inbound activities get the right subject, campaign, disposition list, required-disposition policy, and follow-up subject actions.
3. Create the target queue, set its SLA, reservation timeout, routing strategy, required skills, and overflow queue.
4. Attach a business-hours calendar when the queue should pause or overflow outside staffed hours.
5. Create an **Inbound entry point** for the DID. Set the target queue, priority, optional welcome/closed messages, and the closed action: hold, voicemail, overflow, or reject.
6. Sign at least one skilled agent in to the queue, then place a test call. The expected path is **provider webhook → entry point → queue → reservation → Agent Workspace offer → soft-phone media**.
7. Watch **Live dashboard** while testing. The queue waiting count should increase before assignment, then the selected agent should move from available to reserved/busy/wrap-up as the call progresses.

## For contact center managers: outbound and callback runbook

Use CRM campaigns and activities as the source of outbound work; the dialer profile only controls execution.

1. Create the campaign and Subject Flow in Omnichannel. Configure dispositions and subject actions first so every outcome has a business result.
2. Load activities through **Load Inventory**. Choose a dialer source for dialer inventory so activities are loaded unassigned and available for reservation.
3. Create a dialer profile that points to the campaign, queue, voice provider, dialing mode, pacing, and compliance settings.
4. Confirm do-not-call, retry delay, calling window, and national registry settings before enabling an automated mode.
5. For callbacks, schedule a callback request with the destination, due time, queue, and notes. The callback dispatcher promotes due callbacks into outbound callback activities and enqueues them when a queue is set.
6. Agents receive preview/manual work from their signed-in campaign or automated power/progressive work from the queue, then complete it with the same disposition flow used for inbound work.

## For contact center managers: workflow automation

The Subject Flow is the primary business workflow for work completion. Use it for required dispositions and disposition-driven actions such as finish, retry, new activity, or communication-preference updates. Enable `CrestApps.OrchardCore.ContactCenter.Workflows` only when you need Orchard workflow automation from Contact Center domain events such as routing decisions, offer acceptance, call connected/ended, callback scheduled/promoted, or SLA/analytics events. The feature explicitly enables `OrchardCore.Workflows`. Workflow automation should enrich or react to activity state; it should not bypass queues, reservations, or the source-neutral disposition service.

## For contact center agents: the Agent Workspace

Open **Interaction Center → My workspace**. This is the screen an agent keeps open for the whole shift. Keep the [Telephony soft phone](../telephony/index.md) available too - it is where the call audio and device controls live.

### Shift checklist

1. Open **My workspace** and the Telephony soft phone.
2. Sign in to the queues and campaigns you are staffed for.
3. Set presence to **Available** when ready, or choose a reason code when not ready.
4. Accept or decline inbound offers from the ringing card; dialer assignments open their **Complete activity** screen automatically so the assigned record is ready without another navigation step.
5. End the conversation, review/update the CRM context, choose the disposition, add notes when needed, and submit.
6. Use **Recent activity** to verify your last outcomes before taking the next offer.

### 1. Sign in and set your presence

- **Sign in to queues and campaigns** from the soft phone's **Work** tab. You can only choose queues and campaigns you are allowed to handle.
- Empty queue and campaign selectors show **Select queue(s)** and **Select campaign(s)**. No membership is selected until you explicitly choose it.
- Select at least one queue or campaign before signing in. The Work tab shows an inline error when nothing is selected.
- After sign-in, the Work tab lists every queue and campaign you are signed in to. Use the individual **Sign out** action to leave one membership while remaining signed in to the others, or **Sign out of all** to leave every membership.
- If inbound voice work is already waiting in one of those queues, signing in or switching back to **Available** immediately asks routing to offer the next queued call instead of waiting for another inbound event.
- The soft-phone **Work** tab now signs you in and out over the Contact Center SignalR hub instead of reloading the page, so queue membership updates stay in-place and the same browser connection immediately joins or leaves the live queue groups.
- If the browser refreshes or the soft phone reconnects while you are still signed in and available, Contact Center now re-checks those queues again as soon as the soft phone reconnects, so already-waiting calls are re-offered instead of staying parked until the next inbound routing event.
- If a ringing inbound offer was already assigned to you when the page refreshed, the soft phone now restores that same offer from the active reservation and keeps the ringing modal visible until you accept it, decline it, or the reservation timeout sends it back to routing.
- New inbound offers now also reopen the soft-phone ringing modal from the live Contact Center hub event as soon as routing assigns them, so agents no longer need a page refresh or reconnect cycle to see the next queued call.
- If a restart or earlier failure leaves behind a half-cleared voice offer, queued-voice recovery now cancels that orphaned pending reservation before it re-checks waiting calls, so a ghost reservation cannot keep blocking the next inbound assignment.
- Queue sign-in, sign-out, and reconnect-driven availability recovery now all run the same self-healing pass before routing resumes, so impossible leftovers such as a pending reservation without a live ringing interaction, or an available agent still owning assigned voice work, are reclaimed and re-queued automatically instead of silently blocking the next inbound offer.
- Once you accept an inbound voice offer, the soft phone now suppresses any duplicate restore of that same ringing reservation and will not show a new inbound modal over an already active call.
- If the real-time layer revokes the pending offer at the same moment your accept finishes, the soft phone now keeps the accepted call active instead of snapping back to **Ready**, so the ringing modal can disappear without losing the live call card.
- Queue sign-in no longer eagerly resolves the queued-voice re-offer pipeline while the sign-in postback is being processed, so signing into queues stays responsive even when the Voice feature is enabled.
- When a timed-out voice offer is re-queued, Contact Center now clears the stale ringing interaction assignment before putting the work back into the queue, so the same agent is not left falsely at capacity for the next inbound offer.
- Reconnect-driven queued-voice recovery also repairs a stale ringing offer that no longer has an active reservation before it asks routing for the next queued call, so an abandoned old offer cannot keep the agent falsely at capacity forever.
- Queue sign-in and sign-out now also synchronize the live agent-session membership used by the real-time layer, so a soft-phone sign-out immediately removes the current browser session from the signed-in queue and campaign state instead of waiting for a reconnect.
- **Set your presence** from the presence button at the top of the workspace. Choose **Available** to receive work, pick a **reason code** (for example *Lunch* or *Coaching*) to go not-ready, or choose **Request break**. A break is granted immediately when nothing is being routed to you; if an offer is already in flight, you finish it and the break is granted automatically afterward.

The top bar also shows a live chip per signed-in queue with its current waiting count, so you can see where the pressure is.

### 2. Receive and answer an offer

When routing selects you for a piece of work, a **ringing offer card** appears with the customer name (or number), the queue, and a countdown showing how long you have to respond. You have two choices:

- **Accept** - accepts the reservation, connects the media, and moves the work into your active panel. For providers that ring your device (such as DialPad's soft phone), your device rings and you answer there; the workspace and the incoming-call modal now revalidate the current provider call state before accept and do not mark the interaction connected until the provider's authoritative event says it is connected. That means you do not get stuck on a call the server already ended while the offer was in flight. For server-side queue delivery on provider-only integrations such as the current Asterisk path, Contact Center still answers the live provider call during the authoritative accept so the connected call stays visible and controllable after the ringing offer is accepted.
- **Decline** - releases the offer so it is immediately re-offered to the next available agent, and the incoming modal no longer follows that reservation decline with a second raw telephony reject against the same call.

If you do not respond before the countdown ends, the offer is revoked and routed elsewhere.

Dialer work is distinguished from inbound queue offers by its activity source. When Preview, Power, Progressive, Predictive, or generic dialer inventory is assigned to you, the browser opens the assigned activity's shared **Complete activity** page automatically. Inbound work continues to show the ringing offer instead, so it is never redirected before you choose **Accept** or **Decline**.

### 3. Handle the active interaction

Once you accept, the **active interaction** panel shows:

- The **customer**, with a link to open the full CRM contact record (customer 360).
- The **direction** (inbound or outbound), the current **call status**, and a **live talk timer**.
- The **queue** the work came from.
- A **Complete activity** link that opens the same Omnichannel CRM completion page used by manual activities.

Use the soft phone for hold, mute, transfer, and hang-up. When a call is held, the keypad becomes available for a second call. The soft phone lists every active interaction by phone number and state, lets the agent select the current call, conferences two selected calls without requiring a provider call id, and can disconnect all active calls. The workspace reflects call state in real time.

Consultative transfer remains provider-dependent. Asterisk currently supports blind transfer and two-call conference but rejects warm transfer; DialPad exposes its provider transfer and merge actions when configured. The shared Telephony contract does not yet expose an agent-specific conference participant or leave-conference operation, so the UI does not claim that an agent can leave a conference while keeping all remote participants connected.

The soft phone also keeps the active remote number visible while you are on the call, and the **Recent** tab now includes inbound calls as well as outbound history.

When Contact Center owns the assigned voice interaction, server-side call-session changes now flow back into the Telephony soft phone in real time, so provider-side disconnects, failed calls, transfers, hold/resume, mute/unmute, and other normalized call-state changes immediately update the live call card and the persisted **Recent** history instead of waiting for the next browser reconnect.

For an answered call, a terminal provider event moves the agent from **Busy** to **Wrap-up** immediately. Wrap-up is not a timed auto-return: the agent reviews the CRM context, selects the disposition, records notes or scheduling changes, and completes the activity. Completion records the wrap-up end time and returns the agent to a previously requested break when one is pending; otherwise it returns the signed-in agent to **Available** and routing can offer the next call. This avoids sending another call while after-call work is unfinished.

Presence changes and queued-call recovery run as separate operations. The soft phone no longer constructs the voice routing graph inside the same presence-change request, which prevents a circular activation or pending YesSql flush from leaving the presence control spinning indefinitely.

Contact Center also runs a provider-truth reconciliation pass when the tenant activates and on a periodic safety cadence. If Orchard Core restarts during busy hours, persisted ringing or active interactions are revalidated against the telephony server before routing resumes, and a pre-connect offer that already ended on the provider side is removed from the queue instead of being re-offered as a ghost call.

If a prior terminal provider event was already recorded in the call session but another recovery path left the interaction nonterminal, reconciliation now repairs the interaction from the terminal call session before capacity is evaluated, then clears stale queue, reservation, and agent state. This prevents an ended call from consuming the agent's `MaxConcurrentInteractions` slot indefinitely.

For inbound server-side calls, a provider may report the caller leg as connected before an agent accepts the Contact Center offer. Ended-offer cleanup therefore uses the accepted reservation or assigned queue item—not the provider leg's answered timestamp—to decide whether the work reached an agent. A terminal call that was only waiting or reserved is removed and releases the agent so routing can continue to the next live call.

### 4. Complete the activity in the CRM

When the conversation ends, click **Complete activity** in the active panel. This opens the shared Omnichannel completion page for the assigned activity, so contact-center work follows the same CRM experience as manual activities:

1. Review the customer/contact context and open the customer record when details need correction.
2. Review activity details such as campaign, channel, urgency, schedule, instructions, and assignee.
3. Update the subject details captured by the activity's subject content type.
4. Choose a **disposition** from the list defined by the activity's subject flow.
5. Add notes when needed and submit the completion form.

Completing routes through the shared disposition path, which applies the disposition, marks the activity completed, and runs the subject flow's follow-up actions - the same path used everywhere in the CRM, so inbound, outbound, and manual work all behave consistently. If the subject flow **requires** a disposition, completion is blocked until you pick one and the completion page shows why.

The workflow preview renders subject- and action-derived titles with DOM text nodes rather than HTML injection. Stored CRM text is displayed literally and cannot create markup or execute script in the agent's browser.

Completion links opened from Contact Center include a local return location. After dialer work is completed or cancelled, the agent returns to **My workspace**; manual activity completion keeps the default **Activities** destination. Return locations are accepted only when they are local application URLs.

### 5. Review recent activity

The **Recent activity** panel lists your most recent interactions with their direction, outcome, and time, so you can quickly refer back to your last few contacts.

## How it works

- The workspace loads a **state snapshot** from the server and then keeps itself current from the real-time hub's presence, offer, and queue events. It re-reads the authoritative state after you act, so what you see always matches the server.
- Contact Center domain events are persisted immediately and the handler fan-out runs as deferred outbox work, so slow workflow or real-time projections do not block the soft-phone sign-in or sign-out postback.
- **Accept** calls a single server-side command that accepts the reservation, revalidates the provider's current call state, tells the voice provider to connect the call when needed, and advances the interaction and call session only when the provider truth supports that transition.
- **Complete** goes through the source-neutral `IActivityDispositionService`, so dispositions, required-disposition rules, and subject-flow actions behave identically across every channel and source.

## Permissions and roles

| Permission | Grants |
| --- | --- |
| `ContactCenterSignIntoQueues` | Sign in to queues/campaigns, change own presence, and use the Agent Workspace (accept/decline offers, complete work). |
| `MonitorContactCenter` | Open the Supervisor Dashboard and watch queues in real time. Included in the **Supervisor** role. |
| `ManageContactCenterQueues`, `ManageContactCenterAgents`, `ManageContactCenterSkills`, `ManageContactCenterDialer` | Configure the routing environment (queues, agents, skills, dialer). See [Agents, Queues & Dialer](agents-queues-dialer.md). |
