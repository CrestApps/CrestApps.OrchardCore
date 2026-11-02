---
sidebar_label: Structured Data
title: "Structured Data: Describe Your Site to Search Engines and AI"
description: Describe the site and its pages with schema.org structured data, so search engines show rich results and AI agents understand your content.
technical_manual:
  - modules/structured-data
---

Structured data describes the site and each page in a standard vocabulary, [schema.org](https://schema.org), that search engines and AI agents read. A product page can say its price and whether it is in stock. An article can say when it was published. Search engines use this for rich results, such as prices and star ratings in search results, and AI agents use it to answer questions about your content.

| | |
| --- | --- |
| **Menu** | Settings > Search > Search Engine Optimization > Structured Data; Content Definition > Content Types (each content type) |
| **Permission** | Manage the structured data settings (to change the settings); Edit content types (for the content type settings) |
| **Feature** | Structured Data |

<AskYourAdmin />

Once the feature is on, every page of the site describes the site and the content item it shows. You do not need to change anything for it to work. The settings below let you say who publishes the site and tune how each content type is described.

## Describe who publishes the site

1. Open **Settings > Search > Search Engine Optimization > Structured Data**.
2. Fill in the fields.
3. Click **Save**.

| Field | What it does |
| --- | --- |
| **Add structured data to pages** | When on, every public page describes the site and the content item it shows. On at first. |
| **Publisher** | Who publishes the site: an **Organization**, such as a company, or a **Person**. Choose **None** to leave it out. |
| **Publisher name** | The name of the organization or person. Leave it empty to use the site name. |
| **Logo URL** | The organization's logo, or the person's photo. A URL that starts with a slash is on this site. |
| **Profiles on other sites** | One web address per line, such as the publisher's social network pages. |
| **Describe the web site** | When on, each page names the web site it belongs to. On at first. |
| **Describe the site search** | When on, search engines are told how to search the site, so they can show a search box for it. This needs the site's search to be on. On at first. |

## Describe a content type

Each content type can say what kind of thing its content items are, and which details to publish.

1. Open **Content Definition > Content Types** and click **Edit** next to the content type.
2. Find the **Structured data** section.
3. Fill in the fields.
4. Click **Save**.

| Field | What it does |
| --- | --- |
| **Publish no structured data** | When on, the content items of this type are left out of the page's structured data and are not described to AI agents. |
| **Schema.org type** | The kind of thing, such as **Article**, **BlogPosting**, **Event**, **Recipe** or **LocalBusiness**. The box suggests common types as you type. Leave it empty to let the site choose: products are described as products, and everything else as a web page. |
| **Schema.org properties** | Extra details, one per line, written as a name, a colon and a value, such as `headline: {{ ContentItem.DisplayText }}`. The value can use Liquid to read the content item. Your developer can help you write these. |

Without any setting, each content item is already described with its title, address, dates, language and a short description taken from its text. Products also publish their price, currency and stock, and their star rating when the site has reviews.

## The SEO part

When the content item has the **SEO** part, its **Meta description** and social image are used for the description and the image. JSON-LD that an editor types in the SEO part's **Google Schema** field is merged into the page's structured data, so the page still has a single description of itself.

## Check a page

Open a page of the site and use a validator such as Google's **Rich Results Test** or the **Schema Markup Validator** at validator.schema.org with the page's address. Both show what the page says about itself, and warn about missing details.
