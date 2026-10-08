---
sidebar_label: Skills & Entitlements
sidebar_position: 21
title: Skills and Agent Entitlements
description: Define routable skills, then decide which queues and campaigns each agent may sign in to and how skilled they are.
technical_manual:
  - contact-center/agents-queues-dialer
---

**Skills** describe what an agent can do, such as *Spanish*, *Billing* or *Tier 2 support*. Queues can require or prefer skills. **Agent entitlements** decide which queues and campaigns an agent may sign in to, in what order the agent is offered work, and which skills the agent holds.

| | |
| --- | --- |
| **Menu** | Interaction Center > Management > Skills, and Interaction Center > Management > Agent entitlements |
| **Permission** | Manage Contact Center skills; Manage Contact Center agents |
| **Features** | Skills come with Contact Center Work Distribution. Entitlements are the optional **Contact Center Agent Entitlements** feature. |

<AskYourAdmin />

<video controls preload="metadata" width="100%" aria-label="Screencast of creating a skill and granting an agent queues, campaigns and skills on the agent entitlements screen">
  <source src="/img/docs/um-skills-entitlements.mp4" type="video/mp4" />
</video>

## Create a skill

1. Open **Interaction Center > Management > Skills** and click **Add Skill**.
2. Enter a **Name** (for example *Spanish*) and an optional **Description**.
3. Leave **Enabled** ticked and click **Save**.

A disabled skill stays on the agents and queues that already use it, but it can no longer be picked for new ones.

## Grant an agent queues, campaigns and skills

1. Open **Interaction Center > Management > Agent entitlements** and click **Add agent entitlements**.
2. Click **Browse users**, search for the person and pick them as the **User**. Each user has one entitlement record; the screen tells you if one already exists.
3. Under **Allowed queues**, pick every queue the agent may sign in to. Removing a queue later also signs the agent out of it.
4. Under **Queue preferences**, set a **Priority** for each allowed queue (a lower number is served first) and an optional **Delay (s)**, which makes a caller wait that many seconds before this agent is offered work from that queue. Use delays to make an agent a backup for a queue.
5. Under **Allowed campaigns**, pick the dialer campaigns the agent may work.
6. Under **Skills**, add each skill with a **Proficiency** from 1 (basic) to 5 (expert).
7. Click **Save**.

The list shows how many queues and campaigns each agent is allowed. Entitlement records cannot be deleted; remove the queues and campaigns instead.

:::info[Supervisors need entitlements too]
The [live dashboard](live-dashboard.md) and [shared voicemail](voicemail.md#shared-voicemail) show a supervisor only the queues and campaigns on **their own** entitlement record. Give each supervisor an entitlement record with the queues they supervise, or their dashboard is empty.
:::

:::note[When the entitlements feature is off]
Without the Agent Entitlements feature, any agent may sign in to any queue or campaign, and there is no screen to give agents skills. Turn the feature on as soon as queues need skills or you have more than one team.
:::
