---
sidebar_label: Storage
title: Storage
description: "Where Tickets keeps its data: tickets and their indexes in the default collection, and the service desk's own records in the ServiceDesk collection."
---

Tickets are content items, so they, `TicketIndex` and `TicketLinkIndex` stay in the default collection. The service desk's own records live in its YesSql collection, `ServiceDesk` (`TicketsConstants.CollectionName`, registered with `StoreCollectionOptions`): the desks, statuses, priorities, saved views and boards, one document per record with its index (`TicketDeskIndex`, `TicketStatusIndex`, `TicketPriorityIndex`, `TicketViewIndex`, `TicketBoardIndex`, through `TicketSettingsStore<T, TIndex>`, the `ICatalog<T>` of each), and the protected information, the requester e-mail outbox, the daily backlog snapshots and the time-elapsed markers. Code that queries those documents passes the collection. The migration to version 17 moves a site's existing records there: the documents from the default collection, and the settings records from the one dictionary document per kind they were kept in, keeping their ids; it then drops the old index tables. Timeline entries, service-level timers and attachments stay in the Timeline, Service Levels and Attachments collections.
