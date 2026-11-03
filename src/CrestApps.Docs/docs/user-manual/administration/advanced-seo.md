---
sidebar_label: Advanced SEO
title: "Advanced SEO: Describe Your Site to Search Engines and AI"
description: Describe the site and its pages with schema.org structured data, so search engines show rich results and AI agents understand your content.
technical_manual:
  - modules/advanced-seo
---

Structured data describes the site and each page in a standard vocabulary, [schema.org](https://schema.org), that search engines and AI agents read. A product page can say its price and whether it is in stock. An event can say when and where it takes place. Search engines use this for rich results, such as prices, dates and star ratings in search results, and AI agents use it to answer questions about your content.

| | |
| --- | --- |
| **Menu** | Settings > Search > Search Engine Optimization > Advanced SEO; Content Definition > Content Types (each content type) |
| **Permission** | Manage the advanced SEO settings (for the site settings); Edit content types (to add and set up the Schema.org part) |
| **Feature** | Advanced SEO |

<AskYourAdmin />

Once the feature is on, every page describes the site and who publishes it. To describe a content type's items, such as blog posts or events, add the **Schema.org** part to the content type and choose what they are. Products describe themselves, with their price and stock, without the part.

## Describe who publishes the site

1. Open **Settings > Search > Search Engine Optimization > Advanced SEO**.
2. Fill in the fields.
3. Click **Save**.

| Field | What it does |
| --- | --- |
| **Add structured data to pages** | When on, every public page describes the site and the content item it shows. On at first. |
| **Describe the web site** | When on, each page names the web site it belongs to. On at first. |
| **Describe the site search** | When on, search engines are told how to search the site, so they can show a search box for it. This needs the site's search to be on. On at first. |
| **Publisher** | Who publishes the site: an **Organization**, such as a company, or a **Person**. Choose **None** to leave it out. |
| **Publisher name** | The name of the organization or person. Leave it empty to use the site name. |
| **Logo URL** | The organization's logo, or the person's photo. A URL that starts with a slash is on this site. |
| **Profiles on other sites** | One web address per line, such as the publisher's social network pages. |

## Describe a content type

1. Open **Content Definition > Content Types** and click **Edit** next to the content type, such as **Blog Post**.
2. Click **Add Parts**, check **Schema.org** and click **Save**.
3. Next to the **Schema.org** part, click **Edit**.
4. In **Schema.org type**, choose what the items are, such as **BlogPosting**, **Event**, **Recipe** or **LocalBusiness**. The types are grouped, and each has a link to its description on schema.org and to Google's guide. To use any other schema.org type, choose **Another schema.org type…** and type its name.
5. The **Properties** list fills in with the properties the type needs, each taking its value from the most likely place. Each property is a card, like the items of a bag: its header shows the property, whether it is required or recommended, and where its value comes from (for example, *Headline ← Title*). Click a card to open it and change:
   - **Property**: the schema.org property. Properties marked **Required** or **Recommended** are the ones Google looks for.
   - **Value from**: where the value comes from. Choose one of the content type's fields, a value every item has (title, summary, body, page URL, author, dates, language, the site's publisher or name), or, under **Other**:
     - **A fixed value**, the same for every item;
     - **Editors enter it on each item**, which adds a box to the item's **Schema.org** tab;
     - **Liquid template**, for a value built from several fields.
   - A ★ marks the source the editor suggests for the property.
6. Use **Add property** to pick another property from the list, grouped by required, recommended and other, and **Add suggested properties** to add the suggested ones still missing. Reorder cards with the arrows, and remove one with its trash button.
7. A warning lists the required properties no card gives yet. Open **Preview** to see the shape of what each page will publish.
8. Click **Save**.

You don't need rows for the name, page address, description, dates and language: every item gets them. Add them only to take them from somewhere else, such as a **Teaser** field for the description.

## Fill in an item's details

When a property is set to **Editors enter it on each item**, the item's editor has a **Schema.org** tab with a box for it, such as **Start date** for an event or **Event status**. The tab also has:

- **Publish no structured data about this item**: leaves this item out of its page's structured data.
- **Preview of the JSON-LD**: what the item's page publishes, as it was last saved.

## Set it up with the AI assistant

When AI is on, the AI assistant can do this for you, if its profile has the **Search Engine Optimization** tools. Ask, for example:

- *"Describe blog posts as schema.org BlogPosting, with the image and tags."*
- *"Set up the Event content type for schema.org events; editors should enter the event status."*
- *"Our site is published by the organization Contoso, with the logo /media/logo.png and our LinkedIn page."*

It chooses the type, maps the properties to the content type's fields, and tells you which required properties are still missing. It needs the same permissions you do: **Edit content types** for content types, and **Manage the advanced SEO settings** for the site.

## The SEO part

When the content item has Orchard Core's **SEO** part, its **Meta description** and social image are used for the description and the image. JSON-LD that an editor types in the SEO part's **Google Schema** field is merged into the page's structured data, so the page still has a single description of itself.

## Pages that are not content items

Pages built by a developer, such as a hard-coded About or Contact page, can describe themselves in code, in their view or in their Liquid template. Ask your developer; the details are in the technical manual.

## Check a page

Open a page of the site and use a validator such as Google's **Rich Results Test** or the **Schema Markup Validator** at validator.schema.org with the page's address. Both show what the page says about itself, and warn about missing details.
