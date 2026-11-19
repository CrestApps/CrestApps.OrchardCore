---
sidebar_label: Fulfillment Partners
title: Fulfillment Partners
description: The engine that connects a store to the outside companies that make and ship its products, such as print-on-demand services. A partner plugs in through IFulfillmentProvider; the engine assigns each line to one partner, reserves it in a Shipping shipment, sends paid orders once, and leaves unclear outcomes to a person.
user_manual:
  - user-manual/commerce/fulfillment-partners
---

| | |
| --- | --- |
| **Feature** | `CrestApps.OrchardCore.Fulfillment` (Fulfillment Partners) |
| **Projects** | `CrestApps.OrchardCore.Fulfillment.Abstractions`, `CrestApps.OrchardCore.Fulfillment.Core`, `CrestApps.OrchardCore.Fulfillment` |
| **Dependencies** | Commerce, Orders, Shipping |
| **Menu** | Commerce > Fulfillment requests; Settings > Commerce > Fulfillment partners |
| **Permissions** | `ManageFulfillmentPartners` for the settings page and credentials (security critical; the Administrator role has it). Shipping's `FulfillOrders` for the order card, sending and the Fulfillment requests list. |

A **fulfillment partner** is an outside company that makes and ships a store's products for it, such as a print-on-demand service. This module is the engine: the settings page partners are connected on, line assignment, the order card, the requests list, notifications and the sender. It ships no partner. Each company is a provider module that implements `IFulfillmentProvider`, so a site adds the partners it uses, from this repository or from its own solution.

## Scope today

The engine connects partners, sends paid orders to them and records what each one answered. Tracking numbers, cancellations and refunds do not come back from partners yet: a person types the tracking number on the reserved shipment. `IFulfillmentProvider` has only the members this needs, and it grows when those capabilities are added.

## Built on Shipping

What a partner is sent is a Shipping shipment, so the order page, the order's fulfillment status, the buyer's emails and the reports all see it the same way as a parcel the store packs itself.

- Before the partner is called, the engine creates a **pending** shipment with the partner's lines (`IShipmentService.CreateAsync(..., ship: false)`, noted "Sent to *partner*"). The shipment and the request's **Sending** status are saved in one commit, so the units are reserved before anything leaves.
- Only what is left is sent: the quantities come from `IShipmentService.GetRemainingAsync`, and only physical lines (`ShippingOrderRateService.IsPhysical`) count. If an order had three units and one was shipped by hand, the partner gets two.
- When the partner accepts, the shipment stays pending and its note gets the partner's order id. Typing the carrier and tracking number on it (`ShipAsync`) marks it shipped and updates the order's fulfillment status.
- When the partner refuses, or reports nothing to send, the shipment is deleted and the units are free again. When the outcome is unclear, the shipment stays, so nobody ships the units by hand by mistake, and **Send again** reuses it instead of reserving more.
- If a reserved shipment was shipped by hand meanwhile, sending again sends nothing.

## Assigning lines

The engine decides which partner makes which line, so two partners never make the same unit.

1. Every connected partner is asked which of the remaining lines it makes (`ClaimLinesAsync`). A partner that fails to answer is skipped for the other partners; for the partner being sent, it is **Needs attention** and nothing is reserved.
2. A claim larger than what is left is capped. A claim for a line that is not left is ignored.
3. If two partners claim the same line, the request is **Needs attention**, naming the line and the partners, and neither partner is called.
4. A partner that claims nothing has **Nothing to send** and is not called.
5. `SubmitAsync` receives only the lines assigned to that partner, and the request stores them, so the order card and the list show each partner's lines.

## Sending orders

`Fulfillment.Core` keeps one `FulfillmentRequest` per order and partner in the `Fulfillment` collection, indexed by order, order number, partner, shipment, status, due time and last change.

- **Checks before sending.** The order must be paid, not refunded in full, without an open dispute and not anonymized. It needs a shipping address and must not be a pickup. The store ships bundles. The engine excludes bundle lines before asking partners to claim the other lines. If only bundles remain, the request is **Nothing to send** and no one is notified.
- **One sender per order.** Recording, assigning, reserving and calling a partner happen under a distributed lock on the order, held for longer than the partner call may take. After the lock is taken, the request is read again from the database (`FulfillmentRequestStore.FindCommittedAsync`), so a request another worker sent or settled is left alone. The request store checks concurrency too; when another worker wins, the caller gets `FulfillmentSendResult.TakenByAnotherWorker` instead of an error.
- **Bounded partner call.** `SubmitAsync` is cancelled after `FulfillmentConstants.SubmitTimeout` (2 minutes). A timeout, an exception or any unclear answer is **Needs attention** with the shipment kept, and is never sent again by itself.
- **Results.** `FulfillmentSubmitResult` is **Sent** (with the partner's order id), **Nothing to send**, **Retry later** (the partner certainly did not create the order; retried with a growing wait, at most 10 times, keeping the shipment), **Refused** (the partner certainly did not create it; the shipment is released) or **Needs attention**.
- **Interrupted sends.** A request left in **Sending** for 15 minutes (the site stopped mid-call) is marked **Needs attention**; the check takes the order's lock and reads the request again first.
- **Reasons.** A request stores a reason code (`FulfillmentReasons`) and its values, never a sentence. The module turns them into localized text (`FulfillmentRequestText`) on the card, the list, the notices and the notifications. The partner's own message is kept as it came.
- **Manual sending.** The **Fulfillment partners** card on an order's admin page (`DetailAdmin`) shows each partner's request, its reason and its lines whenever the order has requests, read-only once the order is no longer paid. On a paid order it has a button that records and sends the order, under the order's lock, so two people sending at once share one request per partner. Sending again when the partner may already have the order asks for confirmation through the admin theme's confirmation dialog (`data-url-af="RemoveUrl"`).
- **Automatic sending.** With `FulfillmentSettings.SendAutomatically` on, `FulfillmentOrderEventHandler.PaidAsync` records a pending request for each connected partner in the same unit of work as the payment. It reads saved settings only and calls no partner. Its own failures are logged and swallowed; a database or concurrency failure propagates, so the checkout rolls back and retries. The **Send orders to fulfillment partners** background task sends due requests every minute.

## Fulfillment requests list

**Commerce > Fulfillment requests** lists every request, most recently changed first, on the standard admin list (`AdminListContext<FulfillmentRequest>`, rows from `FulfillmentRequestDisplayDriver` in the `SummaryAdmin` and `SummaryAdminGrid` display types). It searches by order number, filters by status in the list toolbar, links each row to its order and shows the partner, the status, the reason, the partner's message, the lines and the last change. A row that is not sent has **Send again**, which uses the same sender and the same confirmation.

## Notifications

With the Notifications feature on, a request that becomes **Needs attention** notifies each person who may ship orders (the Administrator role and every role with `FulfillOrders`) once, in the notification center, with a link to the order. The type is `FulfillmentConstants.NeedsAttentionNotificationType` (`FulfillmentNeedsAttention`), which people can turn off in their notification preferences. Other modules can react too by registering an `IFulfillmentRequestHandler`; the sender calls `NeedsAttentionAsync` once per transition, after the request is saved.

## Settings, recipes and deployments

`FulfillmentSettings` is set with Orchard Core's `Settings` recipe step, described to CrestApps Recipes by a `SiteSettingsSchemaBase`, and exported by the **Fulfillment settings** deployment step. Requests are operational records, like orders and shipments, so they have no recipe or deployment step.

## Add a provider

A provider is a module that depends on the `CrestApps.OrchardCore.Fulfillment` feature (`FulfillmentConstants.Features.Fulfillment`) and references the `CrestApps.OrchardCore.Fulfillment.Abstractions` package. It can live in any solution.

1. Implement `IFulfillmentProvider`:
   - `Name` is the technical name stored requests refer to; `DisplayName` is shown to people.
   - `IsConnectedAsync` reads saved settings only. It never calls the partner, because the settings page, the order card and the paid-order handler call it.
   - `TestConnectionAsync` calls the partner with the saved credentials. A refusal or an outage is a failed `FulfillmentConnectionResult`, not an exception.
   - `ClaimLinesAsync` returns a `FulfillmentLineClaim` (line id and quantity) for each line of the `FulfillmentOrder` the partner makes, typically from a part the provider keeps on the product, read from the published product. It must not call the partner to create anything.
   - `SubmitAsync` sends the `FulfillmentOrder` it is given, which holds only the lines assigned to this partner, and honors the cancellation token. Return **Retry later** or **Refused** only when the partner certainly did not create the order, and send the order's `OrderId` as the partner's external reference when the partner supports one, so a retry can't make it twice.
   - The order's `ShippingAddress` is the Addresses `Address` the order stores, with the order's `Email` and `Phone`. `FulfillmentNames.Split` splits a full name into a first word and the rest; a single word has no last name, and the provider decides what its partner needs instead.
2. Register it as a scoped service: `services.AddScoped<IFulfillmentProvider>(sp => sp.GetRequiredService<MyProvider>())`.
3. Add a `SiteDisplayDriver<TSettings>` on `FulfillmentConstants.SettingsGroupId` that checks `FulfillmentPermissions.ManageFulfillmentPartners`, and call `services.AddSiteSettingsPermission(FulfillmentConstants.SettingsGroupId, FulfillmentPermissions.ManageFulfillmentPartners)` in the same startup.
4. Keep secrets with `ProtectedSettingExtensions` (`ApplySecret`, `HasSecret`, `UnprotectSettingOrDefault`), and describe the settings to CrestApps Recipes with a `SiteSettingsSchemaBase`.

Credentials are protected site settings, as the map, carrier and payment providers keep theirs, so the owner connects a partner on one page. A [data connection](data-connectors) is shaped for reading data sets and has no step for choosing an account, so it is not used here.
