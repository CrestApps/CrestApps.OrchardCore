# CrestApps OrchardCore Subscriptions

The Subscriptions module sells anything on a recurring cycle: a plan, a service, member-only access to the
site, or a whole Orchard Core tenant. It owns what happens *after* the first payment, which is where most of
the work in a subscription business actually is.

## Why this module exists

A subscription is easy to sell and hard to keep honest. The questions that matter are all about time:

- Who is subscribed **right now**, as opposed to who once paid?
- Whose renewal failed last night, and how long do they keep access before it is fair to cut them off?
- What happens to somebody who cancels halfway through a month they have already paid for?
- When the gateway and the site disagree, which one is right?

Answering those from a completed checkout record does not work, so the module keeps a durable **subscription
agreement** that outlives the checkout that created it, and one service that owns every change to it.

## Features

- **Subscriptions** (`CrestApps.OrchardCore.Subscriptions`) — plans, subscription agreements, the lifecycle
  service, entitlements, the customer portal, and the admin screens.
- **Subscriptions - Sites** (`...Tenants`) — sells whole Orchard Core sites, provisioning each from a durable
  job that survives a restart and retries on failure.

This module owns no checkout of its own. Buying a plan runs through the **Checkout** module like any other
purchase, so there is one path that takes money and one ledger that records it.

## The subscription agreement

Every recurring obligation a checkout settles creates an agreement recording what it bills, on what cycle,
through which gateway, the period the customer has paid through, when the next cycle is due, what it grants,
and a full event history.

It is created only from payment attempts the checkout actually confirmed, and it is looked up by obligation
before being created, so an unpaid checkout never grants a subscription and a checkout that completes twice
never produces two.

## One place state changes

A gateway webhook, a nightly sweep, an administrator, and the customer all edit subscription state, and they
race. Every transition therefore goes through `ISubscriptionLifecycleService`, which locks the single
agreement it changes, re-reads it inside the lock, and is idempotent:

- reporting the same renewal twice advances it once, so a webhook and the sweep cannot skip a payment;
- a second payment failure inside the same dunning window does not restart the grace clock;
- a gateway status arriving after a local cancellation cannot revive it and start billing somebody who left.

## Access is not status

Whether a subscriber has access is a separate question from the agreement's status, and asking the status
gets it wrong for exactly the customers you least want to lose. A cancelled agreement is still owed until
the paid period runs out; a past-due one is owed through its grace window. Ask `ISubscriptionAccessService`.

## Entitlements

Attach the **Subscription Entitlements** part to a plan and pick the roles a subscriber holds while their
subscription is current. Roles already gate content, features, and permissions across Orchard Core, so this
makes all of those subscription-aware without any of them knowing subscriptions exist. Implement
`ISubscriptionEntitlementApplier` to grant something other than a role.

## Reacting to what happens

The reactions a site owner wants — welcoming a subscriber, warning one whose card was declined, asking a
leaver why — differ from site to site, so the module raises workflow events rather than hard-coding any of
them: started, renewed, past due, canceled, and expired.

## Documentation

Full documentation: <https://docs.crestapps.com/docs/modules/subscriptions>

## License

This project is licensed under the MIT License.
