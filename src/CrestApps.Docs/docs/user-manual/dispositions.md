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

<video controls preload="metadata" width="100%" aria-label="Screen cast of creating omnichannel dispositions">
  <source src="/img/docs/omni-dispositions.mp4" type="video/mp4" />
</video>

## Create a disposition

1. Open **Interaction Center > Management > Dispositions** and click **Add Disposition**.
2. Enter a **Name** and an optional **Description**, then **Save**. Names must be unique.

After it is saved, a disposition's name cannot be changed; only its description can. Deleting a disposition does not check whether a subject flow still uses it, so remove it from the flows first.

A disposition only appears on an activity's Complete page when the activity's subject flow has an action for it.
