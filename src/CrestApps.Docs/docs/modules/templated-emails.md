---
sidebar_label: Templated e-mails
title: Templated e-mails
description: "The shared templated e-mail sender in CrestApps.OrchardCore.Core: kinds of e-mail a site can write its own Liquid template of, grouped by the module that sends them, sources that keep those templates, the built e-mail as {{ Content }}, a layout per group, and sending in the recipient's culture."
---

A module that sends people e-mails of a kind (a ticket's *we got your request*, an order's confirmation) builds the e-mail in code and lets the site write over it in Liquid. `CrestApps.OrchardCore.Core` holds the shared pieces; Commerce sends its e-mails through them, and Tickets will too.

| Piece | Holds |
| --- | --- |
| `CrestApps.OrchardCore.Emails` (Abstractions) | `TemplatedEmail`, `TemplatedEmailContent`, `TemplatedEmailContext`, `TemplatedEmailMessage`, `TemplatedEmailTemplate`, `TemplatedEmailOverride`, `TemplatedEmailKind`, `TemplatedEmailOptions`, `TemplatedEmailCulture`, `ITemplatedEmailSender`, `ITemplatedEmailTemplates`, `ITemplatedEmailTemplateSource` and `AddTemplatedEmailKind` |
| `CrestApps.OrchardCore.Core.Emails` (Core) | `TemplatedEmailSender`, `LiquidTemplatedEmailTemplates`, `AddTemplatedEmails()` and `AddTemplatedEmailTemplateSource<T>()` |
| `CrestApps.OrchardCore.Core.Liquid` (Core) | `LiquidModelValues`: a record as the plain values Liquid reads |

## Sending

```csharp
await sender.SendAsync(
    new TemplatedEmail
    {
        Kind = "ticket-received",
        To = requester.Email,
        Models = new Dictionary<string, object> { ["Ticket"] = ticket },
        Culture = requester.Culture,
        Reference = ticket.Number,
    },
    async cancellationToken => new TemplatedEmailContent
    {
        Subject = S["We got your request"],
        Html = await RenderAsync(ticket, cancellationToken),
        Values = new Dictionary<string, string> { ["TicketUrl"] = url },
    });
```

The sender builds the content in the e-mail's culture, passes an e-mail with a kind through `ITemplatedEmailTemplates`, and sends it with Orchard Core's e-mail service when the Email feature is on. It returns whether the e-mail went; a send that fails is logged and never throws, so a mail server that's down never undoes what the e-mail is about. `CanSend` says whether the site can send e-mail at all.

## Kinds, groups and sources

A module lists the kinds of e-mail a site can write over, in a group named after it:

```csharp
services
    .AddTemplatedEmailKind("Tickets", "ticket-received", LocalizationSource.Create<Startup>("Request received"), LocalizationSource.Create<Startup>("{{ TicketUrl }}: the link to the request"))
    .AddTemplatedEmailTemplateSource<TicketEmailTemplateSource>();
```

The module keeps the site's templates where it likes, such as its settings, and returns them from its `ITemplatedEmailTemplateSource`: the kind's template when it's on, the group's layout and any values every template of the group reads. `LiquidTemplatedEmailTemplates` asks the sources in turn and renders what the first one that knows the kind gives; a kind no source knows goes as built.

In a template the built HTML is `{{ Content }}`, the subject `{{ Subject }}`, the site's name `{{ SiteName }}`, each model by its name with its properties as written (`{{ Ticket.Number }}`), and each value by its name. A template's body replaces the built HTML, and the text part follows it; a layout goes around the HTML. A template that fails to render gives the built e-mail.

## Commerce

`ICommerceEmailSender` and `ICommerceEmailTemplates` keep their signatures, so the store's call sites don't change. `CommerceEmailSender` sends through `TemplatedEmailSender`, and `LiquidCommerceEmailTemplates` renders the store's templates of `OrderEmailTemplateSettings` (**Settings › Commerce › Emails**) through `LiquidTemplatedEmailTemplates` with `CommerceEmailTemplateSource`, which adds `{{ StoreName }}`. The store's layout only wraps commerce e-mails.
