---
sidebar_label: AI agents
title: Sell through AI agents
description: Let AI agents such as Google AI Mode, Gemini and ChatGPT buy from your store for their users, with your own prices, tax, shipping, discounts and stock, and see the orders they place.
technical_manual:
  - modules/agentic-commerce
---

An **AI agent** is an assistant, such as Google AI Mode, Gemini or ChatGPT, that shops for its user. When a buyer asks it to buy something you sell, the agent finds your store, puts the items in a checkout, and pays with a card the buyer saved with the agent's company. Your store stays in charge: the agent sees your prices, tax, shipping, discounts and stock, exactly as a buyer on your site does, and the order is an ordinary order in your store.

The business that runs the agents is called an **agent platform**, such as Google or OpenAI. You choose which platforms may buy from your store, and you give each one its own key.

| | |
| --- | --- |
| **Menu** | Commerce > AI agents for the platforms, and Settings > Commerce > Agentic commerce for the settings |
| **Permission** | **Manage AI agent platforms** for the platforms, and **Manage agentic commerce settings** for the settings |
| **Feature** | Agentic Commerce, with Universal Commerce Protocol (for Google's agents) and Agentic Commerce Protocol (for ChatGPT) |

<AskYourAdmin />

## How an agent buys

Agents speak one of two open languages, called **protocols**. Google's agents use the **Universal Commerce Protocol** (UCP). ChatGPT uses the **Agentic Commerce Protocol** (ACP). Your administrator turns on the protocols of the platforms you sell through.

1. The agent finds your products, in your product feeds or by searching your store.
2. It starts a checkout with the items, the buyer's e-mail address and the address to ship to. Your store works out the prices, tax, shipping and discounts, and holds the stock.
3. The buyer approves the purchase with the agent, and the agent pays with a payment token from the buyer's saved card. The card number never reaches your store.
4. Your store charges the token through your payment provider, the order is paid, and the agent tells the buyer it is done.
5. When the order ships, is delivered, is canceled or is refunded, your store tells the platform, so the agent can tell the buyer.

Sometimes only the buyer can finish a purchase: their bank asks them to approve the payment, or your checkout asks for something the agent can't give. Then the agent gives the buyer a link that opens your own checkout in their browser, with the items already in it.

## Before you start

Platforms ask for a few things before their agents may buy:

1. Set your site's base URL under **Settings > General**, so links to your store work outside it.
2. Fill in **Website and policies** under **Settings > Commerce > Store** (below). Google and OpenAI ask for your terms of service and your privacy policy.
3. Connect your payment provider. Agents pay through Stripe; see [Payment providers](payment-providers.md#connect-stripe).
4. Choose which products agents may show and buy, in your product feed's settings and on each product. See [Product listings](product-listings.md#feeds-for-ai-agents).

### Website and policies

| | |
| --- | --- |
| **Menu** | Settings > Commerce > Store |
| **Permission** | Manage the store profile |
| **Feature** | Store Profile |

<AskYourAdmin />

The **Website and policies** card holds the links that checkouts, product feeds and AI agents give buyers. Enter a full address, such as *https://contoso.com/terms*, or a page of this site, such as */terms*. Leave a field empty when you don't have that page.

| Field | What it does |
| --- | --- |
| **Website** | Your store's own website, when it isn't this site. |
| **Terms of service** | Your terms of service. Required by Google and OpenAI. |
| **Privacy policy** | Your privacy policy. Required by Google and OpenAI. |
| **Return policy** | Your return policy. |
| **Shipping policy** | Your shipping policy. |
| **Frequently asked questions** | Your questions and answers page. |
| **Contact page** | How buyers contact you. |
| **About page** | About your store. |

Click **Save**. Agents show these links to the buyer with every checkout.

## Add an agent platform

1. Open **Commerce > AI agents** and click **Add platform**.
2. Fill in the fields (below). You need at least a **Name** and one **Protocol**.
3. Click **Save**.
4. Open the platform again and give it an API key (see [Give the platform its API key](#give-the-platform-its-api-key)).

**Commerce > AI agents** lists your platforms with their protocols and when they were last changed. A **Test mode** tag shows a platform in test mode, and **Disabled** one whose agents may not buy. Click **Edit** to change a platform.

### The platform

| Field | What it does |
| --- | --- |
| **Name** | The business whose agents buy from your store, such as *Google* or *OpenAI*. Required. |
| **Notes** | Anything you want to remember about it. |
| **Protocols** | The protocols its agents use: **Universal Commerce Protocol (UCP)**, **Agentic Commerce Protocol (ACP)**, or both. Choose at least one. Agents can only use a protocol whose feature your administrator turned on. |
| **Its agents may buy** | Untick it to stop the platform's agents at once. Their open checkouts can't be completed. |
| **Test mode** | Its agents may only pay with test credentials, which are never charged. Use it while the platform checks your store. See [Test a platform](#test-a-platform). |

### How it signs in

| Field | What it does |
| --- | --- |
| **API key** | The key the platform sends with each request. Shown after the first save. See [Give the platform its API key](#give-the-platform-its-api-key). |
| **OAuth client ID** | For a platform that signs in through your site's OpenID Connect server instead of with an API key, and for buyers who link their store account to an agent. Your administrator creates the application under **Security > OpenID Connect > Applications** and gives you its client ID. |
| **UCP profile** | The address of the platform's UCP profile, such as *https://platform.example/.well-known/ucp*. The platform gives it to you. Your store checks the platform's signed requests with the keys in it. |
| **Allowed IP addresses** | One address, or one range such as *203.0.113.0/24*, per line. Requests from other addresses are refused. Leave it empty to allow any address. OpenAI asks stores to allow only the address ranges it publishes. |

### ACP request signatures

Some ACP platforms sign each request with their own private key, so your store can check that the request really comes from them. Ask the platform whether it signs, and for its public key.

| Field | What it does |
| --- | --- |
| **Require signed requests** | Refuse every ACP request that isn't signed with the platform's key. Leave it unticked if the platform doesn't sign. Even when it's unticked, a signed request is checked when a public key is set, and refused if the signature is wrong. |
| **Algorithm** | How the platform signs. **ES256** is the only choice. |
| **Platform's public key** | The public key the platform gives you, as a JSON Web Key (it starts with *{"kty":"EC"*) or a PEM block (it starts with *-----BEGIN PUBLIC KEY-----*). It must be a P-256 key. Never paste a private key: the store refuses it. The public key isn't secret, so it is copied when you export the platform. |
| **Allowed clock skew** | How many seconds the time on a signed request may differ from your store's clock. Leave it empty for 5 minutes. |

### Embedded checkout

| Field | What it does |
| --- | --- |
| **Embedding origins** | The addresses of the platform's pages that may show your checkout inside them, one per line, written as *https://* and a host with no path, such as *https://agent.example*. The platform gives them to you. Only this platform's checkouts can be shown there, and only when **Allow embedded checkout** is on. Leave it empty to keep your checkout out of the platform's pages. UCP only. See [Let buyers finish inside the agent](#let-buyers-finish-inside-the-agent). |

### Payments and order updates

| Field | What it does |
| --- | --- |
| **Payment handlers** | The ways its agents may pay, one per line, such as *com.stripe.payments*, *com.google.pay* or *dev.acp.tokenized.card*. Leave it empty to allow every way your store offers. |
| **Order updates address** | Where your store sends updates about the platform's orders. UCP platforms give it in their profile, so you only need it for ACP platforms. |
| **Signing secret** | The secret ACP order updates are signed with, so the platform knows they came from your store. Shown after the first save. See [Give the platform a signing secret](#give-the-platform-a-signing-secret). |
| **ACP new order event** | The name of the update a new order sends. Leave it empty for *order_create*, the name in the ACP specification; some platforms expect *order_created*. |
| **ACP changed order event** | The name of the update a changed order sends. Leave it empty for *order_update*. |

## Give the platform its API key

1. Open the platform under **Commerce > AI agents**.
2. In **How it signs in**, click **New API key**, and confirm.
3. A message shows the new key, which starts with *agk_*. Copy it now and give it to the platform in a safe way: it isn't shown again.

Afterwards, the platform's page only shows the key's last four characters, so you can tell keys apart. A new key replaces the old one at once: the platform must use the new key from then on. Give a platform a new key when you think someone else has seen it.

## Give the platform a signing secret

ACP platforms check that order updates really come from your store with a shared secret.

1. Open the platform under **Commerce > AI agents**.
2. In **Payments and order updates**, click **New secret**.
3. A message shows the secret, which starts with *whsec_*. Copy it now and give it to the platform: it isn't shown again.

Until the platform has both an **Order updates address** and a signing secret, no ACP order updates are sent to it. UCP order updates are signed with your store's own keys, which the store makes and replaces by itself.

## Set how agents check out and pay

| | |
| --- | --- |
| **Menu** | Settings > Commerce > Agentic commerce |
| **Permission** | Manage agentic commerce settings |
| **Feature** | Agentic Commerce |

<AskYourAdmin />

Open **Settings > Commerce > Agentic commerce**, change what you need, and click **Save**.

### Agent checkouts

| Field | What it does |
| --- | --- |
| **Checkout lifetime (hours)** | How long an agent's checkout holds its items in stock before it expires, from 1 to 72 hours. 6 hours at first, the same as UCP's default. |
| **Open checkouts per platform** | The most checkouts one platform may keep open at once, so no agent can hold all your stock. 1,000 at first. |
| **Let UCP platforms the store doesn't know buy** | A UCP platform that isn't in your list may buy when it signs its requests with a key from its own published profile. Off at first. |
| **Allow embedded checkout** | A UCP platform may show your checkout inside its own page, so the buyer finishes without leaving the agent. Only the pages a platform lists under **Embedding origins** may show its own checkouts; every other page of your site still refuses to be shown inside another site. Off at first. |

### How agents pay

| Field | What it does |
| --- | --- |
| **Stripe business profile** | Your Stripe business profile ID, which starts with *profile_*. Google's agents give your store payment tokens for it. Leave it empty to not offer Stripe tokens to UCP agents. ChatGPT's agents pay with Stripe tokens without it. |
| **Offer Google Pay to UCP agents** | Lets Google's agents pay with Google Pay. Google Pay payments are charged through Stripe, so Stripe must be connected. |
| **Google Pay merchant ID** | Your Google Pay merchant ID. |
| **Google Pay merchant name** | The name Google Pay shows the buyer. Leave it empty to use your store's name. |
| **Use Google Pay's test environment** | Google Pay test payments are never charged. On at first. Turn it off only when Stripe uses your live keys. |

### 3-D Secure by agents

Some banks ask the buyer to approve a payment with 3-D Secure. Normally the agent then gives the buyer a link to your checkout, where they approve it. Some agents that use ACP can run 3-D Secure with the buyer themselves and send your store the result, so the buyer never leaves the chat. Your payment provider uses that result for the same payment, so the buyer is charged once.

| Field | What it does |
| --- | --- |
| **Let agents run 3-D Secure** | Asks agents that say they can run 3-D Secure to do it, instead of sending the buyer to your checkout. Off at first. It needs the four details below, and a payment provider that takes an outside 3-D Secure result. With Stripe, ask Stripe to turn on 3D Secure import for your account. |
| **Acquirer BIN** | Your acquirer's BIN, up to 11 characters. Your payment provider gives it to you. |
| **Acquirer country** | Your acquirer's two-letter country code, such as *US*. |
| **Acquirer merchant ID** | Your merchant ID at the acquirer, up to 35 characters. |
| **Merchant name** | The name the buyer's bank shows while it checks the buyer, up to 40 characters. |
| **3-D Secure requestor ID** | Optional. Leave it empty when your acquirer gave you none. |

Agents that can't run 3-D Secure, and agents that use UCP, such as Google's, still send the buyer to your checkout. If the bank doesn't approve the buyer, the payment isn't taken and the agent can pay another way.

## Test a platform

Platforms check a store before they send it real buyers. While they do, tick **Test mode** on the platform: its agents may then only pay with test credentials, and nothing is charged.

Test credentials need the **Test payments** feature, which your administrator turns on, usually on a test site only. While it is on, the store offers agents three test credentials:

| Test credential | What happens |
| --- | --- |
| **tok_test_success** | The payment succeeds and the order is paid. |
| **tok_test_decline** | The payment is declined. The agent can try again. |
| **tok_test_3ds** | The buyer's bank asks them to approve the payment. The agent gets a link that opens your checkout, where the buyer finishes it. With **Let agents run 3-D Secure** on, an agent that can run 3-D Secure is asked to instead, and the payment succeeds when it sends an approved result. |

When the platform has finished checking your store, untick **Test mode** and click **Save**.

:::note[Test credentials and live platforms]
Only a platform in test mode may pay with test credentials. A platform that isn't in test mode never sees the test card, even while the Test payments feature is on.
:::

## Orders from AI agents

An agent's order is an ordinary order in **Commerce > Orders**: you fulfill, ship, cancel and refund it as usual, and its stock, tax and receipts work as for any order. To see only agent orders, choose **AI agents** in the order list's channel filter.

- An agent's order is held as a draft while the agent's checkout is open, and is paid when the agent completes it. A checkout that is canceled, or that expires after the **Checkout lifetime**, releases the stock it held.
- When the agent changes the items, the store replaces the draft order with a new one, as it does when a buyer changes their cart.
- When an agent's order is paid, canceled, refunded or returned, or its shipment ships, is delivered or comes back, the store tells the platform. If the platform doesn't answer, the store tries again for nearly two days.
- A buyer who opens the link an agent gave them finishes the checkout in your store's own checkout, like any buyer.

## Let buyers finish inside the agent

Some UCP platforms can show your checkout inside their own page instead of sending the buyer to your site (UCP calls it embedded checkout). The buyer sees your checkout, with your prices, shipping and payment methods, and finishes it there; the platform hears when the order is placed.

1. Ask the platform for the addresses of the pages that show checkouts, such as *https://agent.example*.
2. Open the platform under **Commerce > AI agents**, enter them in **Embedding origins** and click **Save**.
3. Open **Settings > Commerce > Agentic commerce**, tick **Allow embedded checkout** and click **Save**.

Your store then tells the platform it can embed its checkouts. Your checkout only works inside the platform's page when your site uses *https://*. If the platform signs in with an OpenID Connect application, it can also prove it is the platform when it shows the checkout. To stop it, untick **Allow embedded checkout** or empty the platform's **Embedding origins**; the buyer then gets the ordinary link to your checkout.

## Let buyers link their account

A buyer can link their account on your store to an agent, so the agent checks out as them, with their saved addresses, and can see their orders. This needs the **UCP Account Linking** feature and your site's OpenID Connect server, which your administrator sets up, and an OpenID Connect application for the platform. Enter that application's client ID in the platform's **OAuth client ID**. The buyer approves the link on your site's sign-in page. Agents find your sign-in server's details at */.well-known/oauth-authorization-server* on your site.

## Agents that talk to other agents

Some platforms send their agent to talk to an agent of your store, instead of calling your store directly. They use the **Agent2Agent** (A2A) standard for this. Your administrator turns on the **UCP over A2A** feature to give your store such an agent. It needs no AI features.

- The agent buys with the same checkout, prices and rules as every other agent. Its orders are ordinary agent orders.
- A platform signs in with the same key or token as before. You don't set anything up for A2A.
- Your store's UCP address tells platforms where to find the agent. They find it on their own.

## Product feeds and your own AI assistant

AI agents also read your products from feeds. **Settings > Commerce > Agent feeds** publishes them in OpenAI's format for ChatGPT and pushes them to the platforms that host a feed. See [Product listings](product-listings.md#feeds-for-ai-agents).

Some agents use the Model Context Protocol (MCP) instead of plain web requests. Your administrator turns on **UCP over MCP** or **ACP over MCP** for them. Nothing changes on the platform: it signs in with the same key.

Your own AI assistant can sell too. When the AI features are on, your administrator can pick the **Search store products**, **Get store product**, **Add to store cart**, **Get store cart** and **Start store checkout** tools on an AI profile. The assistant then finds products for a visitor, fills the visitor's own cart, and gives them a link to your checkout. It never pays for them. See [Tools, Agents and Connections](../ai/tools-and-agents.md).

A shopper's own browser assistant can use the same tools on your store's pages, even when the shopper isn't signed in. Turn on **Offer storefront tools to visitors** on the **WebMCP** card and tick the tools. See [WebMCP](../ai/webmcp.md#let-shoppers-assistants-use-your-store).

## Stop or remove a platform

- To pause a platform, untick **Its agents may buy** and click **Save**. Its agents can't start, change or complete checkouts until you tick it again.
- To remove a platform, click **Delete** in the list and confirm. Its agents can't buy any more, and its key stops working.

## Troubleshooting

| Problem | What to check |
| --- | --- |
| **Commerce > AI agents** isn't in the menu | You need **Manage AI agent platforms**, and the Agentic Commerce feature must be on. Ask your administrator. |
| A platform's agents can't reach the store with a protocol | Your administrator must turn on that protocol's feature, and the protocol must be ticked on the platform. |
| The platform says the store refused its key | Check that you gave it the latest key, that **Its agents may buy** is ticked, and that its address is in **Allowed IP addresses**. Give it a new key if you aren't sure. |
| An agent can't pay | Check that Stripe is connected and, for Google's agents, that **Stripe business profile** is filled in. A platform in **Test mode** can only pay with test credentials. A **Payment handlers** list that isn't empty limits how its agents pay. |
| The platform says its signed requests are refused | Check that **Platform's public key** is the key the platform signs with, and that the platform's clock is right. If the platform doesn't sign, untick **Require signed requests**. |
| The platform gets no order updates | For an ACP platform, fill in **Order updates address** and give it a signing secret. For a UCP platform, its profile must give the address, or fill in **Order updates address**. |
| A platform can't show your checkout inside its page | Check that **Allow embedded checkout** is ticked, that the page's address is in the platform's **Embedding origins** exactly (*https://*, the host and any port, no path), and that your site uses *https://*. |
| A product isn't offered to agents | Check **Show to AI agents** and **Let AI agents check out** on the product and in the feed's settings. See [Product listings](product-listings.md#feeds-for-ai-agents). |
