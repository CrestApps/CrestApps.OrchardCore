---
sidebar_label: AI Knowledge Assistant
title: Build an AI Knowledge Assistant
description: Give an AI profile your company's documents, site content and data, so it answers from your own knowledge and shows where each answer came from.
technical_manual:
  - ai/documents/index
  - ai/data-sources/index
---

## Who it's for and what you get

For **AI content managers** who want an assistant that knows your company: your policies, product guides, price lists, help articles or website.

When it is done:

- an AI profile that searches your documents and data before it answers;
- answers that show the documents they came from, as references;
- if you want, an assistant that answers **only** from your knowledge and says so when it cannot find the answer;
- the same knowledge available wherever that profile is used: the admin chat, the website widget, or an AI agent answering customers.

<video controls preload="metadata" width="100%" aria-label="Screencast of creating an AI data source from a site content index, restricting a profile to the indexed data and asking it a question">
  <source src="/img/docs/ai-elasticsearch-datasource.mp4" type="video/mp4" />
</video>

## Before you start

The AI does not read your files whole. It splits them into passages and stores them in a **search index**, so it can find the right passage for each question. That index runs on a search service such as Elasticsearch or Azure AI Search, which IT provides.

| You need | Who sets it up |
| --- | --- |
| A search service (Elasticsearch or Azure AI Search) for the knowledge indexes | IT; see the [AI documents](../../ai/documents/index.md) and [AI data sources](../../ai/data-sources/index.md) technical pages |
| An AI connection with a chat deployment, and one that can create the search data (embeddings) | Your administrator or AI content manager; see [AI connections](../ai/connections.md) |
| For documents: **AI Documents for Profiles**, the indexing feature for your search service (**AI Documents indexing using Elasticsearch** or **AI Documents indexing using Azure AI Search**), and **AI Documents (PDF)** or **AI Documents (OpenXml)** for PDF, Word and PowerPoint files | Your administrator, in **Tools > Features** |
| For data sources: the data source feature for your search service (**AI Data Sources - Elasticsearch** or **AI Data Sources - Azure AI Search**; **AI Data Sources - PostgreSQL** for a database), and **AI Web Crawlers** or **AI File Sources** if the knowledge lives on a website or a file server | Your administrator |
| Permission to manage AI profiles and AI data sources | Your administrator; see [Roles and permissions](../getting-started/roles-and-permissions.md) |

<AskYourAdmin />

## Steps

1. **Set up the knowledge indexes.** The administrator adds the indexes under **Search > Indexing** and picks the default document index under **Settings > Artificial Intelligence**. See [Knowledge](../ai/knowledge.md).
2. **Pick the profile.** Create or open the AI profile that should use the knowledge, under **Artificial Intelligence > Profiles**. See [AI profiles](../ai/profiles.md).
3. **Upload documents.** On the profile's **Knowledge** tab, upload your files: text, Markdown, HTML, PDF, Word or PowerPoint, depending on the features that are on. They become background knowledge for every conversation with the profile. See [Knowledge](../ai/knowledge.md).
4. **Connect larger or changing knowledge as a data source.** For your site's own content, a search index, a database table, or a website, add a data source under **Artificial Intelligence > Data Sources** with **Add Data Source**. Website pages are collected by a web crawler under **Artificial Intelligence > Web Crawlers**. Data sources keep themselves up to date as the source changes. See [Knowledge](../ai/knowledge.md).
5. **Attach the data source to the profile.** Pick the data source on the profile's **Knowledge** tab.
6. **Decide how strict it is.** Tick **Restrict answers to retrieved data only** if the assistant must not answer from general knowledge. Leave it off if your knowledge should come first but the AI may fill gaps. See [Knowledge](../ai/knowledge.md).
7. **Test and refine.** Chat with the profile and ask questions your documents answer. Check the references under each answer. See [AI chat](../ai/chat.md).
8. **Put it to work.** Use the profile on your website, in the admin, or as an AI agent that answers customers. See [Put an AI assistant on your website](ai-assistant-on-your-website.md) and [Let the AI answer your customers](ai-answers-customers.md).

## Check that it works

1. Ask a question whose answer is only in one of your documents. The answer should match the document and list it as a reference.
2. Ask a question your knowledge does not cover. With **Restrict answers to retrieved data only** on, the assistant should say it cannot find the answer instead of guessing.
3. Change a page or record in a data source, wait for it to sync, and ask again.

## Tips

- Start with a few good, current documents rather than everything you have. Out-of-date files lead to out-of-date answers.
- Remove a document from the profile when it is replaced, and upload the new one.
- Spreadsheets cannot be uploaded as profile knowledge; they are for data analysis in chat, not for answering questions.
- If the profile shows a warning that the index is not configured, ask your administrator to pick the document index under **Settings > Artificial Intelligence**.
- To let people upload their own files during a chat, see [Chat interactions](../ai/chat-interactions.md).
