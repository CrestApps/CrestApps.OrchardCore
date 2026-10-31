---
sidebar_label: Skills and Certifications
title: Skills and Certifications
description: "Keep one list of the skills people hold and how well they hold them, the certifications they need, and the certificates they have, with reminders before a certificate runs out."
technical_manual:
  - modules/skills
---

Skills and certifications tell the site who can do what. A **skill** is something a person can do, such as speaking Spanish or servicing boilers, held at a level from 1 (trainee) to 5 (expert). A **certification** is a licence or certificate that expires and has to be renewed, such as a gas safety certificate. A **certificate** is one person's copy of a certification, with its number and its expiry date.

The same skills are used across the site. When Contact Center is on, the skills on **Interaction Center > Management > Skills** and the agents' skills are the same ones you see here, so you set an agent's skills once.

| | |
| --- | --- |
| **Menu** | Workforce > Skills, Workforce > Certifications, Workforce > Certificates |
| **Permission** | Manage skills and certifications (the lists of skills and certifications), Assign skills and record certifications (giving people skills and recording their certificates), View skills and certifications |
| **Feature** | Skills |

<AskYourAdmin />

## Add a skill

Open **Workforce > Skills** and choose **Add skill**.

| Field | What it does |
| --- | --- |
| **Name** | The skill's name, such as "Spanish" or "Boiler servicing". Two skills can't have the same name, whatever the capital letters. |
| **Category** | A group the skill is listed under, such as Languages or Electrical. |
| **Description** | What the skill means, shown under its name in the list. |
| **Required certification** | When set, the skill only counts for a person on the days they hold a valid certificate of this certification. Use it for skills that need a licence, such as gas work. |
| **Enabled** | Untick to stop using a skill without deleting it. A disabled skill can't be given to anyone, and the people who hold it don't count as holding it. |

Deleting a skill takes it away from everyone who held it.

## Give a person their skills

Open the person under **Access Control > Users**, edit them, and find the **Skills** section. Choose a level for each skill they hold, from **1 - Trainee** to **5 - Expert**, or **Doesn't hold it**, and save. Only people who can assign skills see the section.

Contact Center agents' skills are also set on **Interaction Center > Management > Agent entitlements**. Both screens change the same skills.

## Add a certification

Open **Workforce > Certifications** and choose **Add certification**.

| Field | What it does |
| --- | --- |
| **Name** | The certification's name. Two certifications can't have the same name. |
| **Issued by** | Who issues it. |
| **Description** | What it covers. |
| **Valid for (months)** | How long a certificate lasts after it is issued. A certificate entered with only an issue date gets its expiry date from this. Leave it empty when the certification never expires. |
| **Reminders (days before expiry)** | The days before expiry that a reminder is raised, such as *60, 30, 7*. Each reminder is raised once for each certificate. Workflows can listen for it to email the person or their manager. |
| **Needs a copy of the certificate** | When ticked, a certificate can't be marked valid until a copy of it is attached. |
| **Enabled** | Untick to stop using a certification without deleting it. |

## Record a certificate

Open **Workforce > Certificates** and choose **Add certificate**.

| Field | What it does |
| --- | --- |
| **Held by** | The person who holds it. |
| **Certification** | Which certification it is. |
| **Certificate number** | The number printed on it. |
| **Issued on** | The day it was issued. |
| **Valid until** | The last day it is valid. Leave it empty to work it out from the issue date and the certification's **Valid for (months)**. |
| **Copy of the certificate** | The path of a copy in the media library, such as *certificates/john-smith-gas.pdf*. |
| **Status** | **Pending check** while it waits to be checked, **Valid** once it has been checked, **Expired** or **Withdrawn**. Only a valid certificate counts. |
| **Notes** | Anything else to keep with it. |

## Check, withdraw and find certificates

In **Workforce > Certificates**, each certificate's menu has **Mark as checked** (for one waiting to be checked), **Withdraw** (for a valid one that should no longer count) and **Delete**.

Use the filters above the list to show one certification, one status, or **Expiring in 60 days**, which lists the valid certificates that run out in the next 60 days, soonest first. Search finds a person's name or a certificate number.

## What happens when a certificate expires

Once a day the site checks every valid certificate:

- When a certificate reaches one of its certification's reminder days, a **Certificate Expiring** workflow event is raised, once for that reminder.
- The day after its **Valid until** date, the certificate's status changes to **Expired** and a **Certificate Expired** workflow event is raised. From then on, skills that require that certification stop counting for the person.

When the person renews it, edit the certificate with the new dates and the reminders start again for the new expiry date.

## Workflows

| Workflow activity | What it does |
| --- | --- |
| **Certificate Expiring** (event) | Starts when a certificate reaches a reminder day. It can be limited to some certifications. |
| **Certificate Expired** (event) | Starts when a certificate passes its expiry date. It can be limited to some certifications. |
| **Skills Changed** (event) | Starts when the skills a person holds change. |
| **Assign Skill** (task) | Gives a person a skill at a level, or takes it away when the level is 0. |
