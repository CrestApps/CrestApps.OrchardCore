---
sidebar_label: Templated e-mails
title: Templated e-mails
description: "The shared templated e-mail sender in CrestApps.OrchardCore.Core: kinds of e-mail a site can write its own Liquid template of, grouped by the module that sends them, sources that keep those templates, the built e-mail as {{ Content }}, a layout per group, and sending in the recipient's culture."
---

A module that sends people e-mails of a kind (an inspection's *booked*, an order's confirmation) builds the e-mail in code and lets the site write over it in Liquid. `CrestApps.OrchardCore.Core` holds the shared pieces; Commerce sends its e-mails through them.

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
        Kind = "inspection-booked",
        To = requester.Email,
        Models = new Dictionary<string, object> { ["Inspection"] = inspection },
        Culture = requester.Culture,
        Reference = inspection.Number,
    },
    async cancellationToken => new TemplatedEmailContent
    {
        Subject = S["We got your request"],
        Html = await RenderAsync(inspection, cancellationToken),
        Values = new Dictionary<string, string> { ["InspectionUrl"] = url },
    });
```

The sender builds the content in the e-mail's culture, passes an e-mail with a kind through `ITemplatedEmailTemplates`, and sends it with Orchard Core's e-mail service when the Email feature is on. It returns whether the e-mail went; a send that fails is logged and never throws, so a mail server that's down never undoes what the e-mail is about. `CanSend` says whether the site can send e-mail at all.

## Kinds, groups and sources

A module lists the kinds of e-mail a site can write over, in a group named after it:

```csharp
services
    .AddTemplatedEmailKind("Inspections", "inspection-booked", LocalizationSource.Create<Startup>("Inspection booked"), LocalizationSource.Create<Startup>("{{ InspectionUrl }}: the link to the inspection"))
    .AddTemplatedEmailTemplateSource<InspectionEmailTemplateSource>();
```

The module keeps the site's templates where it likes, such as its settings, and returns them from its `ITemplatedEmailTemplateSource`: the kind's template when it's on, the group's layout and any values every template of the group reads. `LiquidTemplatedEmailTemplates` asks the sources in turn and renders what the first one that knows the kind gives; a kind no source knows goes as built.

In a template the built HTML is `{{ Content }}`, the subject `{{ Subject }}`, the site's name `{{ SiteName }}`, each model by its name with its properties as written (`{{ Inspection.Number }}`), and each value by its name. A template's body replaces the built HTML, and the text part follows it; a layout goes around the HTML. Templates render with restricted Liquid: they read only these values, never Orchard's request, user or site objects or its own filters, and need no Liquid feature. A template that doesn't parse or fails to render gives the built e-mail.

## Commerce

`ICommerceEmailSender` and `ICommerceEmailTemplates` keep their signatures, so the store's call sites don't change. `CommerceEmailSender` sends through `TemplatedEmailSender`, and `LiquidCommerceEmailTemplates` renders the store's templates of `OrderEmailTemplateSettings` (**Settings › Commerce › Emails**) through `LiquidTemplatedEmailTemplates` with `CommerceEmailTemplateSource`, which adds `{{ StoreName }}`. The store's layout only wraps commerce e-mails.
