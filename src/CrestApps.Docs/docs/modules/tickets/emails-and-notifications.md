---
sidebar_label: E-mails and notifications
title: E-mails and notifications
description: "The messages requesters get and their templates, the outbox that retries them, the notification types agents get, and reply templates in the composer."
user_manual:
  - user-manual/service-desk/notifications
  - user-manual/service-desk/notification-templates
---

## Requester messages

With a desk that notifies requesters, the requester gets the `ticket-received`, `ticket-reply` (public replies only), `ticket-solved` and `ticket-rating` messages, [templated](../templated-emails) so the site can rewrite them under **Settings** → **Service Desk** (**Notification templates**). A requester who has a site account gets each through Orchard Core notifications, as the `TicketRequestReceived`, `TicketReplied`, `TicketRequestSolved` and `TicketRatingRequested` notification types (`TicketRequesterNotifier`, an `ITicketRequesterNotifier`): the message the template wrote is the notification's subject and body, it opens the request in their account, and they get it in their notification center and by the methods they choose, or turn a kind off. A requester without an account (a contact or a guest), or one whose account is gone, gets the same message by e-mail (the outbox e-mail's `Source` is the ticket). A template has `Ticket` (`Number`, `Subject`, `Status`, `RequesterName`, and `Category`, the category with those above it such as "Hardware > Laptop") and, in a reply, `Reply`: `Reply.Body` is the plain text, encoded where it is written, and `Reply.Html` is the reply already encoded as paragraphs with links to its files, written raw as `{{ Reply.Html | raw }}`. A reply's files are linked with signed download links that work for 30 days, from the site's **Base URL** (without one, the e-mail names the files and asks the requester to open the request).

Every requester e-mail ends with **View your request**; how that link is built, and the rating message, are described in [Portal and web form](./portal-and-web-form).

## The outbox

Every requester e-mail goes through the site's shared templated e-mail outbox (`ITemplatedEmailOutbox`): it is stored, tried at once, and when it fails tried again after 5 minutes, 15 minutes, an hour, 4 hours and 12 hours, then given up. When the site has no e-mail service, the reply is marked "not delivered" at once. While a reply's e-mail hasn't reached the requester, the reply on the ticket shows a "not delivered" notice with the reason (the timeline's `Notice` entry property), and the notice goes away once it is sent. An e-mail that is given up is also logged as a warning. A ticket created with `TicketCreateRequest.IsImport` (a bulk import of past tickets) tells no observer, so no requester is e-mailed, no workflow starts and no webhook is sent; tickets brought in by a recipe's `Content` step are saved as content items and tell no observer either.

## Notifications to agents

People are told through Orchard Core notifications (Tickets depends on Email Notifications, so the e-mail method is always there), each kind as its own notification type (`TicketsConstants.NotificationTypes`, registered with `AddNotificationType`, so users choose per type and method in their notification preferences): `TicketAssigned` (`TicketAssignmentNotifier`), `TicketTeamAssigned` (the team's members other than the assignee, when a ticket is created with a team or its team changes), `TicketRequesterReplied` (the assignee, or the team's members when nobody has the ticket), `TicketNoteAdded` (the assignee, unless the note mentions them), `TicketStatusChanged` (the assignee; not a new ticket's first status, nor the reopening by the requester's reply), `TicketServiceLevel` (the assignee or the team, once per metric and state while unread), `TicketMerged` (the assignee of the ticket merged into), `TicketWorkflow` (the Notify About Ticket task). `TicketApprovalRequested` comes with Approvals. `TicketNotificationSender` never tells the person who made the change (`TicketEventActor`), and the desk's **Tell the assignee** covers the assignment and team notices. Each notification carries the ticket as its `ReferenceId`, its admin page as its `Url` and `TicketNotificationInfo` (number, subject, desk), which `TicketNotificationDisplayDriver` shows in the navbar and the notification center; `TicketNotificationVisibilityHandler` hides it from a user who may no longer view the ticket (an approver keeps the approval notice, a requester the notices about their own request).

## Reply templates

With Omnichannel Messaging on, reply templates of the `Tickets` scope are offered in the composer, filled in for the ticket, and can also set a status, priority or team.
