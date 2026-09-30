---
sidebar_label: Dispositions
sidebar_position: 12
title: Dispositions
description: Create the outcomes agents and the AI pick when an activity is done, such as No answer, Call back, Sold or Do not call.
---

A **disposition** is the outcome of an activity: *No answer*, *Call back*, *Lead won*, *Do not call*. Agents pick one when they complete an activity, and an AI conversation picks one when it ends. The [subject flow](subject-flows.md) decides what each disposition does next.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Dispositions |
| **Permission** | Manage dispositions |
| **Feature** | Omnichannel Management |

<video controls preload="metadata" width="100%" aria-label="Screencast of creating three dispositions with descriptions">
  <source src="/img/docs/um-dispositions.mp4" type="video/mp4" />
</video>

## Create a disposition

1. Open **Interaction Center > Management > Dispositions** and click **Add Disposition**.
2. Enter a **Name** and an optional **Description**.
3. Pick an **Outcome** if the disposition has a special meaning (see below), then **Save**. Names must be unique.

After it is saved, a disposition's name cannot be changed; only its description can. Deleting a disposition does not check whether a subject flow still uses it, so remove it from the flows first.

A disposition only appears on an activity's Complete page when the activity's subject flow has an action for it.

## Outcomes

Set an **Outcome** only on a disposition the platform should apply on its own, when a call ends in a way nobody chose. Leave it at **None** for everything else, including dispositions that only agents or the AI conversation pick. The disposition list shows the outcome as a badge next to the name.

| Outcome | Applied automatically by | When | Also does |
| --- | --- | --- | --- |
| **None** | Nobody | Never. | Nothing extra. |
| **Number not in service** | The dialer and automated (AI) calls | The network rejects the call as not in service. | Adds the number that was called to the [Numbers Not In Service](numbers-not-in-service.md) list, whoever picks it, so it is never loaded or dialed again. |
| **No answer** | Automated (AI) calls | Nobody spoke on the call. | Nothing extra. |
| **Busy** | Automated (AI) calls | The network reports the line busy. If no disposition has this outcome, *No answer* is used. | Nothing extra. |
| **Answering machine** | Automated (AI) calls | The call reached voicemail. If no disposition has this outcome, *No answer* is used. | Nothing extra. |

The dialer does not disposition a call that rang out, was busy or reached a machine: it dials the record again after the retry delay.

When the platform needs a disposition for an outcome, it uses the one with that outcome in the activity's subject flow, so the flow's actions for it run (for example **Try Again** for *No answer*). For *Number not in service*, a disposition with the outcome that is not in the flow is used too, and one named *Number Not In Service* is created if none exists. If no disposition has the *No answer*, *Busy* or *Answering machine* outcome, an unanswered automated call uses the disposition the subject's **Try Again** action is wired to.

To set a contact's Do Not Call when a disposition is picked, use **Set do not call** on the disposition's action in the [subject flow](subject-flows.md).
