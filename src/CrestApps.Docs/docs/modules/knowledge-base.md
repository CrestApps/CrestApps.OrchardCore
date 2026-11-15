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

The audience is enforced wherever an article is shown: an authorization handler fails every permission that reads an article (view, view own, preview, preview own and list, in general or for the type, such as `ViewContent_KnowledgeArticle`, compared by name) for anyone outside its audience, whatever content permissions they hold. So Orchard's search results, which check **ViewContent** on each hit, leave staff articles out for everyone else. Writing articles uses the type's content permissions. Published articles are indexed in `KnowledgeArticleIndex` (title, summary, category, audience, published date, helpful count).

## Help center

`/help` (route `KnowledgeBaseHome`) shows the categories with their article counts and the most helpful articles; `?category=` lists a category and `?q=` searches. A search matches articles with every word (up to five, two letters or more) in the title or the summary, most helpful first, among the articles the reader may read (`KnowledgeArticleSearch`). Each article ends with **Was this helpful?**, posted to `help/articles/{id}/feedback` and counted once per browser, under a lock on the article so answers given at the same time all count. With Orchard Core's **Rate Limits** on, a visitor address gives at most 20 answers in 10 minutes (group `knowledge-base-feedback`), so a script can't push an article up.

`help/api/suggest?q=` (route `KnowledgeBaseSuggest`) answers up to five articles as JSON (`title`, `summary`, `url`) for the reader, so other modules can suggest articles while someone writes: articles with any of the text's longer words (four letters or more, common words left out, the last eight) in the title or summary, those matching the most words first. With [Tickets](./tickets) on, the requester's **New request** form and the agent's reply box show these suggestions as they type; an agent adds an article's link to the reply with one click. `&reader=public` or `&reader=signedin` keeps the answer to articles that reader can open: a public reply offers only what its requester can read (public, plus signed-in articles when the requester has a login), while an internal note offers every article the agent may read.

## AI answers

Articles are content items, so an [AI data source](../ai/data-sources) that indexes the `KnowledgeArticle` type lets AI profiles answer from them. Index only public articles for a public chat.
