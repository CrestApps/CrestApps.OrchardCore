---
sidebar_label: Ticket types and categories
title: Ticket types and categories
description: "Ticket types and the Ticket part's settings, the New ticket page and the Details card, categories from a taxonomy with the routing they give, and the questions that show only when they apply."
user_manual:
  - user-manual/service-desk/ticket-types-and-categories
  - user-manual/service-desk/open-a-ticket
---

## Ticket types

A ticket type is any content type with `TicketPart` (stereotype `Ticket`). The feature creates a `Ticket` type whose title is the **Subject**; add fields to it, or create more types, to ask for different things. Ticket types are not versioned or drafted: a save updates the ticket in place.

The Ticket part's settings on a ticket type (`TicketPartSettings`, edited on the type's Ticket part under **Content Definition**) say what a new ticket of the type starts with: the desk when none is chosen, the team (before the desk's default team), the priority, the service-level policy (used before the desk's) and, with Skills on, the skills an assignee must hold.

## The New ticket page and the Details card

**New ticket** is one ticket type's form (`Admin/tickets/new/{contentType}`), built by that type's editor; the type is chosen before it and can't change on it. Opened without a type (from an account, a messaging conversation or a link), it shows **What kind of ticket?** with the types a desk the agent may reply on takes, each with its **Description for customers**, and carries the account, requester and conversation to the form; with only one such type it opens that type's form at once.

The ticket page's **Details** card is the ticket type's editor (title and fields, show-when rules included) for a user who may edit the ticket; it posts to `POST Admin/ticket/{id}/edit`, and a save that fails shows the ticket page again with the posted values and the errors in the card. A user who may only view sees the fields' display, and **Not set** for each empty field that applies, from `IContentFieldStateService` (Conditional Fields).

## Categories

A ticket type can take its **categories** from an Orchard Core taxonomy (Hardware > Laptop > Battery): the Ticket part's settings name the taxonomy (`TicketPartSettings.CategoryTaxonomyId`), whether a category is required on the forms (`CategoryRequired`, agents' page, portal, web form; never for the API, workflows or e-mail) and whether only a category without sub-categories may be chosen (`CategoryLeavesOnly`). Several types may share a taxonomy. The Tickets feature defines a `TicketCategory` term type with the `TicketCategoryPart`; any taxonomy whose terms carry the part works.

`TicketCategoryPart` (on the term, edited in the taxonomy editor) gives a category's tickets a desk, team, priority and service-level policy, and says whether customers can choose it (`HiddenFromCustomers`) and what they read about it (`CustomerDescription`). A category takes each value it leaves empty from the nearest category above it, and a category hidden from customers hides those below it. `ITicketCategoryService` (`ListAsync`, `FindAsync`) reads a type's categories as `TicketCategory` records with their inherited values, `Path` (`/top/child/category/`) and `PathText` ("Hardware > Laptop").

A new ticket keeps `TicketPart.CategoryId` and `CategoryPath`. What the request says comes first, then the category, then the ticket type, then the desk; the service-level policy is the account's, then the category's, the type's and the desk's. A desk the request only suggests (`TicketCreateRequest.DeskIsSuggestion`, as the portal and the web form send it) gives way to the category's desk when that desk serves the same audience and takes the type, so a customer's choice never moves their request to an internal desk. The category's team is used only when the ticket lands on the category's desk (or the category names none).

A ticket keeps its category's path (`CategoryPath`, the ids from the top category down) for the list's "with sub-categories" filter and the reports. When a category moves under another parent, publishing the taxonomy asks for the resumable **Tickets.CategoryPaths** step (`TicketCategoryPathsStep`), which brings the paths of the tickets up to date a page at a time; renaming a category or changing its routing changes no path. Changing a ticket's category (`TicketUpdate.CategoryId`, an empty string clears it) changes only the category, recorded on the timeline with the change key `Category`. When the category's routing differs from the ticket's, the properties panel offers **Apply the category's routing** (`Admin/ticket/{id}/category-routing`), which moves the desk (a desk the agent may move tickets to), the priority and, with the assign permission, the team, through the same checks as a manual change. A workflow on Ticket Updated with the `Category` change key can do it instead.

## Questions that follow the answers

The Tickets feature depends on **Conditional Fields** (Content Fields module), so every field of a ticket type can have **Show this field when**, a rule on its other fields and on the category: Tickets offers the category to rules as the `TicketCategory` source (`IContentConditionSource`), with the categories above it, so "is or is under Hardware" covers its sub-categories. The forms put the chosen category on the new ticket before its editor is read, so the server applies the rules to what was chosen, and the picker posts `CategoryId`, which the rules' script reads in the browser. On a customer's form, the fields customers don't fill in are hidden fields too (`TicketCustomerFieldsVisibility`, an `IContentFieldVisibilityProvider`): what was posted for them is dropped and their validation is ignored, so a required internal field never refuses a customer's request.

The category picker is the Content Fields `TaxonomyCascadingPicker` shape: one list per level (Category, then Sub-category), with the category's description for customers under it.

| Where | What it does with categories |
| --- | --- |
| Ticket list | **Category** picker (a category and those below it, `TicketQuery.CategoryId` matching `TicketIndex.CategoryPath`), kept on the pager's links |
| Index | `TicketIndex.CategoryId` and `CategoryPath` (Tickets migration v12) |
| API | `GET tickets?categoryId=`, `POST tickets` and `PATCH tickets/{id}` take `categoryId`; `TicketApiModel` has `categoryId` and `categoryPath` (webhooks too) |
| Workflows | `CategoryId` and `CategoryPath` in `Workflow.Input`; **Update Ticket** sets the category; Ticket Updated's changed-field filter knows `Category` |
| Data sets | `Tickets`: `CategoryId`, `Category`, `CategoryPathText`, `CategoryLevel1`, `CategoryLevel2` |
| Recipes | Categories are a taxonomy content item (the `Content` step); the type's settings and the field rules travel with `ContentDefinition`. The **Help desk starter** recipe adds an **IT categories** taxonomy and an **IT request** type with questions that follow the category and an outage answer |

A term that is deleted while tickets use it leaves them with its id; they show "Removed category". A category moved inside the taxonomy keeps its tickets' old paths until they are saved again.
