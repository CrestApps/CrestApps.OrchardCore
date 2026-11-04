---
sidebar_label: Dispositions
sidebar_position: 12
title: Dispositions
description: Create the outcomes agents and the AI pick when an activity is done, such as No answer, Call back, Sold or Do not call.
technical_manual:
  - omnichannel/management
  - contact-center/agents-queues-dialer
---

A **disposition** is the outcome of an activity: *No answer*, *Call back*, *Lead won*, *Do not call*. Agents pick one when they complete an activity, and an AI conversation picks one when it ends. The [subject flow](subject-flows.md) decides what each disposition does next.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Dispositions |
| **Permission** | Manage dispositions |
| **Feature** | Omnichannel Management |

<AskYourAdmin />

<YouTube video="contact-center-dispositions" title="Video overview of dispositions: why they exist, creating them, outcomes, and which disposition the platform uses" />

<YouTube video="contact-center-setup-walkthroughs" title="Video of creating three dispositions with descriptions" start={567} end={609} />

## Create a disposition

<YouTube video="contact-center-dispositions" title="Video of creating three dispositions" start={60} end={118} />

1. Open **Interaction Center > Management > Dispositions** and click **Add Disposition**.
2. Enter a **Name** and an optional **Description**.
3. Pick an **Outcome** if the disposition has a special meaning (see below). Names must be unique.
4. Optionally, pick **Set lead status** (when the CRM features are on). When an activity that belongs to a lead is completed with this disposition, in any subject, the lead moves to that status. A subject action for the disposition that sets its own lead status takes precedence. It has no effect on contacts. Leave it on **Leave the status as it is** otherwise.
5. Click **Save**.

For example, a test-drive campaign might use *Test Drive Booked*, *Call Back Later* and *Not Interested*. A clear description helps agents, and the AI, tell the outcomes apart.

<YouTube video="contact-center-dispositions" title="Video of what can change after a disposition is saved" start={118} end={140} />

After it is saved, a disposition's name cannot be changed (the edit page says *The disposition name is fixed after creation*); only its description and its other settings can. Each row of the list has an **Edit** and a **Delete** button. Deleting a disposition does not check whether a subject flow still uses it, so remove it from the flows first.

A disposition only appears on an activity's Complete page when the activity's subject flow has an action for it.

## Outcomes

<YouTube video="contact-center-dispositions" title="Video of the outcomes the platform applies by itself" start={140} end={217} />

Set an **Outcome** only on a disposition the platform should apply on its own, when a call ends in a way nobody chose. Leave it at **None** for everything else, including dispositions that only agents or the AI conversation pick. The disposition list shows the outcome as a badge next to the name. In the **Outcome** list the entries read, for example, *Number not in service (dialer and automated calls)* or *Rejected (dialer)*, which says who applies them.

| Outcome | Applied automatically by | When | Also does |
| --- | --- | --- | --- |
| **None** | Nobody | Never. | Nothing extra. |
| **Number not in service** | The dialer and automated (AI) calls | The network rejects the call as not in service. | Adds the number that was called to the [Numbers Not In Service](numbers-not-in-service.md) list, whoever picks it, so it is never loaded or dialed again. |
| **No answer** | The dialer and automated (AI) calls | Nobody answered (the dialer), or the customer did not speak on the call (automated calls). | Nothing extra. |
| **Busy** | The dialer and automated (AI) calls | The network reports the line busy. If no disposition has this outcome, *No answer* is used. | Nothing extra. |
| **Answering machine** | The dialer and automated (AI) calls | The dialer's answering-machine screening hung up on a machine, or an automated call reached voicemail. If no disposition has this outcome, *No answer* is used. | Nothing extra. |
| **Rejected** | The dialer | The called party or the network declined the call. If no disposition has this outcome, *No answer* is used. | Nothing extra. |
| **Call failed** | The dialer | The network failed the call, or the provider refused to place it. If no disposition has this outcome, *No answer* is used. | Nothing extra. |
| **Disconnected** | The dialer and automated (AI) calls | A customer answered but hung up before the agent was connected (the dialer), or spoke on an automated call but decided nothing the subject's dispositions record, on a subject whose **Try Again** action is wired to none. For the dialer, if no disposition has this outcome, *No answer* is used. | Nothing extra. |

The dialer dispositions every call that ends before an agent is connected, so the agent never receives it; see [Dialer profiles](dialer-profiles.md#what-happens-to-each-record).

<YouTube video="contact-center-dispositions" title="Video of which disposition the platform uses" start={217} end={265} />

When the platform needs a disposition for an outcome, it uses the one with that outcome in the activity's subject flow, so the flow's actions for it run (for example **Try Again** for *No answer*). For a dialer call it then tries, in order: the flow's *No answer* disposition, the disposition the subject's **Try Again** action is wired to, any disposition with the outcome (or with *No answer*), and a disposition already named for the outcome. If there is still none, one is created: *No Answer*, *Busy*, *Answering Machine*, *Rejected*, *Call Failed* or *Disconnected*. A created disposition is not in any subject flow, so add it to the flow and wire **Try Again** to it to have the contact called again. For *Number not in service*, a disposition with the outcome that is not in the flow is used too, and one named *Number Not In Service* is created if none exists. An automated (AI) call never creates one. A call on which the customer said nothing takes, in order: the flow's disposition with the outcome (or with *No answer*), the disposition the subject's **Try Again** action is wired to, and any disposition with the outcome (or with *No answer*). A call on which the customer spoke takes the disposition the AI chose from the flow, from what the customer said; when the customer decided nothing those dispositions record, it takes the one wired to **Try Again**, so the contact is called again, and then any disposition with the *Disconnected* outcome. Only when none of these exists is the flow's first disposition used, so every call is still concluded.

To set a contact's Do Not Call when a disposition is picked, use **Set do not call** on the disposition's action in the [subject flow](subject-flows.md).
