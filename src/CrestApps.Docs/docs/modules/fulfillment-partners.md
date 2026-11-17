---
sidebar_label: Fulfillment Partners
title: Fulfillment Partners
description: The engine that connects a store to the outside companies that make and ship its products, such as print-on-demand services. A partner plugs in through IFulfillmentProvider; the engine sends paid orders once, from the order page or automatically, and leaves unclear outcomes to a person.
user_manual:
  - user-manual/commerce/fulfillment-partners
---

| | |
| --- | --- |
| **Feature** | `CrestApps.OrchardCore.Fulfillment` (Fulfillment Partners) |
| **Projects** | `CrestApps.OrchardCore.Fulfillment.Abstractions`, `CrestApps.OrchardCore.Fulfillment.Core`, `CrestApps.OrchardCore.Fulfillment` |
| **Menu** | Settings > Commerce > Fulfillment partners |
| **Permission** | `ManageFulfillmentPartners` (security critical; the Administrator role has it) |

A **fulfillment partner** is an outside company that makes and ships a store's products for it, such as a print-on-demand service. This module is the engine: the settings page partners are connected on, the order card and the sender. It ships no partner. Each company is a provider module that implements `IFulfillmentProvider`, so a site adds the partners it uses, from this repository or from its own solution.

## Scope today

The engine connects partners, sends paid orders to them and records what each one answered. Tracking numbers, cancellations and refunds do not come back from partners yet. `IFulfillmentProvider` has only the members this needs, and it grows when those capabilities are added.

## The settings page

`FulfillmentConstants.SettingsGroupId` (`fulfillment-partners`) is the site settings group. The module registers the group's permission, its menu item, `FulfillmentPartnersDisplayDriver` (every registered `IFulfillmentProvider` with a **Connected** or **Not connected** badge from `IsConnectedAsync`) and the **Send paid orders automatically** switch (`FulfillmentSettings.SendAutomatically`, off by default). A provider module adds its own section to the same group with a `SiteDisplayDriver<TSettings>`.

## Sending orders

`Fulfillment.Core` turns a store order into a neutral `FulfillmentOrder` (`FulfillmentOrderFactory`), so a provider never depends on the order model. It keeps one `FulfillmentRequest` per order and partner in the `Fulfillment` collection, indexed by order, partner, status and due time.

- **Checks before sending.** The order must be paid, not refunded in full, without an open dispute and not anonymized. It needs a shipping address and must not be a pickup. Only lines of `ProductType.Good` are sent; a bundle line goes to a person.
- **Sending once.** `FulfillmentSender` marks the request **Sending** and commits it before it calls the provider, and the request store checks concurrency, so two workers can't both send it. A request left in **Sending** for 15 minutes (the site stopped mid-call) is marked **Needs attention**.
- **Results.** `FulfillmentSubmitResult` is **Sent** (with the partner's order id), **Nothing to send**, **Retry later** (the partner certainly did not create the order; retried with a growing wait, at most 10 times) or **Needs attention**. A timeout, a server error or any other unclear answer is **Needs attention** and is never sent again by itself.
- **Manual sending.** The **Fulfillment partners** card on a paid order's admin page (`DetailAdmin`, shown only to people with `ManageFulfillmentPartners`, since the same display type is a customer's own order) lists each partner's request and has a button that records and sends the order. Sending again after **Needs attention** asks for confirmation.
- **Automatic sending.** With `FulfillmentSettings.SendAutomatically` on, `FulfillmentOrderEventHandler.PaidAsync` records a pending request for each connected partner in the same unit of work as the payment. It reads saved settings only, calls no partner and never throws, because a throw there would undo the payment. The **Send orders to fulfillment partners** background task sends due requests every minute.

Requests are operational records, like orders, so they have no recipe or deployment step.

## Add a provider

A provider is a module that depends on the `CrestApps.OrchardCore.Fulfillment` feature (`FulfillmentConstants.Features.Fulfillment`) and references the `CrestApps.OrchardCore.Fulfillment.Abstractions` package. It can live in any solution.

1. Implement `IFulfillmentProvider`:
   - `Name` is the technical name stored requests refer to; `DisplayName` is shown to people.
   - `IsConnectedAsync` reads saved settings only. It never calls the partner, because the settings page, the order card and the paid-order handler call it.
   - `TestConnectionAsync` calls the partner with the saved credentials. A refusal or an outage is a failed `FulfillmentConnectionResult`, not an exception.
   - `SubmitAsync` sends the lines of a `FulfillmentOrder` the partner makes and returns a `FulfillmentSubmitResult`. The provider works out which lines are its own, typically from a part it keeps on the product beside the product part, read from the published product. Return **Retry later** only when the partner certainly did not create the order, and send the order's `OrderId` as the partner's external reference when the partner supports one.
2. Register it as a scoped service: `services.AddScoped<IFulfillmentProvider>(sp => sp.GetRequiredService<MyProvider>())`.
3. Add a `SiteDisplayDriver<TSettings>` on `FulfillmentConstants.SettingsGroupId` that checks `FulfillmentPermissions.ManageFulfillmentPartners`, and call `services.AddSiteSettingsPermission(FulfillmentConstants.SettingsGroupId, FulfillmentPermissions.ManageFulfillmentPartners)` in the same startup.
4. Keep secrets with `ProtectedSettingExtensions` (`ApplySecret`, `HasSecret`, `UnprotectSettingOrDefault`), and describe the settings to CrestApps Recipes with a `SiteSettingsSchemaBase`.

Credentials are protected site settings, as the map, carrier and payment providers keep theirs, so the owner connects a partner on one page. A [data connection](data-connectors) is shaped for reading data sets and has no step for choosing an account, so it is not used here.
