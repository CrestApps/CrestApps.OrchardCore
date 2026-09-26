---
sidebar_label: Agent & Supervisor User Manual
sidebar_position: 3
title: Contact Center Agent and Supervisor User Manual
description: A task-oriented, searchable how-to manual for every day-to-day Contact Center action an agent or supervisor performs, with step-by-step instructions and screencast demos.
---

This is the task-oriented user manual for the two people who operate the Contact Center every day:

- The **agent**, who signs in, takes calls from a queue, works outbound records, and wraps up each contact with a disposition.
- The **supervisor** (manager), who prepares the routing environment, watches the live dashboard, and monitors, whispers to, or barges into live calls.

Each task below is written as an independent, searchable **how-to** with its own heading, prerequisites, and numbered steps, so you can jump straight to the one action you need. For the concepts and architecture behind these tasks, see [Agents, Queues & Dialer](agents-queues-dialer.md) and [Agent Desktop & Dashboard](agent-desktop.md).

:::info Where the audio lives
The [Telephony soft phone](../telephony/index.md) is where call audio and the device controls (dial pad, hold, mute, transfer, hang up) live. The **Agent Workspace** (*Interaction Center → My workspace*) adds the CRM context, work offers, and wrap-up. Keep both open during a shift.
:::

## About the screencasts

Every task in this manual is paired with a short screencast so the written steps and the on-screen actions stay in sync. The recordings follow the same convention as the rest of the documentation site: full-screen capture at **1600×1000**, delivered as **MP4** (H.264, `yuv420p`) under `static/img/docs/`, and embedded with an HTML `<video controls preload="metadata" width="100%">` player.

The exact click path, the audit checklist, and the target file name for each recording are collected in the [Screencast library](#screencast-library) at the end of this page. Tasks whose demo is not yet published show a **Screencast** callout with the file name that will be used, so the video can be dropped in without editing the surrounding steps.

## Roles and permissions

| You are a… | Grant the role/permission | It lets you… |
| --- | --- | --- |
| Agent | `ContactCenterSignIntoQueues` | Sign in to queues/campaigns, change your own presence, accept/decline offers, and complete work in **My workspace**. |
| Supervisor | **Supervisor** role (includes `MonitorContactCenter` and `ContactCenterInterveneInCalls`) | Open the **Live dashboard**, listen to, whisper to, or barge into live calls, and message agents. `ContactCenterInterveneInCalls` adds taking a call over, ending, transferring and recording it, and setting an agent's state. |
| Administrator | `ManageContactCenterQueues`, `ManageContactCenterAgents`, `ManageContactCenterSkills`, `ManageContactCenterDialer` | Create queues, agents, skills, reason codes, business hours, entry points, and dialer profiles. |

Confirm your role with your administrator before starting. Agents only see the queues and campaigns they are **entitled** to; supervisors only see monitoring actions the active voice provider actually supports.

---

# Part 1 — Agent tasks

Everything in this part happens in the floating **Telephony soft phone** and the **My workspace** desktop.

## How to sign in to a queue or campaign

**Prerequisites:** the `ContactCenterSignIntoQueues` permission and at least one queue or campaign entitlement assigned by your administrator.

1. Open **Interaction Center → My workspace** and make sure the Telephony soft phone is visible.
2. In the soft phone, open the **Work** tab.
3. In **Select queue(s)** and/or **Select campaign(s)**, choose the memberships you want to receive work from. You can only pick memberships you are entitled to.
4. Click **Sign in**. Your presence changes to **Available** and the Work tab lists every queue and campaign you are now signed in to.
5. If inbound calls are already waiting in one of those queues, routing offers you the next queued call immediately after sign-in.

To leave a single membership, use its **Sign out** action; to leave everything, use **Sign out of all**. Sign-in and sign-out happen over the real-time hub, so the page does not reload.

:::note Screencast
`contact-center-agent-signin.mp4`
:::

## How to set your presence to Available

**Prerequisites:** you are signed in to at least one queue or campaign.

1. In the soft-phone header, open the **presence** dropdown.
2. Choose **Available**.

Returning to **Available** immediately asks routing to offer any call already waiting in your signed-in queues, so you do not have to wait for a new inbound event.

:::note Screencast
`contact-center-agent-presence.mp4`
:::

## How to request a break

**Prerequisites:** you are signed in.

1. Open the **presence** dropdown in the soft-phone header.
2. Choose **Request break**.

A break is **system-approved**:

- If nothing is being routed to you, the break is granted immediately and your presence becomes **Break**.
- If an offer or call is already in flight, the request is kept **pending**; you finish the current work and the break is granted automatically afterward.

While in **RequestBreak** or **Break** you are not eligible for new routing decisions. Return to **Available** when you are ready for work again.

:::note Screencast
`contact-center-agent-break.mp4`
:::

## How to go not-ready with a reason code

**Prerequisites:** your administrator has defined reason codes under *Interaction Center → Management → Agent states*.

1. Open the **presence** dropdown.
2. Pick a **reason code** (for example *Lunch*, *Coaching*, or *Team meeting*).

The reason sets your presence to the state the administrator mapped it to (for example `Break`, `Meeting`, or `Training`) and records the reason on your profile and in the audit trail. If no reason codes are configured, the dropdown falls back to the built-in not-ready states.

:::note Screencast
`contact-center-agent-reason-code.mp4`
:::

## How to receive and accept an inbound call from a queue

**Prerequisites:** you are signed in to the queue and your presence is **Available**.

1. When routing selects you, a **ringing offer card** appears showing the customer (name or number), the source **queue**, and a **countdown**.
2. Click **Accept** before the countdown ends.
3. The reservation is accepted, the media connects, and the work moves into your **active interaction** panel. (Until then the panel reads **No active interactions right now**.)
   - On providers that ring your device (for example Dialpad), your device rings and you answer there.
   - On server-side delivery (for example the bundled Asterisk path), the call is connected during accept and stays controllable from the soft phone.
4. If you do nothing before the countdown ends, the offer is revoked and re-routed to another agent.

:::note Screencast
`contact-center-agent-accept-inbound.mp4`
:::

## How to decline an offer

**Prerequisites:** a ringing offer card is visible.

1. Click **Decline** on the ringing offer card.
2. The offer is released and immediately re-offered to the next available agent.

:::note Screencast
`contact-center-agent-decline.mp4`
:::

## How to put a call on hold and resume it

**Prerequisites:** you are on an active call and the provider advertises the **Hold** capability.

1. In the soft phone, click **Hold**. The caller is placed on hold and the keypad becomes available for a second call.
2. Click **Resume** to return to the caller.

If the provider does not advertise Hold, the button is hidden.

:::note Screencast
`contact-center-agent-hold-resume.mp4`
:::

## How to mute and unmute

**Prerequisites:** an active call and provider **Mute** capability.

1. Click **Mute** to stop sending your microphone audio.
2. Click **Unmute** to resume.

:::note Screencast
`contact-center-agent-mute.mp4`
:::

## How to send DTMF digits (dial pad)

**Prerequisites:** an active call and provider **SendDigits** capability.

1. Open the soft-phone **keypad**.
2. Press the digits you need (for example an IVR menu selection). Each press sends a DTMF tone to the far end.

:::note Screencast
`contact-center-agent-dtmf.mp4`
:::

## How to transfer a call

**Prerequisites:** an active call and provider **Transfer** capability.

1. Click **Transfer** on the soft phone. The transfer panel opens inside the phone, in place of the keypad; the back arrow returns to the keypad.
2. Search the directory by name or extension and pick an entry, or type where to send the call. Like the keypad, the panel has a **Transfer to an extension** / **Transfer to a phone number** toggle under the field:
   - In **number** mode the field is the keypad's country-flag field: the number is read and checked the way the keypad checks one, so an incomplete number and the phone system's own number are refused before anything is sent.
   - In **extension** mode type the colleague's extension (for example `2`); it is sent as an extension, never dialed as a phone number.

   Picking someone from the directory works in either mode.
3. When the call offers both, choose **Blind** (the call is sent straight to them) or **Warm** (you speak to them first). A call that supports only one shows no choice.
4. Click **Transfer** (or press Enter).

### Transferring a Contact Center call

When the call came to you through the Contact Center (a queue, a direct line or a campaign), the Contact Center carries the transfer rather than the phone provider, and the panel lists:

- **Agents** — every other agent with their extension and presence. Only agents shown as **Available** can be picked; the others are listed so you can see why.
- **Queues** — every enabled queue, with how many callers are waiting in it.
- **Outside numbers** — the approved external destinations, when you have the **Transfer Contact Center calls externally** permission. A number that is not on that list can be typed only when an administrator turned on **Let agents transfer to numbers that are not on this list** (*Settings → Contact Center*). Emergency, premium-rate and the contact center's own numbers are always refused.

An extension typed in extension mode is the agent it rings: the transfer (blind or warm) goes to that agent exactly as if you had picked them from the list. An extension that belongs to nobody, or to someone who is not a Contact Center agent, is refused with the reason.

**Blind transfer**

- **To an agent:** the caller hears the queue's hold music while the call rings on that agent's phone as a normal offer. If they do not answer within the ring window, the caller goes to that agent's voicemail.
- **To a queue:** the caller joins the queue, keeping at least the priority they had, and is offered to the next available agent; with nobody free they hear the queue's treatment.
- **To an outside number:** the call leaves the contact center and is connected to that number from the platform's own number.

In every case the call leaves your phone at once and you go into wrap-up, or straight back to ready for a direct call, exactly as when a call ends.

**Warm (consult) transfer**

1. Choose **Warm**, pick an available agent or an outside number, and click **Transfer**. The caller is put on hold with the queue's hold music and the destination is rung. The panel shows **Calling…**; you can **Cancel transfer** at any time.
2. When they answer, the panel shows **Talking to…**: you are speaking to them privately while the caller holds.
3. Click **Complete transfer** to join the caller to them and leave the call, or **Cancel transfer** to drop them and return to the caller.

If the person you consulted hangs up or does not answer within 30 seconds, the caller comes straight back to you and the panel says so. If the caller hangs up during the consult, the consult ends and the person you consulted is released. A queue cannot be consulted; send the call to a queue with a blind transfer.

:::caution Provider differences
Transfer support is provider-dependent. The bundled **Asterisk** provider supports **blind transfer** and a **two-call conference** but rejects warm (consultative) transfer. **Dialpad** exposes its own transfer and merge actions when configured. **Telnyx** supports every Contact Center transfer above, including the consult. Only the actions the active provider supports are shown.

On **Telnyx**, a number you dial from the keypad is connected by the phone system: your phone rings its own line for a moment and answers it by itself, and the number is dialed from there. Such a call can be transferred -- blind or warm, to a number or an extension -- and merged like any other. If the phone system cannot connect the call that way -- for example your phone is still registering -- the phone dials the number itself, and that call cannot be transferred or merged: the transfer panel says so instead of offering a target, and its line in **Active calls** shows a disabled checkbox.

**Transferring a keypad call on Telnyx**

- **Blind:** the caller is put on hold (they hear the hold tone) and the destination is rung. The panel shows **Calling…** with **Cancel transfer**. When the destination answers, the call is handed to them and leaves your phone. If they decline or do not answer within 30 seconds, the panel says so and the caller is still with you, on hold -- click **Resume**. If you hung up in the meantime, a colleague's caller is sent to that colleague's voicemail.
- **Warm:** the caller is put on hold and your phone rings a second, consult call by itself; you talk to the destination on it. Once they answer, click **Complete transfer** to hand them the caller, or **Cancel transfer** to drop them and take the caller off hold. If they hang up first, the caller is still with you, on hold. If the caller hangs up during the consult, the consult carries on as an ordinary call. Hanging up the consult after they answered hands them the caller, as on most phone systems.
- **Receiving one:** a call a colleague hands you rings your phone with **Answer** and **Decline**, showing the caller's number and *Transferred by* and the colleague's name. Declining it gives the caller back to the colleague. Once answered it is an ordinary call on your phone: you can hold it, transfer it again or merge it.
:::

:::note Screencast
`contact-center-agent-transfer.mp4`
:::

## How to conference two calls

**Prerequisites:** two active calls (for example the original caller on hold plus a second call) and provider **Merge** capability.

1. With two or more calls up, the soft phone's **Active calls** list on the keypad shows a checkbox beside every call and a **Merge calls** button, which stays disabled until two or more calls are ticked.
2. Tick the calls to join, or tick **Select all**. The button then reads **Merge 2 calls**, **Merge 3 calls** and so on, and the line beside it names the calls it joins.
3. Click it. The calls are merged; no provider call id is required.
4. The conference is listed under its own **Conference** heading with its participants. Each participant has its own hang-up button, which ends only that participant's call.

To get the second call in the first place, click **Add call** during a call:

- The current call is put on hold. The keypad opens with an empty number field and the held call is listed above it.
- Type the number and press **Enter** (or click the green call button).
- Changed your mind? Click **Back to call** or press **Escape** to take the held call off hold.

To add another call to a running conference, tick any participant of the conference and the new call; the button reads **Add to conference**, and the new call joins the same conference rather than starting a second one.

**Leaving a conference or ending it for everyone.** In a conference, **Hang up** reads **Leave**: you drop out and everyone else stays connected to each other. A Contact Center caller is never disconnected by you leaving; their call stays on your phone. If only one other person is left, **Hang up** simply ends the call, so nobody is left alone on the line. To end the call for everyone, click **End for all** and confirm. The confirmation starts on **Keep talking**, so pressing Enter does not end the call by accident.

:::note Screencast
`contact-center-agent-conference.mp4`
:::

## How to end a call (hang up)

**Prerequisites:** an active call.

1. Click the red **Hang up** to end the selected call. It sits with your own call controls: **Mute**, **Hold** and **Keypad**. The keypad stays closed during a call until you open it to send digits. With two or more calls up, **End all** (outlined in red, in the row with **Transfer** and **Add call**) ends every call after you confirm. In a conference, see [leaving a conference or ending it for everyone](#how-to-conference-two-calls).

When an answered call ends, a terminal provider event moves you from **Busy** to **Wrap-up** so you can complete after-call work.

:::note Screencast
`contact-center-agent-hangup.mp4`
:::

## How to complete after-call work (wrap-up) and become available again

**Prerequisites:** the call has ended and you are in **Wrap-up**.

Wrap-up is **not** a timed auto-return — you control when it ends by completing the activity:

1. In the active interaction panel, click **Complete activity**. This opens the shared Omnichannel completion page for the assigned activity.
2. Review the customer/contact context and open the customer record if details need correcting.
3. Review the activity details (campaign, channel, urgency, schedule, instructions, assignee).
4. Update the subject details captured by the activity's subject content type.
5. Choose a **disposition** from the list defined by the activity's subject flow. If the subject flow requires a disposition, completion is blocked until you pick one.
6. Add notes if needed and **submit**.

Completion records the wrap-up end time and returns you to a **pending break** if you requested one during the call; otherwise it returns you to **Available** so routing can offer the next call. This is what prevents a new call from arriving before after-call work is finished.

:::note Screencast
`contact-center-agent-wrapup.mp4`
:::

## How to preview-dial a record

**Prerequisites:** you are signed in to a campaign whose dialer profile uses the **Preview** mode.

1. When preview work is assigned to you, the browser automatically opens the assigned activity's **Complete activity** page so the record is ready — no extra navigation.
2. Review the customer and activity context before dialing.
3. Place the call from the soft phone (see [manual dialing](#how-to-place-a-manual-outbound-call)), talk to the customer, then complete the activity with a disposition.

:::note Screencast
`contact-center-agent-preview-dial.mp4`
:::

## How to place a manual outbound call

**Prerequisites:** the soft phone is connected and the provider advertises **Dial**.

1. Open the soft-phone **keypad**.
2. Enter the destination in E.164 form (or select the country and type the national number — the field normalizes it).
3. Click **Call**. Use hold, mute, transfer, and hang up as needed during the call.

:::note Screencast
`contact-center-agent-manual-dial.mp4`
:::

## What to expect in power and progressive (automated) dialing

**Prerequisites:** an administrator has enabled **Contact Center Paced Dialing** and created a Power or Progressive dialer profile pointing at your queue. Automated pacing is compliance-gated.

In automated modes you do **not** press dial. When the pacer connects a customer and reserves you:

1. A work assignment arrives and its **Complete activity** page opens automatically.
2. Handle the conversation on the soft phone exactly like an inbound call.
3. Complete the activity with a disposition to return to **Available** for the next paced call.

Pacing, retry, do-not-call, and calling-window rules are enforced by the profile — see [Dialer](agents-queues-dialer.md#dialer).

:::note Screencast
`contact-center-agent-paced-dial.mp4`
:::

## How to protect sensitive data (credit cards) in a recording

Recording is orchestrated by the Contact Center **recording service**, which supports **Start**, **Pause**, **Resume**, and **Stop**. `Pause` is intended for exactly this scenario — suppressing capture *"while sensitive data is captured"* — and it emits the auditable `Recording paused` / `Recording resumed` events.

How this reaches a live call today:

1. **Automation-driven (supported now).** Bracket the sensitive step with recording state changes so the card number is never captured. Use the **Start Call Recording** and **Stop Call Recording** workflow tasks (or a custom automation that calls the recording service's `PauseAsync`/`ResumeAsync`) around the payment step, then resume normal recording.
2. **Provider-side (when available).** Some voice providers can pause media capture on their side; when the provider advertises it, the pause/resume flows through the same recording service and audit events.

:::caution Availability
There is **no dedicated agent "pause recording" button** in the Agent Workspace today. Sensitive-data suppression is driven by automation or the provider, not by a self-service control on the desktop. Do not tell customers a card segment is excluded unless your tenant has configured one of the mechanisms above. Recording access and deletion are additionally governed by the recording governance and erasure services.
:::

:::note Screencast
`contact-center-agent-recording-pause.mp4`
:::

## How to review your recent activity

**Prerequisites:** you have handled at least one interaction.

1. In the soft phone, open the **Recent** tab (inbound and outbound history), or use the **Recent activity** panel in **My workspace**.
2. Review the direction, outcome, and time of your last interactions before taking the next offer.

:::note Screencast
`contact-center-agent-recent.mp4`
:::

---

# Part 2 — Supervisor and manager tasks

Supervisors prepare the environment and monitor operations. The configuration screens live under **Interaction Center → Management**; the live tools live under **Interaction Center → Live dashboard**. For the full manager runbooks, see [Agent Desktop & Dashboard](agent-desktop.md#for-contact-center-managers-preparing-the-environment).

## How to create an inbound queue

**Prerequisites:** the `ManageContactCenterQueues` permission and the **Work Distribution** (Queues) feature enabled. Define any required **Skills** and **Business hours** first.

1. Go to **Interaction Center → Management → Queues** and click **Add** (create).
2. Give the queue a **name** and, for inbound voice, associate the dialed number (DID) that feeds it.
3. Choose the **routing strategy** (longest-idle, round-robin, or least-busy) and, optionally, a sticky-agent preference.
4. Set the **SLA threshold** and the **reservation timeout**.
5. Add any **required skills** so routing only offers work to agents who have them.
6. Attach a **business-hours** calendar and choose the closed-hours behavior (hold, overflow, or close).
7. Optionally set an **overflow queue** for long waits.
8. **Save**. Then create an [Inbound entry point](agent-desktop.md#for-contact-center-managers-inbound-routing-runbook) that maps the DID to this queue, and sign a skilled agent in to test.

:::note Screencast
`contact-center-manager-create-queue.mp4`
:::

## How to configure a dialer (dialing) profile

**Prerequisites:** the `ManageContactCenterDialer` permission and the **Outbound Dialer** feature. A campaign and a target queue should already exist. Power/Progressive modes also require **Paced Dialing**.

1. Go to **Interaction Center → Management → Dialer Profiles** and click **Add**.
2. Select the **campaign** whose activities this profile dials.
3. Select the **queue** whose available agents the profile reserves.
4. Choose the **dialing mode**: Manual, Preview, Power, or Progressive.
5. Set the **pacing** and the **voice provider**.
6. Configure the **compliance** settings: retry delay, do-not-call/suppression, and the allowed **calling window**.
7. **Save**. The profile now governs how the campaign's activities are executed.

:::note Screencast
`contact-center-manager-dialer-profile.mp4`
:::

## How to load dialer inventory

**Prerequisites:** the `ManageActivityBatches` permission. Dialer inventory loads require an existing **dialer profile** so the loaded activities inherit the correct dialing mode and campaign.

1. Go to **Interaction Center → Management → Load Inventory**.
2. Start a new load and, in the creation dialog, **select the source first**:
   - **Manual work** — loads user-assigned activities.
   - **Dialer inventory** — loads **unassigned** work available for reservation; this choice requires a dialer profile.
3. For dialer inventory, choose the **dialer profile** so the activities inherit its dialing mode and campaign.
4. Provide the source data for the batch and start the load.
5. The loaded activities become available to routing/dialing according to the selected profile.

:::note Screencast
`contact-center-manager-load-inventory.mp4`
:::

## How to monitor the live dashboard (workload, queues, and productivity)

**Prerequisites:** the **Supervisor** role (or `MonitorContactCenter`) and the **Supervision & Live Dashboard** feature.

1. Open **Interaction Center → Live dashboard**. It connects to the real-time hub and refreshes automatically, so it can be left open on a wallboard.
2. Read the three sections:
   - **Summary metrics** — total items waiting across all queues, available agents, total agents, and queue count.
   - **Queue tiles** — per-queue waiting count, signed-in/available/busy agents, longest current wait, and SLA breaches. Tiles turn **amber** near the SLA and **red** once items breach it.
   - **Agent board** — every agent's live presence, current reason, and how many interactions they are handling.
3. Use it to spot a backing-up queue, an SLA breach, or too few available agents, then rebalance staffing or open a campaign.

:::note Screencast
`contact-center-manager-live-dashboard.mp4`
:::

## How to monitor a live call

**Prerequisites:** an agent is on a live call, and the active voice provider advertises **Monitor** and implements the executable monitoring contract (the bundled Asterisk Contact Center Voice provider and the [Telnyx](../telephony/telnyx.md#supervisor-monitoring) provider do). You hear the call through **your own soft phone**, so keep it open: the platform rings it for you and it answers by itself.

1. On the **Live dashboard** agent board, find the agent who is on a call.
2. Click **Listen** on that agent's card. You listen to the live call silently; neither party hears you.
3. The card shows **You: Listen** (or **Connecting your phone…** until your phone answers), and your soft phone shows a **Monitoring** banner with the agent's name. The banner, not a call row, is how your phone shows it.

The action invokes the provider first and only records the audited monitoring event after the provider confirms success. If the provider does not support the action, the button is not shown; an agent on a call that cannot be monitored — one that is not a Contact Center interaction, such as a number they dialed from the keypad or an extension call, or one whose provider does not support monitoring — shows **Cannot be monitored**, with the reason in its tooltip.

### Switch mode, or stop

While you are on the call, the card's **Listen**, **Whisper** and **Barge** buttons (and the same switcher on your phone's banner) change how you are heard without ringing you again; the active one is shown pressed. **Stop** — on the card or the banner — hangs up only your leg: the call goes on for the agent and the customer exactly as before.

:::note Screencast
`contact-center-manager-monitor.mp4`
:::

## How to whisper to an agent

**Prerequisites:** the provider advertises **Whisper** and implements the contract.

1. On the **Live dashboard**, find the agent on a live call.
2. Click **Whisper** on the agent's card. You can coach the agent and only the **agent** hears you; the customer does not.

:::note Screencast
`contact-center-manager-whisper.mp4`
:::

## How to barge into a call

**Prerequisites:** the provider advertises **Barge** and implements the contract.

1. On the **Live dashboard**, find the agent on a live call.
2. Click **Barge** on the agent's card. You join the call as a full participant that **both** the agent and the customer hear.

:::note Screencast
`contact-center-manager-barge.mp4`
:::

## How to take over a call

**Prerequisites:** `ContactCenterInterveneInCalls`, an agent profile of your own, your soft phone open, and a provider that can hand a call to a supervisor (Telnyx can).

1. Click **Take over** on the agent's card. If you are not on the call yet, you barge in first, and the call is taken over as soon as your phone is on it.
2. The customer hears you before the agent is released. Your phone's banner changes to **You took over … 's call**, with **Hang up**.

From then on the call is yours: the interaction, its talk time and its after-call work are recorded against you, and the released agent goes to wrap-up (a queue call) or back to ready (a direct call). The takeover is audited as `SupervisorTookOver`, naming you and the agent.

## How to end, transfer or record a call

**Prerequisites:** `ContactCenterInterveneInCalls`.

Open **More ▾** on the agent's card:

- **End call…** asks you to confirm, then ends the call for everyone on it.
- **Transfer…** sends the call to a queue, an available agent or a phone number, as a blind transfer. Anyone listening is released first.
- **Start recording** / **Stop recording** turns the call's recording on or off, under the tenant's recording governance. Neither is possible while the agent has recording paused for a sensitive-data capture.

## How to set an agent's state or message them

From **More ▾** on any agent's card:

- **Set Available**, **Set Not ready** or **Set Break** changes the agent's state under the same rules as their own change: an agent on a call gets it when their work ends. **Sign out of queues** (after a confirm) signs them out. These need `ContactCenterInterveneInCalls`.
- **Message…** sends a short note (up to 500 characters) that appears on the agent's soft phone until they dismiss it. Messaging needs only `MonitorContactCenter`.

Every intervention is audited under your name (`SupervisorEndedCall`, `SupervisorTransferredCall`, `SupervisorChangedRecording`, `SupervisorSetAgentState`, `SupervisorMessagedAgent`, `SupervisorMonitorModeChanged`) and limited to agents and calls in the queues you supervise.

## How to review productivity with reports

**Prerequisites:** the **Reports & Analytics** feature and the reporting permissions.

1. Open the **Reports** area and choose a Contact Center report — for example the agent, queue/SLA, interaction, transfer, recording, or campaign report.
2. Apply filters (date range, queue, agent, campaign) and review the metrics. Talk time, wrap-up time, and average handle time come from the interaction timestamps recorded on each call.
3. Export to CSV when you need the data outside the dashboard.

See the [Enterprise report catalog](report-catalog.md) for every report, its formula, and its drill paths.

:::note Screencast
`contact-center-manager-reports.mp4`
:::

---

## Screencast library

The table lists every screencast that accompanies this manual. Capture each one full-screen at **1600×1000**, export to **MP4** (H.264, `-pix_fmt yuv420p`, even dimensions), save it under `src/CrestApps.Docs/static/img/docs/`, and replace the task's **Screencast** callout with the embed snippet below.

Embed snippet (replace `FILE`):

```html
<video controls preload="metadata" width="100%">
  <source src="/img/docs/FILE.mp4" type="video/mp4" />
</video>
```

| Task | File | Audience |
| --- | --- | --- |
| Sign in to a queue or campaign | `contact-center-agent-signin.mp4` | Agent |
| Set presence to Available | `contact-center-agent-presence.mp4` | Agent |
| Request a break | `contact-center-agent-break.mp4` | Agent |
| Go not-ready with a reason code | `contact-center-agent-reason-code.mp4` | Agent |
| Receive and accept an inbound call | `contact-center-agent-accept-inbound.mp4` | Agent |
| Decline an offer | `contact-center-agent-decline.mp4` | Agent |
| Hold and resume | `contact-center-agent-hold-resume.mp4` | Agent |
| Mute and unmute | `contact-center-agent-mute.mp4` | Agent |
| Send DTMF digits | `contact-center-agent-dtmf.mp4` | Agent |
| Transfer a call | `contact-center-agent-transfer.mp4` | Agent |
| Conference two calls | `contact-center-agent-conference.mp4` | Agent |
| End a call | `contact-center-agent-hangup.mp4` | Agent |
| Complete after-call work | `contact-center-agent-wrapup.mp4` | Agent |
| Preview-dial a record | `contact-center-agent-preview-dial.mp4` | Agent |
| Place a manual outbound call | `contact-center-agent-manual-dial.mp4` | Agent |
| Power/Progressive dialing experience | `contact-center-agent-paced-dial.mp4` | Agent |
| Protect sensitive data in a recording | `contact-center-agent-recording-pause.mp4` | Agent |
| Review recent activity | `contact-center-agent-recent.mp4` | Agent |
| Create an inbound queue | `contact-center-manager-create-queue.mp4` | Supervisor |
| Configure a dialer profile | `contact-center-manager-dialer-profile.mp4` | Supervisor |
| Load dialer inventory | `contact-center-manager-load-inventory.mp4` | Supervisor |
| Monitor the live dashboard | `contact-center-manager-live-dashboard.mp4` | Supervisor |
| Monitor a live call | `contact-center-manager-monitor.mp4` | Supervisor |
| Whisper to an agent | `contact-center-manager-whisper.mp4` | Supervisor |
| Barge into a call | `contact-center-manager-barge.mp4` | Supervisor |
| Review productivity with reports | `contact-center-manager-reports.mp4` | Supervisor |

:::caution Recording the operational (live-call) demos
The configuration demos (create queue, dialer profile, load inventory, live dashboard layout, presence, sign-in) can be captured against any tenant with the Contact Center features enabled. The **live-call** demos (accept from queue, hold/transfer/conference, monitor, whisper, barge, recording pause) require a working voice provider — the bundled **Asterisk Contact Center Voice** provider with real browser audio, or **Dialpad** — and at least one real inbound/outbound call in flight. Capture those against a provisioned voice environment as described in [Voice Routing](voice-routing.md) and [Asterisk](../telephony/asterisk.md).
:::
