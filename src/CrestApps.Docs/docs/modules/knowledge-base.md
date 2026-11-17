---
sidebar_label: Knowledge Base
title: Knowledge Base
description: "Help articles for everyone, for signed-in people or for the roles you pick: the Knowledge Article content type, the help center at /help with search and categories, \"was this helpful?\" feedback, and suggestions for other modules."
user_manual: user-manual/administration/knowledge-base
---

The **Knowledge Base** holds help articles. It has no dependency on [Tickets](./tickets): a site can publish a help center on its own, and Tickets suggests articles when it is on too.

| Feature | Id | Depends on |
| --- | --- | --- |
| Knowledge Base | `CrestApps.OrchardCore.KnowledgeBase` | Contents, Title, Autoroute, Markdown, [Content Access Control](./content-access-control), Rate Limits |

## Articles

The feature adds a **Knowledge Article** content type (`KnowledgeArticle`): a title, `KnowledgeArticlePart`, a **Who can read it** role picker, a Markdown body and a path (`help/{slug}`). Articles are drafted, versioned and translated as any content item. `KnowledgeArticlePart` holds:

| Property | Holds |
| --- | --- |
| `Category` | The category the help center lists it under |
| `Summary` | One or two sentences shown in lists, searches and suggestions |
| `HelpfulCount`, `NotHelpfulCount` | Readers' answers to "Was this helpful?" |

## Who can read an article

The Knowledge Base has no audience of its own: who may read an article is [Content Access Control](./content-access-control)'s. The type carries a `RolePickerPart` named **Who can read it** with **Restrict content?** on, so:

| Roles picked | Who reads the article |
| --- | --- |
| None | Everyone, signed in or not |
| `Authenticated` | Anyone signed in |
| Staff roles (for example `Editor`) | People in one of those roles |

Administrators read every article. Content Access Control enforces this wherever an article is shown: every permission that reads it (view, view own, preview, preview own and list, in general or for the type, such as `ViewContent_KnowledgeArticle`) is refused to anyone outside its roles, whatever content permissions they hold. So Orchard's search results, which check **ViewContent** on each hit, leave restricted articles out for everyone else. Writing articles uses the type's content permissions.

Published articles are indexed in `KnowledgeArticleIndex` (title, summary, category, published date, helpful count), and Content Access Control indexes the roles allowed to read each one in `ContentAccessRoleIndex`. `KnowledgeArticleSearch` joins the two in SQL, through a subquery on the content item id matched against the reader's roles (`ContentAccessRoles.GetReaderRoles`), so the help center, a category, a search and suggestions only ever load what the reader may open.

### Upgrading from the article audience

Earlier versions kept a `KnowledgeArticlePart.Audience` and a **View staff-only knowledge articles** permission. The migration to version 2 adds the role picker to the type and moves every version of every article to it: `Public` picks no role, `SignedIn` picks `Authenticated`, and `Staff` picks the roles that held **View staff-only knowledge articles**, plus `Administrator` so a staff article stays restricted even when no other role held it. It rebuilds the category index without the audience; the index's nullable `Audience` column stays, unused, so a node still on version 1 keeps working during a rolling upgrade. The permission is gone; roles that held it keep a claim nothing reads.

## Help center

`/help` (route `KnowledgeBaseHome`) shows the categories with their article counts and the most helpful articles; `?category=` lists a category and `?q=` searches. A search matches articles with every word (up to five, two letters or more) in the title or the summary, most helpful first, among the articles the reader may read (`KnowledgeArticleSearch`). Each article ends with **Was this helpful?**, posted to `help/articles/{id}/feedback` and counted once per browser, under a lock on the article so answers given at the same time all count. Through Orchard Core's **Rate Limits** (the feature turns it on), a visitor address gives at most 20 answers in 10 minutes (group `knowledge-base-feedback`), so a script can't push an article up.

`help/api/suggest?q=` (route `KnowledgeBaseSuggest`) answers up to five articles as JSON (`title`, `summary`, `url`) for the reader, so other modules can suggest articles while someone writes: articles with any of the text's longer words (four letters or more, common words left out, the last eight) in the title or summary, those matching the most words first. With [Tickets](./tickets) on, the requester's **New request** form and the agent's reply box show these suggestions as they type; an agent adds an article's link to the reply with one click. `&reader=public` or `&reader=signedin` keeps the answer to articles that reader can open as well (the roles `Anonymous` or `Authenticated`, `KnowledgeArticleSearch.VisitorReader` and `SignedInReader`): a public reply offers only what its requester can read (articles for everyone, plus those for `Authenticated` when the requester has a login), while an internal note offers every article the agent may read.

## AI answers

Articles are content items, so an [AI data source](../ai/data-sources) that indexes the `KnowledgeArticle` type lets AI profiles answer from them. Index only articles for everyone (no role picked) for a public chat.
