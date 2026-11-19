---
sidebar_label: Ticket Types and Categories
title: Ticket Types, Fields and Categories
description: "Make kinds of tickets that ask for different things, give their fields show-when rules, choose what customers fill in, and sort tickets into categories that route them."
technical_manual:
  - modules/tickets/ticket-types-and-categories
---

A **ticket type** is a kind of ticket, such as a general **Ticket** or an **IT request**. Each type asks for its own things: its subject and its fields, such as the laptop model. **Categories** say what a ticket is about, such as Hardware > Laptop, and can send it to the right desk and team.

| | |
| --- | --- |
| **Menu** | Design > Content Definition > Content Types; Content > Taxonomies for categories |
| **Permission** | Edit content types; manage the taxonomy's content items |
| **Feature** | Tickets, and Taxonomies for categories |

<AskYourAdmin />

## Make a ticket type

The Tickets feature makes a **Ticket** type whose title is the **Subject**. Add fields to it, or make more types to ask for different things:

1. Go to **Content Definition > Content Types** and select **Create new type**.
2. Give it a name, such as *IT request*, and add the **Ticket** part.
3. Add the fields it should ask for, such as a **Text** field for the laptop model. Give a text field the **TextArea** editor for answers longer than a line.
4. Save. Agents now find it under **New ticket**.

Tickets aren't drafted or versioned: a save changes the ticket in place.

## The Ticket part's settings

Edit the type and open its **Ticket** part to say what a new ticket of the type starts with and who may send it:

| Setting | What it does |
| --- | --- |
| **Desk** | The desk a new ticket goes to when none is chosen |
| **Team** | The team, before the desk's default team |
| **Priority** | The priority a new ticket starts with |
| **Service-level policy** | Used before the desk's policy |
| **Required skills** | With Skills on, the skills an assignee must hold |
| **Categories** | The taxonomy its categories come from (see below), with **A category is required** and **Only the most specific category** |
| **Customers can submit it** | With Tickets Portal on, customers see this type under **New request** in their account, on the desks that serve customers |
| **Description for customers** | What customers read about the type, so they can tell types apart |
| **Approval** | With Ticket Approvals on, the approval steps. See [Approvals](./approvals.md) |

## Choose what customers fill in

Customers see the subject and only the fields you mark **Customers fill this in**, in each field's settings on the ticket type. Leave it off for fields only your agents set, such as a root cause; customers then never see or change it. See [Tickets Portal](./portal.md).

## Ask the questions that apply

A ticket type's fields can show only when they apply: "Laptop model" only for laptop problems, "Who else is affected?" only when someone ticks "Is this an outage?". Edit the field in the ticket type and fill in **Show this field when**, with a condition on the category (**is or is under** covers its sub-categories) or on another field. See [Show a field only when it applies](../administration/content-fields#show-a-field-only-when-it-applies).

The questions appear as people choose the category and answer, on the agents' **New ticket** page, the ticket's **Details** card, in the portal and on the web form. When the ticket is sent, the site checks the conditions again and keeps nothing a hidden question received.

## Sort tickets into categories

Categories come from a taxonomy, so you edit them like any other list of terms. People choose one when they open a ticket, one level at a time, and the category can send the ticket to the right desk and team with the right priority and service level.

1. Turn on **Taxonomies**, open **Content > Content Items**, choose **New** and **Taxonomy**, and create one, such as *IT categories*, with **Ticket category** as its term type. Add its categories, and sub-categories under them.
2. Edit a category to set what its new tickets start with: **Desk**, **Team**, **Priority** and **Service-level policy**. Leave a value empty to take it from the category above. Tick **Only agents can choose it** for categories customers shouldn't see (and those under it), and write a **Description for customers** to tell them what to have ready. Changes to categories take effect once you **Publish** the taxonomy.
3. Open **Content Definition > Content Types**, edit the ticket type and, on its **Ticket** part, choose the taxonomy under **Categories**. Tick **A category is required** to make people choose one, and **Only the most specific category** so they pick Hardware > Laptop rather than Hardware.

What a ticket starts with comes first from what the agent chose on the form, then from its category, then from the ticket type. A customer's request moves to the category's desk only when that desk serves customers too.

When an agent changes a ticket's category later, only the category changes. If the new category sends tickets elsewhere, the ticket's panel shows **Apply the category's routing**, with what it would change; click it to move the ticket.

The ticket list can show only the tickets of a category and the categories under it. A category that is deleted while tickets use it shows as "Removed category" on them.

The [help desk starter recipe](./help-desk-starter.md) sets up an example: an **IT categories** taxonomy and an **IT request** type that asks for the laptop model for laptop problems, which system for access requests, and who else is affected for an outage.
