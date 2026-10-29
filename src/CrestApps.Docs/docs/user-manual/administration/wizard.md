---
sidebar_label: Wizard
title: Guide Visitors Through a Wizard
description: Ask visitors for information one step at a time, and create content items from their answers.
technical_manual:
  - modules/wizard
---

A **wizard** shows a form one step at a time, with a progress bar, remembers the answers between steps, and creates content items from them at the end. Use it for event or workshop registrations, customer onboarding, surveys and any form that is too long to show at once.

| | |
| --- | --- |
| **Menu** | Design > Content Definition > Content Types (setup); Content > Content Items (building the steps) |
| **Permission** | Edit content types (setup); the usual permission to create the content item (building the steps); Start a wizard and Continue a wizard (for the people who take part) |
| **Feature** | Wizard |

<AskYourAdmin />

<YouTube video="site-wizard-recipes" title="Video overview of wizards, recipes and shared resources" />

## How a wizard is made

- Each **step** is an ordinary content type with its own boxes, for example *Visitor Details* with **Full name** and **Email**.
- A **wizard** is a content item of a type that has the **Wizard** part. It holds the steps as cards, in order.
- Visitors take part on the site's public wizard page, which opens on the first step. Ask your technical team for the link to put on a page or a button.

## Prepare a wizard type

<YouTube video="site-wizard-recipes" title="Video of adding the Wizard part to a content type" start={102} end={154} />

1. Create the content types for the steps, for example *Visitor Details* and *Visit Preferences*, with the boxes each step asks for.
2. Open **Design > Content Definition > Content Types** and edit the type that will host the wizard, for example *Workshop Registration*.
3. Click **Add Parts**, and click **Add & Configure** next to **Wizard**.
4. Set the options below and click **Save**.

| Field | What it does |
| --- | --- |
| **Contained content types** | The kinds of step this wizard can have. Choose **Content types** and tick them, or choose **Stereotype** to allow every content type with a stereotype you type. |
| **Display type** | How the steps are shown when the wizard item itself is displayed. The default is *Summary*. |
| **Collapse contained steps** | Shows the steps collapsed when you edit the wizard. |
| **Requires an authenticated user** | Visitors must be signed in to start. It is off for a new wizard. |
| **Completion policy** | What happens to the answers when the wizard is finished: **Create and publish the collected content items** (the default), **Create the collected content items as drafts**, or **Do not persist the collected content items**. |

## Build the steps

<YouTube video="site-wizard-recipes" title="Video of building the steps of a wizard" start={154} end={182} />

1. Open **Content > Content Items**, click **New** and choose the wizard type.
2. Enter the **Title**.
3. Click **Add step** and choose a step type. Repeat for each step. Each step card has its own boxes, which can start with a default value.
4. Drag a card by its handle to change the order. The order of the cards is the order of the steps.
5. Click **Publish**.

## What a visitor sees

<YouTube video="site-wizard-recipes" title="Video of a visitor taking part in a wizard" start={182} end={212} />

The wizard opens on the first step. A progress bar at the top shows every step and where the visitor is. **Next** saves the step and moves on, a finished step is marked with a tick, and **Previous** goes back without losing answers. On the last step the button reads **Finish**, and a confirmation tells the visitor the submission was completed.

## See what was collected

<YouTube video="site-wizard-recipes" title="Video of the content items a wizard creates" start={212} end={229} />

With the default **Completion policy**, each step's answers become a published content item of the step's type. Open **Content > Content Items** to find them, and edit or publish them like any other item.

:::note
The progress bar names each step after its content type. Give the step types names that make sense to visitors.
:::
