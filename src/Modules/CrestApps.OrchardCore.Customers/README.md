# Customers

Keeps a record of everyone who buys from the store, with an account or as a guest, and lists them under Commerce > Customers.

## Features

| Feature | Id | Description |
| --- | --- | --- |
| Customers | `CrestApps.OrchardCore.Customers` | Keeps a record of everyone who buys from the store, with an account or as a guest, and lists them under Commerce > Customers. |
| Store Credit | `CrestApps.OrchardCore.Customers.StoreCredit` | Gives customers store credit, by hand or as a refund, that pays toward their orders when they're signed in. |
| Loyalty Points | `CrestApps.OrchardCore.Customers.Loyalty` | Gives signed-in customers points for what they buy, which they spend toward later orders, set up under Settings > Commerce > Rewards. |
| Referrals | `CrestApps.OrchardCore.Customers.Referrals` | Gives each signed-in customer a referral link: friends who open it can get a discount on their first order, and the customer is rewarded with store credit or points, set up under Settings > Commerce > Referrals. |
| Checkout details | `CrestApps.OrchardCore.Customers.CheckoutDetails` | Asks buyers for the store's own details at checkout, such as a gift message or a delivery note. Add the fields to the Checkout details content type; what the buyer enters is kept on the order. |
| Customers API | `CrestApps.OrchardCore.Customers.Api` | Lets a signed-in buyer read and change their account and addresses, and a checkout take their contact details, store credit and points, over the commerce JSON API; staff list and look up customers. Enable OpenAPI to document it. |

Enable a feature under **Configuration > Features**, or with the `Feature` step of a recipe.

## Dependencies

Enabling a feature of this module enables the features it depends on:

- [`CrestApps.OrchardCore.Commerce`](../CrestApps.OrchardCore.Commerce/README.md)
- `OrchardCore.Users`
- [`CrestApps.OrchardCore.Addresses`](../CrestApps.OrchardCore.Addresses/README.md)
- [`CrestApps.OrchardCore.Orders`](../CrestApps.OrchardCore.Orders/README.md)
- `OrchardCore.Contents`
- `OrchardCore.ContentTypes`

## Extension points

- `ICustomerContact` (CrestApps.OrchardCore.Customers.Abstractions): A provider-neutral contact for a buyer.
- `ICommercePersonalDataContributor` (CrestApps.OrchardCore.Customers.Abstractions): The part one commerce module plays in exporting and anonymizing a person's data.
- `ICommercePersonalDataService` (CrestApps.OrchardCore.Customers.Abstractions): Exports and anonymizes the personal data the commerce modules keep about one person, for access and erasure requests.
- `ICustomerContactResolver` (CrestApps.OrchardCore.Customers.Abstractions): Resolves a `CustomerOwner` into an `ICustomerContact` so a consumer can address the buyer for statements and notifications without depending on the authenticated user service.
- `ICustomerOwnerNotifier` (CrestApps.OrchardCore.Customers.Abstractions): Sends a buyer a message the same way from every commerce feature.
- and 8 more contracts in the same projects.

## Documentation

See [Customers](../../CrestApps.Docs/docs/modules/customers.md) in the technical manual.
