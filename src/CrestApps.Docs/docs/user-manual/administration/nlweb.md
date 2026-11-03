---
sidebar_label: NLWeb
title: "NLWeb: Let People and AI Agents Ask Your Site Questions"
description: Answer questions about your site asked in plain language by people, apps and AI agents, with the NLWeb standard.
technical_manual:
  - modules/nlweb
---

**NLWeb** lets people, apps and AI agents ask your site questions in plain language, such as "vegetarian recipes with cinnamon" or "which plans include phone support?". The site answers with the content items that match, each described with the same structured data its page publishes, and, when AI is on, a short summary or an answer written from them. NLWeb is an open standard, so AI assistants and search tools that support it can use your site without any custom work.

| | |
| --- | --- |
| **Menu** | Settings > Search > NLWeb |
| **Permission** | Ask the site questions with NLWeb (everyone has it at first); Manage the NLWeb settings (to change the settings) |
| **Feature** | NLWeb (it turns on Advanced SEO too) |

<AskYourAdmin />

## Before you start

NLWeb finds content with the site's search, so the site needs at least one of these:

- The **Search** feature, a search provider such as **Lucene** or **Elasticsearch**, and a search index of your content. The settings page warns you when no search provider is on.
- When AI is on, an **AI data source** of your content, which finds content by meaning rather than by words.

## Choose what NLWeb answers from

1. Open **Settings > Search > NLWeb**.
2. Tick the **Search indexes** to answer from. The first one you tick is used unless a question names another one.
3. Optionally, tick the **Content types** answers can include. Leave them all unticked to include every content type.
4. Set the other fields.
5. Click **Save**.

| Field | What it does |
| --- | --- |
| **Search indexes** | The indexes questions are answered from. |
| **Content types** | The content types answers can include. Answers only ever include published content items the person asking may see. |
| **Number of results** | The most results an answer has, from 1 to 50. 10 at first. |
| **Default mode** | What a question gets when it does not ask for something else: **List** (the results only), **Summarize** (the results and a summary of them) or **Generate** (an answer written from the results). Summaries and answers need AI. |
| **Allow calls from other sites** | When on, pages and assistants on other sites can ask questions from the browser. They always get the answers a visitor who is not signed in would get. On at first. |
| **Accept the original protocol** | When on, the site also answers apps that use the first version of NLWeb. On at first. |

## Use AI to improve the answers

When AI is on, the settings page has an **Artificial intelligence** section. Each question then costs one or more calls to the AI model.

| Field | What it does |
| --- | --- |
| **AI deployment** | The AI model used. **Default utility deployment** uses the site's default small model. |
| **Understand follow-up questions** | When on, a follow-up such as "with cinnamon?", asked after "vegetarian muffins", is understood as "vegetarian muffins with cinnamon". On at first. |
| **Rank the results** | When on, the AI orders the results by how well they answer the question and drops the ones that do not. Off at first. |
| **Allow summaries** | When on, a question can ask for a summary of the results. On at first. |
| **Allow answers** | When on, a question can ask for an answer written only from the results, with links to them. On at first. |
| **AI data source** | Shown when AI data sources are on. A data source of your content that is also searched by meaning. The results of the search indexes and of the data source are merged. |

## Let AI assistants ask through MCP

AI assistants that connect to the site with MCP can use NLWeb through the **Ask the Site (NLWeb)** tool. Tick it in the tools list of the **MCP Server** settings, as you would any AI tool. You can also offer it to the site's own AI chat in an AI profile. The tool answers as the person or app that uses it.

## Who can ask

Everyone can ask questions at first, including visitors who are not signed in, because the answers only hold content they could already see on the site. To limit NLWeb to some people, remove the **Ask the site questions with NLWeb** permission from the **Anonymous** and **Authenticated** roles in **Security > Roles**, and give it to the roles that may ask.

## Tips

- What NLWeb returns about each content item is its structured data. To improve the answers, add the Schema.org part to your content types and describe them well; see [Advanced SEO](advanced-seo.md).
- When the search indexes change, for example when you add a content type to an index, rebuild the index so NLWeb finds the new content.
