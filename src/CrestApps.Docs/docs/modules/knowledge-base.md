---
sidebar_label: Knowledge Base
title: Knowledge Base
description: "Help articles for everyone, for signed-in people or for staff: the Knowledge Article content type, the help center at /help with search and categories, \"was this helpful?\" feedback, and suggestions for other modules."
user_manual: user-manual/administration/knowledge-base
---

The **Knowledge Base** holds help articles. It has no dependency on [Tickets](./tickets): a site can publish a help center on its own, and Tickets suggests articles when it is on too.

| Feature | Id | Depends on |
| --- | --- | --- |
| Knowledge Base | `CrestApps.OrchardCore.KnowledgeBase` | Contents, Title, Autoroute, Markdown |

## Articles

The feature adds a **Knowledge Article** content type (`KnowledgeArticle`): a title, `KnowledgeArticlePart`, a Markdown body and a path (`help/{slug}`). Articles are drafted, versioned and translated as any content item. `KnowledgeArticlePart` holds:

| Property | Holds |
| --- | --- |
| `Audience` | `Public` (everyone), `SignedIn` (anyone signed in) or `Staff` (people with **View staff-only knowledge articles**) |
| `Category` | The category the help center lists it under |
| `Summary` | One or two sentences shown in lists, searches and suggestions |
| `HelpfulCount`, `NotHelpfulCount` | Readers' answers to "Was this helpful?" |

The audience is enforced wherever an article is shown: an authorization handler fails **ViewContent** and **PreviewContent** on an article for anyone outside its audience, whatever content permissions they hold. Writing articles uses the type's content permissions. Published articles are indexed in `KnowledgeArticleIndex` (title, summary, category, audience, published date, helpful count).

## Help center

`/help` (route `KnowledgeBaseHome`) shows the categories with their article counts and the most helpful articles; `?category=` lists a category and `?q=` searches. A search matches articles with every word (up to five, two letters or more) in the title or the summary, most helpful first, among the articles the reader may read (`KnowledgeArticleSearch`). Each article ends with **Was this helpful?**, posted to `help/articles/{id}/feedback` and counted once per browser.

`help/api/suggest?q=` (route `KnowledgeBaseSuggest`) answers up to five matching articles as JSON (`title`, `summary`, `url`) for the reader, so other modules can suggest articles while someone writes.

## AI answers

Articles are content items, so an [AI data source](../ai/data-sources) that indexes the `KnowledgeArticle` type lets AI profiles answer from them. Index only public articles for a public chat.
