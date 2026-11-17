# Plaid

Instant bank account verification with Plaid for the Banking building block: a person signs in to their bank in Plaid Link, picks the account, and the bank account is saved with the bank's own routing and account numbers and verified at once.

## Features

| Feature ID | Name | Depends on |
| --- | --- | --- |
| `CrestApps.OrchardCore.Plaid` | Plaid Bank Account Linking | Banking, Secrets, Provider Webhook Inbox |

- Banking's first `IBankAccountLinkProvider`: **Link your bank with Plaid** on **My Bank Accounts** (add, or replace the numbers of one's own account) and **Link with Plaid** on the admin account page.
- Plaid Auth over plain HTTP (`IHttpClientFactory`, the standard resilience handler, retries only for calls safe to repeat). No new package.
- **Configuration > Settings > Plaid** (`ManagePlaidSettings`): environment, client id and the name of the Orchard secret that holds the Plaid secret. The secret is never in the settings.
- The item is removed as soon as the numbers are read. For an account number the bank made for Plaid (tokenized), the item is kept with its access token protected, and the account goes back to unverified when the person takes back Plaid's access.
- `POST /plaid/webhook`: the ES256 signature, age and body hash are checked, then the webhook is stored in the provider webhook inbox.

## Installation

Reference the module (it is in `CrestApps.OrchardCore.Cms.Core.Targets`), enable **Plaid Bank Account Linking** in **Tools > Features**, add a text secret holding the Plaid secret under **Security > Secrets**, and fill in **Configuration > Settings > Plaid**.

See the Plaid page of the Technical Manual (`src/CrestApps.Docs/docs/modules/plaid.md`) and the Bank accounts page of the User Manual.
