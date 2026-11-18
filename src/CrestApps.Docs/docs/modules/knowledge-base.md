---
sidebar_label: Knowledge Base
title: Knowledge Base
description: "Help articles for everyone, for signed-in people or for the roles you pick: the Knowledge Article content type, the help center at /help with search and categories, \"was this helpful?\" feedback, and suggestions for other modules."
user_manual: user-manual/knowledge-base/index
---

The **Knowledge Base** holds help articles. It is a feature of its own, in the **Content Management** category of **Tools > Features**: it has no dependency on [Tickets](./tickets), so a site can publish a help center without a service desk, and Tickets suggests articles when both are on.

| Feature | Id | Depends on |
| --- | --- | --- |
| Knowledge Base | `CrestApps.OrchardCore.KnowledgeBase` | Contents, Title, Autoroute, Markdown, Taxonomies, [Content Access Control](./content-access-control), Rate Limits |

## Articles

The feature adds a **Knowledge Article** content type (`KnowledgeArticle`): a title, Content Access Control's role picker (**Which roles can see this content** on the editor), `KnowledgeArticlePart`, a **Category** field, a Markdown body labelled **Body** (with Orchard Core's Wysiwyg editor) and a path (`help/{slug}`, from its Autoroute part), in that order. Each part has a whole-number position, which Orchard Core's content type editor needs. Articles are drafted, versioned and translated as any content item. `KnowledgeArticlePart` holds:

| Property | Holds |
| --- | --- |
| `Summary` | One or two sentences shown in lists, searches and suggestions |
| `HelpfulCount`, `NotHelpfulCount` | How many readers answered yes and no to "Was this helpful?", which the help center sorts by. Each answer is also a dated record; see [Feedback](./knowledge-base/feedback) |

## Who can read an article

The Knowledge Base has no audience of its own: who may read an article is [Content Access Control](./content-access-control)'s. The type carries a `RolePickerPart` with **Restrict content?** on (**Which roles can see this content** on the editor, reading **Any role** while none is picked), so:

| Roles picked | Who reads the article |
| --- | --- |
| None | Everyone, signed in or not |
| `Authenticated` | Anyone signed in |
| Staff roles (for example `Editor`) | People in one of those roles |

Administrators read every article. Content Access Control enforces this wherever an article is shown: every permission that reads it (view, view own, preview, preview own and list, in general or for the type, such as `ViewContent_KnowledgeArticle`) is refused to anyone outside its roles, whatever content permissions they hold. So Orchard's search results, which check **ViewContent** on each hit, leave restricted articles out for everyone else. Writing articles uses the type's content permissions.

Published articles are indexed in `KnowledgeArticleIndex` (title, summary, category term, published date, helpful count), and Content Access Control indexes the roles allowed to read each one in `ContentAccessRoleIndex`. `KnowledgeArticleSearch` joins the two in SQL, through a subquery on the content item id matched against the reader's roles (`ContentAccessRoles.GetReaderRoles`), so the help center, a category, a search and suggestions only ever load what the reader may open.

### Upgrading from the article audience

Earlier versions kept a `KnowledgeArticlePart.Audience` and a **View staff-only knowledge articles** permission. The migration to version 5 adds the role picker to the type, after the title, and moves every version of every article to it: `Public` picks no role, `SignedIn` picks `Authenticated`, and `Staff` picks the roles that held **View staff-only knowledge articles**, plus `Administrator` so a staff article stays restricted even when no other role held it. It rebuilds the category index without the audience; the index's `Audience` column stays, unused, so a node still on version 4 keeps working during a rolling upgrade. The permission is gone; roles that held it keep a claim nothing reads.

## Categories

An article's category is a term of a taxonomy: the Knowledge Article type has a **Category** taxonomy field (`KnowledgeBaseConstants.CategoryField`, on the type's own part, one term) that uses the **Knowledge Base Categories** taxonomy the feature creates, with **Knowledge category** terms (`KnowledgeCategory`). Categories are managed as any taxonomy, under **Content > Taxonomies**, and can be nested; an administrator may point the field at another taxonomy. `KnowledgeArticleIndex.CategoryId` holds the term, and `KnowledgeBaseCategories` reads the taxonomy's terms (`TaxonomyTermTree`). The permalink comes from the type's Autoroute part (`help/{slug}`). Articles saved before categories were terms kept them as text on the part: on upgrade, each distinct text (without regard to case) becomes a top term, or finds the term of that name, every version of its articles gets it, and the text goes.

## Admin menu

The module adds no admin menu of its own. Articles and the **Knowledge Base Categories** taxonomy are content items, listed under **Content > Content Items** (the `KnowledgeArticle` type is listable and creatable), and the [feedback report](./knowledge-base/feedback) is under **Reports**, as every coded report is.

## Help center

`/help` (route `KnowledgeBaseHome`) lists the articles by category: each category with articles, in the taxonomy's order, with its five most helpful and a link to the rest, beside the list of categories and their counts (a category counts those of the categories under it). `?category=` (a term's content item id) is the category's page, with its articles and those of the categories under it, and searches within it; `?q=` searches. A site without categories shows the most helpful articles. A search matches articles with every word (up to five, two letters or more) in the title or the summary, most helpful first, among the articles the reader may read (`KnowledgeArticleSearch`). Each article ends with **Was this helpful?**, posted to `help/articles/{id}/feedback`: each answer is a dated record and one more on the article's counter, once per browser, under a lock on the article so answers given at the same time all count. After a No the reader may say what was missing. Through Orchard Core's **Rate Limits** (the feature turns it on), a visitor address gives at most 20 answers in 10 minutes (group `knowledge-base-feedback`), so a script can't push an article up. See [Feedback](./knowledge-base/feedback).

The help center's page shows the zones `KnowledgeBaseTop`, `KnowledgeBaseSidebar` and `KnowledgeBaseAfterContent` itself, and so does an article's page (the `Content-KnowledgeArticle.Detail` template): the Knowledge Base recipe places the search, categories and related articles widgets there. Without widgets in them, the help center shows its own search box and categories. See [Widgets and recipe](./knowledge-base/widgets-and-recipe).

`help/api/suggest?q=` (route `KnowledgeBaseSuggest`) answers up to five articles as JSON (`title`, `summary`, `url`) for the reader, so other modules can suggest articles while someone writes: articles with any of the text's longer words (four letters or more, common words left out, the last eight) in the title or summary, those matching the most words first. With [Tickets](./tickets) on, the requester's **New request** form and the agent's reply box show these suggestions as they type; an agent adds an article's link to the reply with one click. `&reader=public` or `&reader=signedin` keeps the answer to articles that reader can open as well (the roles `Anonymous` or `Authenticated`, `KnowledgeArticleSearch.VisitorReader` and `SignedInReader`): a public reply offers only what its requester can read (articles for everyone, plus those for `Authenticated` when the requester has a login), while an internal note offers every article the agent may read.

## AI answers

Articles are content items, so an [AI data source](../ai/data-sources) that indexes the `KnowledgeArticle` type lets AI profiles answer from them. Index only articles for everyone (no role picked) for a public chat.
