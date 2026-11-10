---
sidebar_label: Agentic Commerce
title: Agentic Commerce
description: Sell through AI agents with the open agentic commerce protocols, and publish the products in the feeds agent platforms read.
user_manual:
  - user-manual/commerce/product-listings
---

## Agent product feeds

The **Agent Product Feeds** feature (`CrestApps.OrchardCore.AgenticCommerce.Feeds`, which turns on Agentic Commerce and Product Feeds) publishes the store's products for AI agents, from the same items as the [product feed](products.md#product-feeds):

- `feeds/openai/products.jsonl.gz` and `feeds/openai/products.csv.gz` (or `.jsonl` and `.csv` uncompressed) in OpenAI's product feed format for ChatGPT, with `?currency=` and the product feed's `?key=`.
- A daily push of the full OpenAI feed over SFTP.
- An Agentic Commerce Protocol (ACP) feed API client that keeps agent platforms that host one up to date.

**Settings > Commerce > Agent feeds** (permission `ManageAgentFeeds`, settings group `agent-feeds`) shows the feed addresses, how many items are in the feed and why the others were left out, the SFTP push and the feed APIs, the result of the last pushes, and a **Push now** button. See [Product listings](../user-manual/commerce/product-listings.md#feeds-for-ai-agents) for the screen.

### The OpenAI feed

`OpenAIProductFeedService` turns the store's product feed into `OpenAIProductFeedRecord`s with `OpenAIProductFeedWriter.ToRecord` and keeps them as long as the product feed it comes from, so a stock change rebuilds both. The fields:

| Field | From the feed item |
| --- | --- |
| `item_id`, `group_id`, `listing_has_variations`, `variant_dict` | The item id (`ProductFeedItemIds`), the product's id for a variant, and its options. |
| `title`, `description`, `url`, `image_url`, `additional_image_urls` | As in the product feed. |
| `brand`, `gtin`, `mpn`, `condition`, `product_category`, `color`, `size` | As in the product feed; `product_category` is the taxonomy path, else a Google category given by name. |
| `availability` | `in_stock`, `out_of_stock`, `pre_order`, `backorder`, or `unknown`. |
| `price`, `sale_price`, `shipping_price` | Money as `79.99 USD`, at the currency's precision (`CurrencyScale`). |
| `weight`, `item_weight_unit`, `length`, `width`, `height`, `dimensions_unit` | The Shipping contributor's values. |
| `accepts_returns`, `return_deadline_in_days`, `return_policy`, `accepts_exchanges` | The Returns contributor's values and the store profile's return policy link. |
| `seller_name`, `seller_url`, `seller_privacy_policy`, `seller_tos` | The store profile. |
| `is_digital` | The product type is Digital. |
| `is_eligible_search`, `is_eligible_checkout` | **Show to AI agents** and **Let AI agents check out**. An item agents may not show stays in the file with `is_eligible_search` set to `false`, which removes it from ChatGPT sooner than leaving it out. |
| `store_country`, `target_countries` | The store profile's country, and `AgentFeedSettings.TargetCountries` (the store's country when empty). |

A field without a value is left out of a JSON line and empty in a CSV cell. In CSV, booleans are `true` or `false`, lists are joined by commas and `variant_dict` is a JSON object; cells are escaped with `CsvWriter`.

`OpenAIProductFeedValidator` leaves out an item that breaks one of OpenAI's rules, and the settings page lists why: a missing required field (`item_id`, `title`, `description`, `url`, `brand`, `seller_name`, `image_url`, `availability`, `price`), an availability OpenAI doesn't know, money not written as an amount and an uppercase code, a sale price not below the price in the same currency, a GTIN without its check digit, a variant without a `group_id` other than its `item_id` or without `variant_dict`, a weight or size without its unit, and an item agents may check out without `seller_privacy_policy` and `seller_tos`. Items the product feed already left out are listed too.

### The SFTP push

When **Push the full feed over SFTP every day** is on, `AgentFeedPushBackgroundTask` (daily at 02:30) has `AgentFeedSftpPusher` write the whole catalog to the SFTP folder in shards of at most 500,000 items, ordered by item id, with stable names (`products-00001.jsonl.gz`, `products-00002.jsonl.gz`, …) that each push overwrites. Shards a smaller catalog no longer needs, and files of the other format, are deleted. The push signs in with the password, the private key (OpenSSH or PEM, with its passphrase), or both, all stored protected with the tenant's data protection keys, and checks the server's SHA-256 host key fingerprint when one is entered. The result is kept in the `AgentFeedStatusDocument`.

### ACP feed APIs

Each `AcpFeedTarget` in `AgentFeedSettings.FeedApiTargets` has a name, the feed API's https base address, the platform's bearer key (stored protected), a feed id, a target country, a currency and an enabled flag. `AcpFeedPusher`:

- creates the feed with `POST /feeds` (`{"target_country": "US"}`) when the target has no feed id, and keeps the id it gets in the `AgentFeedStatusDocument`;
- upserts complete products with `PATCH /feeds/{id}/products` in batches of 100, every product in the daily push and on **Push now**, and the changed products after a product is published or, with Inventory on, after its available units change (in a deferred task, once per unit of work).

Every call sends `Authorization: Bearer`, `API-Version: 2026-04-17` and an `Idempotency-Key` made from the target, the feed and the body sent, so a retry is the same request. Calls go through the egress-guarded HTTP client, which refuses addresses on the site's own network. Only items agents may show are sent; a product unpublished since the last push stays on the platform until it is changed there.

`AcpFeedProductMapper` makes one ACP `Product` per store product (`id`, `title`, `description.plain`, `url`, `media`) with each item as a `Variant`: `id` (the item id checkouts accept), `title`, `barcodes` (`gtin`), `price` and, during a sale, `list_price` as `{amount, currency}` in minor units with an uppercase code, `availability` (`available`, `status`), `categories`, `condition`, `variant_options`, `media`, and `seller` with its `terms_of_use`, `privacy_policy` and `return_policy` links.

### Recipes

`AgentFeedSettings` travels with Orchard Core's `Settings` recipe step. The secrets are stored protected, so enter them again on another site.

```json
{
  "name": "Settings",
  "AgentFeedSettings": {
    "TargetCountries": [ "US", "CA" ],
    "Sftp": {
      "Enabled": true,
      "Host": "sftp.example.com",
      "Port": 22,
      "Username": "contoso",
      "RemoteFolder": "/",
      "Format": "jsonl.gz"
    },
    "FeedApiTargets": [
      {
        "Id": "01kfeedtarget0000000000000",
        "Name": "Example agent",
        "BaseUrl": "https://api.example.com/acp",
        "TargetCountry": "US",
        "Enabled": true
      }
    ]
  }
}
```
