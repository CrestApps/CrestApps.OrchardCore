# Outreach

The one bulk-send engine for broadcasts and campaigns: a run sends to an audience in batches, skipping recipients who opted out or did not consent, holding text messages in quiet hours, and retrying failures. Messaging broadcasts and Marketing campaigns send through it.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Outreach | `CrestApps.OrchardCore.Outreach` | The outreach engine. It is enabled by the features that send through it. |

## Dependencies

None. Orchard's e-mail feature (`OrchardCore.Email`), when enabled, adds the e-mail channel sender.

## Contracts

- `CrestApps.OrchardCore.Outreach.Abstractions`: `IOutreachEngine`, `IOutreachAudienceSource`, `IOutreachChannelSender`, `IOutreachTemplateRenderer`, `IOutreachRecipientPolicy`, `IOutreachRunObserver` and the `OutreachRun` record.
- `CrestApps.OrchardCore.Outreach.Core`: the engine, `OutreachBatchProcessor`, the stores, `EmailOutreachChannelSender` and the `AddOutreach*` registration helpers.

## Documentation

See [Outreach](../../CrestApps.Docs/docs/modules/outreach.md) in the technical manual.
